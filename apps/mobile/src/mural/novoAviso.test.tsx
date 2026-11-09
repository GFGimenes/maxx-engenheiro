import { act, fireEvent, screen } from "@testing-library/react-native";
import { launchImageLibraryAsync } from "expo-image-picker";

import { json, renderComApi } from "../test/renderComApi";
import { MuralScreen } from "./MuralScreen";
import { NovoAvisoScreen } from "./NovoAvisoScreen";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  dismissTo: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));

jest.mock("expo-image-picker", () => ({
  launchImageLibraryAsync: jest.fn(),
}));

// Cada derivada sai como um arquivo próprio; o tamanho vem do fetch dele, abaixo.
jest.mock("expo-image-manipulator", () => {
  let gerada = 0;
  return {
    SaveFormat: { JPEG: "jpeg" },
    ImageManipulator: {
      manipulate: () => {
        const contexto = {
          crop: () => contexto,
          resize: () => contexto,
          renderAsync: async () => ({
            saveAsync: async () => ({
              uri: `file:///derivada-${++gerada}.jpg`,
              width: 0,
              height: 0,
            }),
          }),
        };
        return contexto;
      },
    },
  };
});

const BD = "0193a000-0000-7000-8000-0000000000a1";
const ALG = "0193a000-0000-7000-8000-0000000000a2";
const UPLOAD = "0193a000-0000-7000-8000-0000000000e1";

const professora = {
  id: "0193a000-0000-7000-8000-0000000000p1",
  papeis: ["professor"],
  arroba: "@renataA-PROF",
  nomeDeExibicao: "Renata Albuquerque",
  foto: null,
};
const aluna = { ...professora, papeis: ["aluno"], arroba: "@anaSouza-CC" };

const TURMAS = [
  {
    id: BD,
    materia: "Banco de Dados",
    curso: "CC",
    semestre: "2026/2",
    alunos: 30,
  },
  {
    id: ALG,
    materia: "Algoritmos",
    curso: "CC",
    semestre: "2026/2",
    alunos: 28,
  },
];

const ASSINADO = {
  id: UPLOAD,
  expiraEm: "2026-11-10T13:15:00Z",
  feed: {
    url: "http://bucket.test/maxx-fotos/X/feed.jpg?assinado",
    tipoDeConteudo: "image/jpeg",
    tamanho: 300,
  },
  grade: {
    url: "http://bucket.test/maxx-fotos/X/grade.jpg?assinado",
    tipoDeConteudo: "image/jpeg",
    tamanho: 40,
  },
};

let quemSou: typeof professora;
let uploads: jest.Mock<Promise<Response>, [Request]>;
let avisos: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;
let rede: jest.SpyInstance;
let bucket: jest.Mock<Promise<Response>, [string, RequestInit]>;

beforeEach(() => {
  jest.clearAllMocks();
  quemSou = professora;
  uploads = jest.fn(async (_pedido: Request) => json(201, ASSINADO));
  avisos = jest.fn(async (_pedido: Request) => json(201, { id: "novo" }));
  api = jest.fn(async (pedido: Request) => {
    const caminho = new URL(pedido.url).pathname;
    if (caminho === "/api/v1/eu") return json(200, quemSou);
    if (caminho === "/api/v1/professor/turmas") return json(200, TURMAS);
    if (caminho === "/api/v1/uploads") return uploads(pedido);
    if (caminho === "/api/v1/avisos") return avisos(pedido);
    if (caminho === "/api/v1/mural")
      return json(200, { itens: [], proximo: null });
    return new Response(null, { status: 404 });
  });

  // O que não é a API: ler a derivada gerada (file://) e o PUT direto no bucket.
  bucket = jest.fn(
    async (_url: string, _init: RequestInit) =>
      new Response(null, { status: 200 }),
  );
  rede = jest
    .spyOn(global, "fetch")
    .mockImplementation(
      async (entrada: RequestInfo | URL, init?: RequestInit) => {
        const url = String(entrada);
        if (url.startsWith("file:///derivada-")) {
          const numero = Number(url.match(/derivada-(\d+)/)?.[1]);
          return new Response(
            new Blob([new Uint8Array(numero % 2 === 1 ? 300 : 40)]),
          );
        }
        return bucket(url, init ?? {});
      },
    );
  jest.mocked(launchImageLibraryAsync).mockResolvedValue({
    canceled: false,
    assets: [{ uri: "file:///galeria.jpg", width: 4000, height: 3000 }],
  } as never);
});

afterEach(() => rede.mockRestore());

async function apertar(nome: string | RegExp, papel = "button" as const) {
  await act(async () => {
    fireEvent.press(await screen.findByRole(papel, { name: nome }));
  });
}

async function escrever(rotulo: string, texto: string) {
  await act(async () => {
    fireEvent.changeText(await screen.findByLabelText(rotulo), texto);
  });
}

async function marcarTurma(nome: string) {
  await act(async () => {
    fireEvent.press(await screen.findByRole("checkbox", { name: nome }));
  });
}

const corpoDe = async (
  mock: jest.Mock<Promise<Response>, [Request]>,
  chamada = 0,
) => (mock.mock.calls[chamada]?.[0] as Request).clone().json();

