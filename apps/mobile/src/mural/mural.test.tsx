import {
  act,
  fireEvent,
  screen,
  waitFor,
  within,
} from "@testing-library/react-native";

import { caminhos, json, renderComApi } from "../test/renderComApi";
import { AvisoScreen } from "./AvisoScreen";
import { MuralScreen } from "./MuralScreen";
import { autorDoAviso, dataDoAviso, rotuloDoEscopo } from "./textos";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  dismissTo: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
let mockParametros: Record<string, string> = {};
jest.mock("expo-router", () => ({
  useRouter: () => mockRoteador,
  useLocalSearchParams: () => mockParametros,
}));

function aviso(id: string, mudancas: Record<string, unknown> = {}) {
  return {
    id,
    escopo: "faculdade",
    titulo: "Matrícula do 2º semestre até 30/07",
    texto: "Renove sua matrícula pelo Portal do Aluno até 30/07.",
    foto: null,
    publicadoEm: "2026-07-24T15:00:00Z",
    autor: {
      id: "u9",
      nomeDeExibicao: "Coordenação UniMax",
      instituicao: true,
    },
    materias: [],
    cursos: [],
    lido: false,
    ...mudancas,
  };
}

// O toque dentro de act assíncrono, como no teste do feed: a mudança de estado só chega
// à tela depois dele.
async function apertar(elemento: Parameters<typeof fireEvent.press>[0]) {
  await act(async () => {
    fireEvent.press(elemento);
  });
}

let mural: jest.Mock<Promise<Response>, [Request]>;
let umAviso: jest.Mock<Promise<Response>, [Request]>;
let leitura: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  mockParametros = {};
  mural = jest.fn(async (_pedido: Request) =>
    json(200, {
      itens: [
        aviso("a1"),
        aviso("a2", { titulo: "Techday: inscrições abertas", lido: true }),
      ],
      proximo: null,
    }),
  );
  umAviso = jest.fn(async (_pedido: Request) => json(200, aviso("a1")));
  leitura = jest.fn(
    async (_pedido: Request) => new Response(null, { status: 204 }),
  );
  api = jest.fn(async (pedido: Request) => {
    const { pathname } = new URL(pedido.url);
    if (pathname === "/api/v1/mural") return mural(pedido);
    if (pathname.endsWith("/leitura")) return leitura(pedido);
    if (pathname.startsWith("/api/v1/avisos/")) return umAviso(pedido);
    return new Response(null, { status: 404 });
  });
});

describe("textos do mural", () => {
  it("a data é dia e mês em São Paulo", () => {
    expect(dataDoAviso("2026-07-24T15:00:00Z")).toBe("24/07");
    // 1h da manhã em UTC ainda é o dia anterior em São Paulo.
    expect(dataDoAviso("2026-08-01T01:00:00Z")).toBe("31/07");
  });

  it("o rótulo diz o escopo, o curso ou as matérias", () => {
    const base = { cursos: [], materias: [] };
    expect(rotuloDoEscopo({ ...base, escopo: "faculdade" })).toBe("Faculdade");
    expect(rotuloDoEscopo({ ...base, escopo: "curso", cursos: ["CC"] })).toBe(
      "Curso · CC",
    );
    expect(
      rotuloDoEscopo({
        ...base,
        escopo: "turma",
        materias: ["Algoritmos", "Banco de Dados"],
      }),
    ).toBe("Algoritmos · Banco de Dados");
  });

  it("a instituição assina como UniMax e o professor pelo nome", () => {
    expect(
      autorDoAviso({ nomeDeExibicao: "Coordenação", instituicao: true }),
    ).toBe("UniMax");
    expect(
      autorDoAviso({
        nomeDeExibicao: "Renata Albuquerque",
        instituicao: false,
      }),
    ).toBe("Renata Albuquerque");
  });
});

