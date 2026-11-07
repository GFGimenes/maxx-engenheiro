import type { components } from "@maxx/api-client";

export type SessaoResponse = components["schemas"]["SessaoResponse"];

// Restaurando: o app ainda não sabe se há sessão. Sem conexão: havia como saber, mas a
// rede falhou; a sessão guardada continua valendo para a próxima tentativa.
export type EstadoDaSessao =
  "restaurando" | "anonimo" | "autenticado" | "sem-conexao";

// Onde o refresh fica entre aberturas do app. No nativo, o secure store; na web, nada:
// o refresh anda em cookie HttpOnly, que o JavaScript nem enxerga.
export interface ArmazenamentoDoRefresh {
  ler(): Promise<string | null>;
  gravar(refresh: string): Promise<void>;
  apagar(): Promise<void>;
}

// Executa uma renovação de cada vez entre todas as abas do navegador. No nativo há um
// processo só, e a fila da própria instância basta.
export type TravaDoRefresh = <T>(renovacao: () => Promise<T>) => Promise<T>;

export interface DependenciasDaSessao {
  baseUrl: string;
  web: boolean;
  armazenamento: ArmazenamentoDoRefresh;
  trava: TravaDoRefresh;
  fetch: (request: Request) => Promise<Response>;
  agora?: () => number;
}

export type ResultadoDaRenovacao = "renovada" | "invalida";

// A API respondeu algo que não é nem sessão nem recusa de sessão (429, 5xx): a sessão
// guardada continua valendo, e quem pediu decide se tenta de novo.
export class FalhaNaRenovacao extends Error {
  constructor(readonly status: number) {
    super(`A renovação da sessão respondeu ${status}.`);
    this.name = "FalhaNaRenovacao";
  }
}

const CABECALHO_DO_CLIENTE = "X-Maxx-Cliente";
const ROTAS_DE_AUTENTICACAO = "/api/v1/auth/";

// Renova antes de vencer, para não gastar um 401 à toa.
const FOLGA_DO_ACCESS_MS = 30_000;

// Dono da sessão no cliente (decisoes/README.md, Autenticação). O access token fica só em
// memória. O refresh é serializado: dois refresh com o mesmo token contam como reuso e
// derrubam a família inteira, então só um sai por vez, e quem chega durante a renovação
// espera o resultado dela.
export class GerenciadorDeSessao {
  private accessToken: string | null = null;
  private accessExpiraEm = 0;
  private estadoAtual: EstadoDaSessao = "restaurando";
  private renovacaoEmVoo: Promise<ResultadoDaRenovacao> | null = null;
  private readonly ouvintes = new Set<() => void>();
  private readonly agora: () => number;

  constructor(private readonly deps: DependenciasDaSessao) {
    this.agora = deps.agora ?? Date.now;
  }

  get estado(): EstadoDaSessao {
    return this.estadoAtual;
  }

  observar = (ouvinte: () => void): (() => void) => {
    this.ouvintes.add(ouvinte);
    return () => this.ouvintes.delete(ouvinte);
  };

  // Na abertura do app: há sessão para continuar?
  async restaurar(): Promise<void> {
    this.mudarEstado("restaurando");
    if (!this.deps.web && (await this.deps.armazenamento.ler()) === null) {
      this.mudarEstado("anonimo");
      return;
    }

    try {
      await this.renovar();
    } catch {
      this.mudarEstado("sem-conexao");
    }
  }

  // A sessão que o login, o cadastro ou a demonstração acabaram de abrir.
  async iniciar(sessao: SessaoResponse): Promise<void> {
    await this.aplicar(sessao);
  }

  async sair(): Promise<void> {
    try {
      const refresh = this.deps.web
        ? null
        : await this.deps.armazenamento.ler();
      await this.deps.fetch(this.pedidoDeAutenticacao("logout", refresh));
    } catch {
      // Sem rede, o logout no servidor fica para trás; aqui a sessão acaba do mesmo jeito.
    }

    await this.encerrar();
  }