describe("novo aviso", () => {
  it("o + do Mural abre o novo aviso para o professor", async () => {
    await renderComApi(<MuralScreen />, api);
    await apertar("Novo aviso");
    expect(mockRoteador.push).toHaveBeenCalledWith("/novo-aviso");
  });

  it("o aluno não vê o + do Mural", async () => {
    quemSou = aluna;
    await renderComApi(<MuralScreen />, api);
    await screen.findByText("Nenhum aviso ainda.");
    expect(screen.queryByRole("button", { name: "Novo aviso" })).toBeNull();
  });

  it("publicar exige ao menos uma turma, título de 3 caracteres e texto", async () => {
    await renderComApi(<NovoAvisoScreen />, api);
    const publicar = () =>
      screen.findByRole("button", { name: "Publicar aviso" });

    expect(await publicar()).toBeDisabled();
    await escrever("Título", "Prova remarcada");
    await escrever("Texto", "Passou para 14/08.");
    expect(await publicar()).toBeDisabled();

    await marcarTurma("Banco de Dados · CC");
    expect(await publicar()).toBeEnabled();

    await escrever("Título", "ab");
    expect(await publicar()).toBeDisabled();
    await escrever("Título", "Prova remarcada");
    await escrever("Texto", "   ");
    expect(await publicar()).toBeDisabled();
  });

  it("título e texto longos avisam e não deixam publicar", async () => {
    await renderComApi(<NovoAvisoScreen />, api);
    await marcarTurma("Banco de Dados · CC");

    await escrever("Título", "a".repeat(121));
    await escrever("Texto", "a".repeat(2001));

    expect(screen.getByText("O título passou de 120 caracteres.")).toBeTruthy();
    expect(
      screen.getByText("O texto passou de 2.000 caracteres."),
    ).toBeTruthy();
    expect(screen.getByText("121/120")).toBeTruthy();
    expect(screen.getByText("2.001/2.000")).toBeTruthy();
    expect(
      screen.getByRole("button", { name: "Publicar aviso" }),
    ).toBeDisabled();
  });

  it("publica para duas turmas sem foto e volta ao Mural", async () => {
    await renderComApi(<NovoAvisoScreen />, api);

    await marcarTurma("Banco de Dados · CC");
    await marcarTurma("Algoritmos · CC");
    await escrever("Título", "Prova remarcada");
    await escrever("Texto", "Passou para 14/08.");
    await apertar("Publicar aviso");

    expect(await corpoDe(avisos)).toEqual({
      escopo: "turma",
      turmaIds: [BD, ALG],
      titulo: "Prova remarcada",
      texto: "Passou para 14/08.",
      uploadId: null,
    });
    expect(uploads).not.toHaveBeenCalled();
    expect(mockRoteador.dismissTo).toHaveBeenCalledWith("/mural");
  });

  it("desmarcar a turma tira ela do aviso", async () => {
    await renderComApi(<NovoAvisoScreen />, api);

    await marcarTurma("Banco de Dados · CC");
    await marcarTurma("Algoritmos · CC");
    await marcarTurma("Banco de Dados · CC");
    await escrever("Título", "Lista nova");
    await escrever("Texto", "No portal.");
    await apertar("Publicar aviso");

    expect((await corpoDe(avisos)).turmaIds).toEqual([ALG]);
  });

  it("com foto, sobe as derivadas como aviso e publica com o upload", async () => {
    await renderComApi(<NovoAvisoScreen />, api);

    await apertar("Escolher foto");
    expect(await screen.findByTestId("previa-da-foto")).toBeTruthy();
    await marcarTurma("Banco de Dados · CC");
    await escrever("Título", "Laboratório novo");
    await escrever("Texto", "Sala 12.");
    await apertar("Publicar aviso");

    expect(await corpoDe(uploads)).toEqual({
      finalidade: "aviso",
      tipoDeConteudo: "image/jpeg",
      tamanhoDoFeed: 300,
      tamanhoDaGrade: 40,
    });
    expect(bucket.mock.calls.map(([url]) => url)).toEqual([
      ASSINADO.feed.url,
      ASSINADO.grade.url,
    ]);
    expect((await corpoDe(avisos)).uploadId).toBe(UPLOAD);
  });

  it("a recusa aparece na tela, sem perder o que foi digitado, e a foto já enviada é reaproveitada", async () => {
    avisos.mockImplementationOnce(async () =>
      json(403, { codigo: "fora-do-escopo" }),
    );
    await renderComApi(<NovoAvisoScreen />, api);

    await apertar("Escolher foto");
    await screen.findByTestId("previa-da-foto");
    await marcarTurma("Banco de Dados · CC");
    await escrever("Título", "Prova remarcada");
    await escrever("Texto", "Passou para 14/08.");
    await apertar("Publicar aviso");

    expect(
      await screen.findByText("Essa turma não é sua neste semestre."),
    ).toBeTruthy();
    expect(screen.getByDisplayValue("Prova remarcada")).toBeTruthy();
    expect(screen.getByDisplayValue("Passou para 14/08.")).toBeTruthy();
    expect(mockRoteador.dismissTo).not.toHaveBeenCalled();

    await apertar("Publicar aviso");
    expect(uploads).toHaveBeenCalledTimes(1);
    expect((await corpoDe(avisos, 1)).uploadId).toBe(UPLOAD);
    expect(mockRoteador.dismissTo).toHaveBeenCalledWith("/mural");
  });

  it("aluno não publica aviso", async () => {
    quemSou = aluna;
    await renderComApi(<NovoAvisoScreen />, api);

    expect(
      await screen.findByText("Só professor publica aviso aqui."),
    ).toBeTruthy();
  });
});