describe("aba Mural", () => {
  it("o não lido vem aberto com bolinha e o lido vem recolhido", async () => {
    await renderComApi(<MuralScreen />, api);

    expect(await screen.findByTestId("bolinha-nao-lido")).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Mural" })).toBeTruthy();
    // O não lido: bolinha, rótulo, data, título com "não lido", texto, autor e Abrir.
    expect(screen.getByTestId("bolinha-nao-lido")).toBeTruthy();
    const cartao = screen.getByTestId("aviso-a1");
    expect(within(cartao).getByText("Faculdade")).toBeTruthy();
    expect(within(cartao).getByText("24/07")).toBeTruthy();
    expect(
      screen.getByRole("heading", {
        name: "Matrícula do 2º semestre até 30/07, não lido",
      }),
    ).toBeTruthy();
    expect(screen.getByText("UniMax")).toBeTruthy();
    expect(
      screen.getByRole("link", {
        name: "Abrir o aviso Matrícula do 2º semestre até 30/07",
      }),
    ).toBeTruthy();

    // O lido: só o título, recolhido. Tocar abre ali mesmo, sem registrar leitura.
    expect(screen.queryByTestId("aviso-a2")).toBeNull();
    await apertar(
      screen.getByRole("button", {
        name: "Techday: inscrições abertas. Mostrar aviso",
      }),
    );
    expect(screen.getByTestId("aviso-a2")).toBeTruthy();
    expect(screen.getAllByTestId("bolinha-nao-lido")).toHaveLength(1);
    expect(leitura).not.toHaveBeenCalled();
  });

  it("Abrir leva ao aviso inteiro", async () => {
    await renderComApi(<MuralScreen />, api);
    await screen.findByTestId("aviso-a1");

    await apertar(
      screen.getByRole("link", {
        name: "Abrir o aviso Matrícula do 2º semestre até 30/07",
      }),
    );

    expect(mockRoteador.push).toHaveBeenCalledWith("/aviso/a1");
  });

  it("os chips pedem o escopo, e Professores é o das turmas", async () => {
    await renderComApi(<MuralScreen />, api);
    await screen.findByTestId("aviso-a1");

    for (const [rotulo, escopo] of [
      ["Faculdade", "faculdade"],
      ["Curso", "curso"],
      ["Professores", "turma"],
    ] as const) {
      await apertar(screen.getByRole("radio", { name: rotulo }));
      await waitFor(() =>
        expect(new URL(mural.mock.calls.at(-1)![0].url).search).toBe(
          `?escopo=${escopo}`,
        ),
      );
    }

    await apertar(screen.getByRole("radio", { name: "Todos" }));
    await waitFor(() =>
      expect(new URL(mural.mock.calls.at(-1)![0].url).search).toBe(""),
    );
  });

  it("Ler todos marca o mural e relê", async () => {
    await renderComApi(<MuralScreen />, api);
    await screen.findByTestId("aviso-a1");
    const antes = mural.mock.calls.length;

    await apertar(screen.getByRole("button", { name: "Ler todos os avisos" }));
    await waitFor(() => expect(mural.mock.calls.length).toBeGreaterThan(antes));

    const pedido = leitura.mock.calls[0]![0];
    expect(new URL(pedido.url).pathname).toBe("/api/v1/mural/leitura");
    expect(pedido.method).toBe("POST");
    expect(mural.mock.calls.length).toBeGreaterThan(antes);
  });

  it("sem aviso não lido, Ler todos fica desativado", async () => {
    mural.mockImplementation(async () =>
      json(200, { itens: [aviso("a1", { lido: true })], proximo: null }),
    );
    await renderComApi(<MuralScreen />, api);
    await screen.findByText("Matrícula do 2º semestre até 30/07");

    expect(
      screen.getByRole("button", { name: "Ler todos os avisos" }),
    ).toBeDisabled();
  });

  it("sem aviso, o estado vazio diz o que aparece aqui", async () => {
    mural.mockImplementation(async () =>
      json(200, { itens: [], proximo: null }),
    );
    await renderComApi(<MuralScreen />, api);

    expect(await screen.findByText("Nenhum aviso ainda.")).toBeTruthy();
    await apertar(screen.getByRole("radio", { name: "Professores" }));
    await act(async () => {});
    expect(
      await screen.findByText("Nenhum aviso dos professores."),
    ).toBeTruthy();
  });

  it("falha vira mensagem amigável com tentar de novo", async () => {
    mural.mockImplementation(async () => json(500, { codigo: "erro-interno" }));
    await renderComApi(<MuralScreen />, api);

    expect(
      await screen.findByText(
        "Não deu certo agora. Tente de novo em instantes.",
      ),
    ).toBeTruthy();
    mural.mockImplementation(async () =>
      json(200, { itens: [aviso("a1")], proximo: null }),
    );
    await apertar(screen.getByRole("button", { name: "Tentar de novo" }));
    await act(async () => {});
    expect(await screen.findByTestId("aviso-a1")).toBeTruthy();
  });
});

describe("aviso aberto", () => {
  it("mostra a imagem e registra a leitura uma vez", async () => {
    mockParametros = { id: "a1" };
    umAviso.mockImplementation(async () =>
      json(
        200,
        aviso("a1", {
          foto: {
            feed: "http://fotos.test/A/feed.jpg",
            grade: "http://fotos.test/A/grade.jpg",
          },
        }),
      ),
    );
    await renderComApi(<AvisoScreen />, api);
    await act(async () => {});

    expect(
      await screen.findByRole("heading", {
        name: "Matrícula do 2º semestre até 30/07",
      }),
    ).toBeTruthy();
    expect(screen.getByTestId("foto-do-aviso").props.source).toEqual({
      uri: "http://fotos.test/A/feed.jpg",
    });
    expect(screen.getByText("UniMax")).toBeTruthy();

    const leituras = leitura.mock.calls.map(([pedido]) => pedido);
    expect(leituras).toHaveLength(1);
    expect(new URL(leituras[0]!.url).pathname).toBe(
      "/api/v1/avisos/a1/leitura",
    );
    expect(leituras[0]!.method).toBe("PUT");
  });

  it("o aviso já lido não registra de novo", async () => {
    mockParametros = { id: "a1" };
    umAviso.mockImplementation(async () =>
      json(200, aviso("a1", { lido: true })),
    );
    await renderComApi(<AvisoScreen />, api);
    await act(async () => {});
    await screen.findByText("UniMax");

    expect(leitura).not.toHaveBeenCalled();
    expect(caminhos(api)).toContain("/api/v1/avisos/a1");
  });

  it("aviso fora do mural explica e volta ao Mural", async () => {
    mockParametros = { id: "a9" };
    umAviso.mockImplementation(async () =>
      json(404, { codigo: "aviso-nao-encontrado" }),
    );
    await renderComApi(<AvisoScreen />, api);

    expect(
      await screen.findByText("Esse aviso não está no seu mural."),
    ).toBeTruthy();
    await apertar(screen.getByRole("button", { name: "Ir para o Mural" }));
    expect(mockRoteador.dismissTo).toHaveBeenCalledWith("/mural");
  });
});
