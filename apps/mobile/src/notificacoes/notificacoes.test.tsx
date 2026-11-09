import { act, fireEvent, screen, waitFor } from "@testing-library/react-native";
import { readFileSync } from "fs";
import { join } from "path";

import { MuralScreen } from "../mural/MuralScreen";
import { json, renderComApi } from "../test/renderComApi";
import { TrofeusScreen } from "../trofeus/TrofeusScreen";
import { NotificacoesScreen } from "./NotificacoesScreen";
import { Sininho } from "./Sininho";
import { APRESENTACOES, apresentar } from "./textos";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  dismissTo: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));

const REF = "0193a000-0000-7000-8000-0000000000r1";
const POST = "0193a000-0000-7000-8000-0000000000f1";

// Os tipos que a API grava, lidos do enum do domínio, em camelCase como no JSON.
function tiposDaApi(): string[] {
  const fonte = readFileSync(
    join(__dirname, "../../../api/src/Maxx.Domain/Notificacoes/Notificacao.cs"),
    "utf8",
  );
  const corpo = fonte.match(/enum TipoDeNotificacao\s*\{([^}]*)\}/)![1]!;
  return [...corpo.matchAll(/^\s*([A-Z]\w*),/gm)].map(
    ([, nome]) => nome![0]!.toLowerCase() + nome!.slice(1),
  );
}

describe("textos das notificações", () => {
  it("todo tipo que a API grava tem texto e destino", () => {
    const tipos = tiposDaApi();

    expect(tipos).toHaveLength(10);
    expect(Object.keys(APRESENTACOES).sort()).toEqual([...tipos].sort());
  });

  it.each([
    [
      { tipo: "medalhaRecebida", item: "Nota 10 em prova" },
      "Você recebeu a medalha Nota 10 em prova.",
      "/trofeus",
    ],
    [
      { tipo: "comprovacaoAprovada", item: "Semana acadêmica", postId: POST },
      "Sua comprovação de Semana acadêmica foi aprovada.",
      `/post/${POST}`,
    ],
    [
      {
        tipo: "comprovacaoReprovada",
        item: "Semana acadêmica",
        motivo: "Foto de outro evento.",
      },
      "Sua comprovação de Semana acadêmica foi reprovada: Foto de outro evento.",
      "/trofeus",
    ],
    [
      { tipo: "contestacaoAberta", item: "Semana acadêmica", postId: POST },
      "Seu prêmio Semana acadêmica foi contestado e está em averiguação.",
      `/post/${POST}`,
    ],
    [
      {
        tipo: "decisaoDeContestacao",
        item: "Semana acadêmica",
        decisao: "confirmada",
        postId: POST,
      },
      "A contestação de Semana acadêmica terminou e o prêmio foi confirmado.",
      `/post/${POST}`,
    ],
    [
      {
        tipo: "decisaoDeContestacao",
        item: "Semana acadêmica",
        decisao: "revogada",
        motivo: "Foto de outro evento",
      },
      "A contestação de Semana acadêmica terminou e o prêmio foi revogado: Foto de outro evento.",
      "/trofeus",
    ],
    [
      {
        tipo: "premioRevogado",
        item: "Palestra",
        motivo: "Removido pela instituição.",
      },
      "Seu prêmio Palestra foi revogado: Removido pela instituição.",
      "/trofeus",
    ],
    [
      { tipo: "conquistaObtida", item: "Cinco no bimestre" },
      "Você obteve a conquista Cinco no bimestre.",
      "/trofeus",
    ],
    [
      { tipo: "conquistaRemovida", item: "Cinco no bimestre" },
      "A conquista Cinco no bimestre foi removida no recálculo dos seus prêmios.",
      "/trofeus",
    ],
    [
      { tipo: "avisoNovo", tituloDoAviso: "Matrícula até 30/07" },
      "Aviso novo no Mural: Matrícula até 30/07",
      `/aviso/${REF}`,
    ],
    [
      { tipo: "premioConcedido", item: "Monitoria" },
      "A UniMax te concedeu Monitoria.",
      "/trofeus",
    ],
  ])("%o", (dados, texto, destino) => {
    const apresentacao = apresentar({ referenciaId: REF, ...dados });

    expect(apresentacao.texto).toBe(texto);
    expect(apresentacao.destino).toBe(destino);
  });

  it("sem o item, a frase continua inteira", () => {
    expect(
      apresentar({ tipo: "medalhaRecebida", referenciaId: REF }).texto,
    ).toBe("Você recebeu a medalha de um professor.");
    expect(
      apresentar({ tipo: "comprovacaoReprovada", referenciaId: REF }).texto,
    ).toBe("Sua comprovação de prêmio foi reprovada.");
  });

  it("tipo que o app ainda não conhece vira texto genérico, sem destino", () => {
    expect(apresentar({ tipo: "tipoDoFuturo", referenciaId: REF })).toEqual({
      texto: "Você tem uma novidade.",
      icone: "inbox",
      destino: null,
    });
  });
});