  // Uma renovação por vez. Quem chega durante a que está em voo recebe o resultado dela.
  renovar(): Promise<ResultadoDaRenovacao> {
    if (!this.renovacaoEmVoo) {
      this.renovacaoEmVoo = this.deps
        .trava(() => this.renovarAgora())
        .finally(() => {
          this.renovacaoEmVoo = null;
        });
    }

    return this.renovacaoEmVoo;
  }

  // O fetch do cliente da API: põe o access token, renova quando ele vence e repete uma
  // vez o pedido que voltou 401.
  fetch = async (request: Request): Promise<Response> => {
    if (ehRotaDeAutenticacao(request.url)) {
      return this.deps.fetch(this.comCabecalhoDoCliente(request));
    }

    if (
      this.accessToken &&
      this.agora() >= this.accessExpiraEm - FOLGA_DO_ACCESS_MS
    ) {
      await this.renovar();
    }

    const copia = request.clone();
    const tokenUsado = this.accessToken;
    const resposta = await this.deps.fetch(
      this.autorizado(request, tokenUsado),
    );
    if (resposta.status !== 401 || tokenUsado === null) {
      return resposta;
    }

    // Outro pedido pode ter renovado enquanto este estava na rede.
    if (this.accessToken === tokenUsado) {
      await this.renovar();
    }

    return this.accessToken === null
      ? resposta
      : this.deps.fetch(this.autorizado(copia, this.accessToken));
  };

  private async renovarAgora(): Promise<ResultadoDaRenovacao> {
    const refresh = this.deps.web ? null : await this.deps.armazenamento.ler();
    if (!this.deps.web && refresh === null) {
      await this.encerrar();
      return "invalida";
    }

    const resposta = await this.deps.fetch(
      this.pedidoDeAutenticacao("refresh", refresh),
    );
    if (resposta.status === 401) {
      await this.encerrar();
      return "invalida";
    }

    if (!resposta.ok) {
      throw new FalhaNaRenovacao(resposta.status);
    }

    await this.aplicar((await resposta.json()) as SessaoResponse);
    return "renovada";
  }

  private async aplicar(sessao: SessaoResponse): Promise<void> {
    // No nativo, o refresh novo é gravado antes de valer: o antigo já foi rotacionado e,
    // se ficasse no secure store, a próxima abertura do app o reusaria.
    if (!this.deps.web && sessao.refreshToken) {
      await this.deps.armazenamento.gravar(sessao.refreshToken);
    }

    this.accessToken = sessao.accessToken;
    this.accessExpiraEm = Date.parse(sessao.accessTokenExpiraEm);
    this.mudarEstado("autenticado");
  }

  private async encerrar(): Promise<void> {
    this.accessToken = null;
    this.accessExpiraEm = 0;
    if (!this.deps.web) {
      await this.deps.armazenamento.apagar();
    }

    this.mudarEstado("anonimo");
  }

  private pedidoDeAutenticacao(
    rota: "refresh" | "logout",
    refresh: string | null,
  ): Request {
    // Na web o refresh vem do cookie, que a API só lê com o cabeçalho do cliente.
    return this.comCabecalhoDoCliente(
      new Request(`${this.deps.baseUrl}${ROTAS_DE_AUTENTICACAO}${rota}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(this.deps.web ? {} : { refreshToken: refresh }),
        credentials: "same-origin",
      }),
    );
  }

  private comCabecalhoDoCliente(request: Request): Request {
    if (!this.deps.web) {
      return request;
    }

    const headers = new Headers(request.headers);
    headers.set(CABECALHO_DO_CLIENTE, "web");
    return new Request(request, { headers });
  }

  private autorizado(request: Request, token: string | null): Request {
    const comCliente = this.comCabecalhoDoCliente(request);
    if (token === null) {
      return comCliente;
    }

    const headers = new Headers(comCliente.headers);
    headers.set("Authorization", `Bearer ${token}`);
    return new Request(comCliente, { headers });
  }

  private mudarEstado(estado: EstadoDaSessao): void {
    if (this.estadoAtual === estado) {
      return;
    }

    this.estadoAtual = estado;
    for (const ouvinte of this.ouvintes) {
      ouvinte();
    }
  }
}

function ehRotaDeAutenticacao(url: string): boolean {
  return new URL(url, "http://maxx.local").pathname.startsWith(
    ROTAS_DE_AUTENTICACAO,
  );
}
