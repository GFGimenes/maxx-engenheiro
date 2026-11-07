import {
  FalhaNaRenovacao,
  GerenciadorDeSessao,
  type ArmazenamentoDoRefresh,
  type SessaoResponse,
  type TravaDoRefresh,
} from "./GerenciadorDeSessao";

const BASE = "http://maxx.test";
const AGORA = Date.parse("2026-11-07T12:00:00Z");

function sessao(n: number, refreshNoCorpo = true): SessaoResponse {
  return {
    accessToken: `a${n}`,
    accessTokenExpiraEm: new Date(AGORA + 15 * 60_000).toISOString(),
    refreshToken: refreshNoCorpo ? `r${n}` : null,
    refreshTokenExpiraEm: new Date(AGORA + 30 * 86_400_000).toISOString(),
  };
}

function json(status: number, corpo: unknown): Response {
  return new Response(JSON.stringify(corpo), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

// A API como o passo 3 a fez: cada refresh vale uma vez, e o reuso derruba a família.
// Responde devagar para os pedidos se sobreporem de verdade.
function apiFalsa({ web = false } = {}) {
  let versao = 1;
  let familiaViva = true;
  const refreshs: string[] = [];
  let emAndamento = 0;
  let sobreposicoes = 0;
  const pedidosProtegidos: { url: string; auth: string | null }[] = [];
  let falharRede = false;
  let statusDoRefresh: number | null = null;
  // Na web o cookie é o do navegador, comum a todas as abas.
  const cookie = { valor: "r1" };

  const fetch = jest.fn(async (request: Request): Promise<Response> => {
    const caminho = new URL(request.url).pathname;
    if (falharRede) {
      throw new TypeError("Network request failed");
    }

    if (caminho === "/api/v1/auth/refresh") {
      emAndamento++;
      if (emAndamento > 1) sobreposicoes++;
      try {
        // O cookie vai com o pedido: é o que o navegador tinha no instante do envio.
        const apresentado = web
          ? request.headers.get("X-Maxx-Cliente") === "web"
            ? cookie.valor
            : null
          : ((await request.json()) as { refreshToken: string | null })
              .refreshToken;
        await new Promise((resolve) => setTimeout(resolve, 5));
        if (statusDoRefresh !== null) return json(statusDoRefresh, {});
        refreshs.push(String(apresentado));
        if (!familiaViva || apresentado !== `r${versao}`) {
          familiaViva = false;
          return json(401, { codigo: "sessao-invalida" });
        }
        versao++;
        cookie.valor = `r${versao}`;
        return json(200, sessao(versao, !web));
      } finally {
        emAndamento--;
      }
    }

    if (caminho === "/api/v1/auth/logout") {
      familiaViva = false;
      return new Response(null, { status: 204 });
    }

    const auth = request.headers.get("Authorization");
    pedidosProtegidos.push({ url: caminho, auth });
    await new Promise((resolve) => setTimeout(resolve, 1));
    return auth === `Bearer a${versao}` && familiaViva
      ? json(200, { ok: true, eco: caminho })
      : json(401, { codigo: "nao-autenticado" });
  });

  return {
    fetch,
    refreshs,
    pedidosProtegidos,
    get sobreposicoes() {
      return sobreposicoes;
    },
    get familiaViva() {
      return familiaViva;
    },
    // Simula o access vencido no servidor: o próximo pedido volta 401.
    vencerAccess: () => {
      versao++;
      cookie.valor = `r${versao}`;
      refreshs.length = 0;
    },
    falharRede: (falhar: boolean) => {
      falharRede = falhar;
    },
    responderRefreshCom: (status: number | null) => {
      statusDoRefresh = status;
    },
  };
}

function memoria(inicial: string | null = null) {
  let valor = inicial;
  const armazenamento: ArmazenamentoDoRefresh = {
    ler: jest.fn(async () => valor),
    gravar: jest.fn(async (novo: string) => {
      valor = novo;
    }),
    apagar: jest.fn(async () => {
      valor = null;
    }),
  };
  return { armazenamento, atual: () => valor };
}

const semTrava: TravaDoRefresh = (renovacao) => renovacao();

function gerenciador(
  api: ReturnType<typeof apiFalsa>,
  opcoes: {
    web?: boolean;
    armazenamento?: ArmazenamentoDoRefresh;
    trava?: TravaDoRefresh;
    agora?: () => number;
  } = {},
) {
  return new GerenciadorDeSessao({
    baseUrl: BASE,
    web: opcoes.web ?? false,
    armazenamento: opcoes.armazenamento ?? memoria().armazenamento,
    trava: opcoes.trava ?? semTrava,
    fetch: api.fetch,
    agora: opcoes.agora ?? (() => AGORA),
  });
}

const get = (caminho: string) => new Request(`${BASE}${caminho}`);

describe("refresh serializado", () => {
  it("cinco pedidos que voltam 401 juntos geram um refresh só, e os cinco são repetidos com o token novo", async () => {
    const api = apiFalsa();
    const { armazenamento } = memoria();
    const sessaoAtual = gerenciador(api, { armazenamento });
    await sessaoAtual.iniciar(sessao(1));
    api.vencerAccess();
    // O servidor está na versão 2: o refresh válido agora é o r2.
    await armazenamento.gravar("r2");

    const respostas = await Promise.all(
      [1, 2, 3, 4, 5].map((n) => sessaoAtual.fetch(get(`/api/v1/coisa/${n}`))),
    );

    expect(respostas.map((r) => r.status)).toEqual([200, 200, 200, 200, 200]);
    expect(api.refreshs).toEqual(["r2"]);
    expect(api.familiaViva).toBe(true);
    const repetidos = api.pedidosProtegidos.slice(5);
    expect(repetidos.map((p) => p.auth)).toEqual(Array(5).fill("Bearer a3"));
  });

  it("quem chega durante a renovação espera o resultado dela, sem novo refresh", async () => {
    const api = apiFalsa();
    const { armazenamento } = memoria("r1");
    const sessaoAtual = gerenciador(api, { armazenamento });

    const primeira = sessaoAtual.renovar();
    const segunda = sessaoAtual.renovar();

    expect(segunda).toBe(primeira);
    await expect(primeira).resolves.toBe("renovada");
    expect(api.refreshs).toEqual(["r1"]);
  });

  it("access perto de vencer é renovado antes do pedido, sem gastar um 401", async () => {
    const api = apiFalsa();
    let agora = AGORA;
    const { armazenamento } = memoria("r1");
    const sessaoAtual = gerenciador(api, { armazenamento, agora: () => agora });
    await sessaoAtual.restaurar();
    agora += 15 * 60_000 - 10_000;

    const resposta = await sessaoAtual.fetch(get("/api/v1/eu"));

    expect(resposta.status).toBe(200);
    expect(api.pedidosProtegidos).toEqual([
      { url: "/api/v1/eu", auth: "Bearer a3" },
    ]);
  });

  it("na web, duas abas abrindo juntas renovam uma depois da outra e a sessão sobrevive", async () => {
    const api = apiFalsa({ web: true });
    // A Web Locks API: uma fila entre todas as abas do navegador.
    let fila: Promise<unknown> = Promise.resolve();
    const travaEntreAbas: TravaDoRefresh = (renovacao) => {
      const vez = fila.then(renovacao);
      fila = vez.catch(() => undefined);
      return vez;
    };
    const abaA = gerenciador(api, { web: true, trava: travaEntreAbas });
    const abaB = gerenciador(api, { web: true, trava: travaEntreAbas });

    await Promise.all([abaA.restaurar(), abaB.restaurar()]);

    expect(api.sobreposicoes).toBe(0);
    expect(api.refreshs).toEqual(["r1", "r2"]);
    expect(api.familiaViva).toBe(true);
    expect([abaA.estado, abaB.estado]).toEqual(["autenticado", "autenticado"]);
  });

  it("sem a fila entre abas, as mesmas duas abas derrubariam a sessão", async () => {
    const api = apiFalsa({ web: true });
    const abaA = gerenciador(api, { web: true });
    const abaB = gerenciador(api, { web: true });

    await Promise.all([abaA.restaurar(), abaB.restaurar()]);

    expect(api.sobreposicoes).toBe(1);
    expect(api.familiaViva).toBe(false);
  });
});

describe("fim da sessão", () => {
  it("sessao-invalida leva ao anônimo, apaga o refresh guardado e devolve o 401 original", async () => {
    const api = apiFalsa();
    const { armazenamento, atual } = memoria();
    const sessaoAtual = gerenciador(api, { armazenamento });
    await sessaoAtual.iniciar(sessao(1));
    api.vencerAccess();
    // r1 já foi rotacionado no servidor: apresentá-lo é reuso.

    const resposta = await sessaoAtual.fetch(get("/api/v1/eu"));

    expect(resposta.status).toBe(401);
    expect(sessaoAtual.estado).toBe("anonimo");
    expect(atual()).toBeNull();
    expect(api.pedidosProtegidos).toHaveLength(1);
  });

  it("o pedido é repetido uma vez só: se o token novo também volta 401, a resposta é devolvida", async () => {
    const api = apiFalsa();
    const { armazenamento } = memoria("r1");
    const sessaoAtual = gerenciador(api, { armazenamento });
    await sessaoAtual.restaurar();
    // Daqui em diante todo pedido protegido volta 401, renove quantas vezes renovar.
    api.fetch.mockImplementation(async (request: Request) => {
      if (new URL(request.url).pathname === "/api/v1/auth/refresh") {
        return json(200, sessao(9));
      }
      return json(401, { codigo: "nao-autenticado" });
    });

    const resposta = await sessaoAtual.fetch(get("/api/v1/eu"));

    expect(resposta.status).toBe(401);
    const caminhos = api.fetch.mock.calls.map(([r]) => new URL(r.url).pathname);
    expect(caminhos.slice(-3)).toEqual([
      "/api/v1/eu",
      "/api/v1/auth/refresh",
      "/api/v1/eu",
    ]);
  });

  it("sair revoga no servidor, esquece o refresh e volta ao anônimo, mesmo sem rede", async () => {
    const api = apiFalsa();
    const { armazenamento, atual } = memoria("r1");
    const sessaoAtual = gerenciador(api, { armazenamento });
    await sessaoAtual.restaurar();

    await sessaoAtual.sair();
    expect(api.familiaViva).toBe(false);
    expect(atual()).toBeNull();
    expect(sessaoAtual.estado).toBe("anonimo");

    await sessaoAtual.iniciar(sessao(5));
    api.falharRede(true);
    await sessaoAtual.sair();
    expect(sessaoAtual.estado).toBe("anonimo");
  });
});

describe("rede", () => {
  it("falha de rede na abertura não encerra a sessão: fica sem conexão e o refresh continua guardado", async () => {
    const api = apiFalsa();
    const { armazenamento, atual } = memoria("r1");
    const sessaoAtual = gerenciador(api, { armazenamento });
    api.falharRede(true);

    await sessaoAtual.restaurar();

    expect(sessaoAtual.estado).toBe("sem-conexao");
    expect(atual()).toBe("r1");

    api.falharRede(false);
    await sessaoAtual.restaurar();
    expect(sessaoAtual.estado).toBe("autenticado");
  });

  it("429 ou 5xx na renovação não encerram a sessão", async () => {
    const api = apiFalsa();
    const { armazenamento, atual } = memoria("r1");
    const sessaoAtual = gerenciador(api, { armazenamento });
    api.responderRefreshCom(503);

    await expect(sessaoAtual.renovar()).rejects.toBeInstanceOf(
      FalhaNaRenovacao,
    );
    expect(atual()).toBe("r1");
    expect(sessaoAtual.estado).not.toBe("anonimo");
  });

  it("falha de rede no meio de um pedido chega a quem pediu, sem mexer na sessão", async () => {
    const api = apiFalsa();
    const sessaoAtual = gerenciador(api);
    await sessaoAtual.iniciar(sessao(1));
    api.falharRede(true);

    await expect(sessaoAtual.fetch(get("/api/v1/eu"))).rejects.toBeInstanceOf(
      TypeError,
    );
    expect(sessaoAtual.estado).toBe("autenticado");
  });
});

describe("onde cada token mora", () => {
  it("no nativo, sem refresh guardado, a abertura vai direto ao anônimo, sem pedido", async () => {
    const api = apiFalsa();
    const sessaoAtual = gerenciador(api);

    await sessaoAtual.restaurar();

    expect(sessaoAtual.estado).toBe("anonimo");
    expect(api.fetch).not.toHaveBeenCalled();
  });

  it("no nativo, o refresh novo é gravado antes de o pedido ser repetido", async () => {
    const api = apiFalsa();
    const { armazenamento } = memoria("r1");
    const sessaoAtual = gerenciador(api, { armazenamento });
    const ordem: string[] = [];
    (armazenamento.gravar as jest.Mock).mockImplementation(async () => {
      ordem.push("gravou");
    });
    api.fetch.mockImplementationOnce(async () => {
      ordem.push("refresh");
      return json(200, sessao(2));
    });

    await sessaoAtual.renovar();

    expect(ordem).toEqual(["refresh", "gravou"]);
    expect(armazenamento.gravar).toHaveBeenCalledWith("r2");
  });

  it("na web, nada é guardado pelo JavaScript e todo pedido leva o cabeçalho do cliente", async () => {
    const api = apiFalsa({ web: true });
    const { armazenamento } = memoria();
    const sessaoAtual = gerenciador(api, { web: true, armazenamento });

    await sessaoAtual.restaurar();
    await sessaoAtual.fetch(get("/api/v1/eu"));
    await sessaoAtual.fetch(
      new Request(`${BASE}/api/v1/auth/login`, { method: "POST" }),
    );

    expect(armazenamento.gravar).not.toHaveBeenCalled();
    expect(armazenamento.ler).not.toHaveBeenCalled();
    for (const [request] of api.fetch.mock.calls) {
      expect(request.headers.get("X-Maxx-Cliente")).toBe("web");
    }
  });

  it("rota de autenticação passa sem token e sem renovação, mesmo voltando 401", async () => {
    const api = apiFalsa();
    const sessaoAtual = gerenciador(api);
    await sessaoAtual.iniciar(sessao(1));
    api.fetch.mockImplementationOnce(async () =>
      json(401, { codigo: "credenciais-invalidas" }),
    );

    const resposta = await sessaoAtual.fetch(
      new Request(`${BASE}/api/v1/auth/login`, {
        method: "POST",
        body: JSON.stringify({ identificador: "x", senha: "y" }),
      }),
    );

    expect(resposta.status).toBe(401);
    expect(api.fetch).toHaveBeenCalledTimes(1);
    expect(api.fetch.mock.calls[0]![0].headers.get("Authorization")).toBeNull();
    expect(sessaoAtual.estado).toBe("autenticado");
  });

  it("o pedido repetido leva o mesmo corpo do original", async () => {
    const api = apiFalsa();
    const { armazenamento } = memoria();
    const sessaoAtual = gerenciador(api, { armazenamento });
    await sessaoAtual.iniciar(sessao(1));
    api.vencerAccess();
    await armazenamento.gravar("r2");
    const corpos: string[] = [];
    const original = api.fetch.getMockImplementation()!;
    api.fetch.mockImplementation(async (request: Request) => {
      if (new URL(request.url).pathname === "/api/v1/coisa") {
        corpos.push(await request.clone().text());
      }
      return original(request);
    });

    const resposta = await sessaoAtual.fetch(
      new Request(`${BASE}/api/v1/coisa`, {
        method: "POST",
        body: JSON.stringify({ texto: "oi" }),
      }),
    );

    expect(resposta.status).toBe(200);
    expect(corpos).toEqual(['{"texto":"oi"}', '{"texto":"oi"}']);
  });
});