function notificacao(id: string, mudancas: Record<string, unknown> = {}) {
  return {
    id,
    tipo: "medalhaRecebida",
    referenciaId: REF,
    criadaEm: new Date(Date.now() - 5 * 60_000).toISOString(),
    lida: false,
    item: "Nota 10 em prova",
    motivo: null,
    decisao: null,
    postId: null,
    tituloDoAviso: null,
    ...mudancas,
  };
}

const aluna = {
  id: "0193a000-0000-7000-8000-0000000000b1",
  papeis: ["aluno"],
  arroba: "@anaSouza-CC",
  nomeDeExibicao: "Ana Souza",
  foto: null,
};

let maisNova: string;
let quemSou: typeof aluna;
let novidades: { sininho: boolean; mural: boolean; trofeus: boolean };
let lista: jest.Mock<Promise<Response>, [Request]>;
let leitura: jest.Mock<Promise<Response>, [Request]>;
let trofeusVistos: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  quemSou = aluna;
  maisNova = new Date(Date.now() - 60_000).toISOString();
  novidades = { sininho: true, mural: false, trofeus: false };
  lista = jest.fn(async (_pedido: Request) =>
    json(200, {
      itens: [
        notificacao("n2", {
          tipo: "avisoNovo",
          tituloDoAviso: "Matrícula até 30/07",
          item: null,
          criadaEm: maisNova,
        }),
        notificacao("n1", { lida: true }),
      ],
      proximo: null,
    }),
  );
  leitura = jest.fn(
    async (_pedido: Request) => new Response(null, { status: 204 }),
  );
  trofeusVistos = jest.fn(
    async (_pedido: Request) => new Response(null, { status: 204 }),
  );
  api = jest.fn(async (pedido: Request) => {
    const caminho = new URL(pedido.url).pathname;
    if (caminho === "/api/v1/eu") return json(200, quemSou);
    if (caminho === "/api/v1/eu/novidades") return json(200, novidades);
    if (caminho === "/api/v1/eu/notificacoes") return lista(pedido);
    if (caminho === "/api/v1/eu/notificacoes/leitura") return leitura(pedido);
    if (caminho === "/api/v1/eu/trofeus/visto") return trofeusVistos(pedido);
    if (caminho === "/api/v1/eu/premios")
      return json(200, { pontos: 0, familias: [], itens: [] });
    if (caminho === "/api/v1/mural")
      return json(200, {
        itens: [
          {
            id: "a1",
            escopo: "faculdade",
            titulo: "Recesso",
            texto: "Sexta.",
            foto: null,
            publicadoEm: "2026-07-24T15:00:00Z",
            autor: {
              id: "u9",
              nomeDeExibicao: "Coordenação",
              instituicao: true,
            },
            materias: [],
            cursos: [],
            lido: true,
          },
        ],
        proximo: null,
      });
    return new Response(null, { status: 404 });
  });
});

async function apertar(nome: string | RegExp) {
  await act(async () => {
    fireEvent.press(await screen.findByRole("button", { name: nome }));
  });
}

describe("sininho", () => {
  it("com notificação não lida, mostra a bolinha e diz no rótulo", async () => {
    await renderComApi(<Sininho />, api);

    expect(await screen.findByTestId("novidade-sininho")).toBeTruthy();
    await apertar("Notificações, com novidade");
    expect(mockRoteador.push).toHaveBeenCalledWith("/notificacoes");
  });

  it("sem não lida, sem bolinha", async () => {
    novidades = { sininho: false, mural: false, trofeus: false };
    await renderComApi(<Sininho />, api);

    expect(
      await screen.findByRole("button", { name: "Notificações" }),
    ).toBeTruthy();
    await waitFor(() =>
      expect(api.mock.calls.map(([p]) => new URL(p.url).pathname)).toContain(
        "/api/v1/eu/novidades",
      ),
    );
    expect(screen.queryByTestId("novidade-sininho")).toBeNull();
  });
});

describe("tela de notificações", () => {
  it("mostra cada uma com o texto, a não lida em destaque, e marca como lidas até a mais nova", async () => {
    await renderComApi(<NotificacoesScreen />, api);

    expect(
      await screen.findByText("Aviso novo no Mural: Matrícula até 30/07"),
    ).toBeTruthy();
    expect(
      screen.getByText("Você recebeu a medalha Nota 10 em prova."),
    ).toBeTruthy();
    expect(screen.getAllByTestId("bolinha-nao-lida")).toHaveLength(1);
    expect(
      screen.getByRole("button", {
        name: /Matrícula até 30\/07 há 1 minuto, não lida$/,
      }),
    ).toBeTruthy();

    await waitFor(() => expect(leitura).toHaveBeenCalledTimes(1));
    const corpo = await leitura.mock.calls[0]![0].clone().json();
    expect(corpo).toEqual({ ate: maisNova });

    // Enquanto a tela está aberta, a que era nova continua em destaque.
    expect(screen.getAllByTestId("bolinha-nao-lida")).toHaveLength(1);
  });

  it("tudo já lido não pede leitura", async () => {
    lista.mockImplementation(async () =>
      json(200, { itens: [notificacao("n1", { lida: true })], proximo: null }),
    );
    await renderComApi(<NotificacoesScreen />, api);

    await screen.findByText("Você recebeu a medalha Nota 10 em prova.");
    await act(async () => {});
    expect(leitura).not.toHaveBeenCalled();
    expect(screen.queryByTestId("bolinha-nao-lida")).toBeNull();
  });

  it("o toque leva ao destino do tipo", async () => {
    await renderComApi(<NotificacoesScreen />, api);

    await apertar(/^Aviso novo no Mural/);
    expect(mockRoteador.push).toHaveBeenCalledWith(`/aviso/${REF}`);
    await apertar(/^Você recebeu a medalha/);
    expect(mockRoteador.push).toHaveBeenCalledWith("/trofeus");
  });

  it("sem notificação, o estado vazio explica o que aparece aqui", async () => {
    lista.mockImplementation(async () =>
      json(200, { itens: [], proximo: null }),
    );
    await renderComApi(<NotificacoesScreen />, api);

    expect(await screen.findByText("Nenhuma notificação ainda.")).toBeTruthy();
  });

  it("falha vira mensagem amigável", async () => {
    lista.mockImplementation(async () => json(500, { codigo: "erro-interno" }));
    await renderComApi(<NotificacoesScreen />, api);

    expect(
      await screen.findByText(
        "Não deu certo agora. Tente de novo em instantes.",
      ),
    ).toBeTruthy();
  });
});

describe("bolinhas fora do sininho", () => {
  it("abrir Troféus marca o prêmio novo como visto, só para aluno", async () => {
    await renderComApi(<TrofeusScreen />, api);
    await waitFor(() => expect(trofeusVistos).toHaveBeenCalledTimes(1));
    expect(trofeusVistos.mock.calls[0]![0].method).toBe("PUT");
  });

  it("o professor abre Troféus sem marcar nada", async () => {
    quemSou = { ...aluna, papeis: ["professor"] };
    await renderComApi(<TrofeusScreen />, api);

    await screen.findByText("Troféus e medalhas são dos alunos.");
    await act(async () => {});
    expect(trofeusVistos).not.toHaveBeenCalled();
  });

  it("Ler todos segue a bolinha do Mural, mesmo com a lista do filtro toda lida", async () => {
    novidades = { sininho: false, mural: true, trofeus: false };
    await renderComApi(<MuralScreen />, api);

    await screen.findByText("Recesso");
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Ler todos os avisos" }),
      ).toBeEnabled(),
    );
  });
});
