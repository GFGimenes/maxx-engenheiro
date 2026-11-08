import { act, fireEvent, screen } from "@testing-library/react-native";
import { launchImageLibraryAsync } from "expo-image-picker";

import { json, renderComApi } from "../test/renderComApi";
import { EditarPerfilScreen } from "./EditarPerfilScreen";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));
jest.mock("expo-image-picker", () => ({ launchImageLibraryAsync: jest.fn() }));
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

const ANA = "0193a000-0000-7000-8000-0000000000b1";

const PERFIL = {
  id: ANA,
  nomeDeExibicao: "Ana Souza",
  arroba: "@anaSouza-CC",
  papel: "aluno",
  curso: "CC",
  nomeDoCurso: "Ciência da Computação",
  semestre: 3,
  exAluno: false,
  ra: "20260101",
  pontos: 30,
  seguidores: 0,
  seguindo: 0,
  euSigo: false,
  foto: null as null | { feed: string; grade: string },
  professor: null,
};

let perfil: typeof PERFIL;
let edicao: jest.Mock<Promise<Response>, [Request]>;
let disponibilidade: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;
let rede: jest.SpyInstance;

beforeEach(() => {
  jest.clearAllMocks();
  perfil = { ...PERFIL };
  edicao = jest.fn(async (_pedido: Request) => json(200, perfil));
  disponibilidade = jest.fn(async (_pedido: Request) =>
    json(200, { disponivel: true }),
  );
  api = jest.fn(async (pedido: Request) => {
    const { pathname } = new URL(pedido.url);
    if (pathname === "/api/v1/eu/perfil" && pedido.method === "PATCH")
      return edicao(pedido);
    if (pathname === "/api/v1/eu/perfil") return json(200, perfil);
    if (pathname === "/api/v1/eu/perfil/disponibilidade")
      return disponibilidade(pedido);
    if (pathname === "/api/v1/uploads")
      return json(201, {
        id: "upload-1",
        expiraEm: "2026-11-10T13:15:00Z",
        feed: {
          url: "http://bucket.test/a/feed.jpg",
          tipoDeConteudo: "image/jpeg",
          tamanho: 300,
        },
        grade: {
          url: "http://bucket.test/a/grade.jpg",
          tipoDeConteudo: "image/jpeg",
          tamanho: 40,
        },
      });
    return new Response(null, { status: 404 });
  });
  rede = jest
    .spyOn(global, "fetch")
    .mockImplementation(async (entrada: RequestInfo | URL) =>
      String(entrada).startsWith("file:///")
        ? new Response(new Blob([new Uint8Array(100)]))
        : new Response(null, { status: 200 }),
    );
  jest.mocked(launchImageLibraryAsync).mockResolvedValue({
    canceled: false,
    assets: [{ uri: "file:///galeria.jpg", width: 1000, height: 1000 }],
  } as never);
});

afterEach(() => rede.mockRestore());

const corpoDaEdicao = async () => edicao.mock.calls[0]![0].clone().json();

async function apertar(nome: string) {
  await act(async () => {
    fireEvent.press(await screen.findByRole("button", { name: nome }));
  });
}

async function escrever(rotulo: string, texto: string) {
  await act(async () => {
    fireEvent.changeText(await screen.findByLabelText(rotulo), texto);
  });
}

describe("editar perfil", () => {
  it("mostra o que está valendo e só deixa salvar quando algo muda", async () => {
    await renderComApi(<EditarPerfilScreen />, api);

    expect((await screen.findByLabelText("Nome de exibição")).props.value).toBe(
      "Ana Souza",
    );
    expect(screen.getByLabelText("Arroba").props.value).toBe("anaSouza");
    expect(screen.getByText(/Seu arroba: @anaSouza-CC/)).toBeOnTheScreen();
    expect(screen.getByRole("button", { name: "Salvar" })).toBeDisabled();
  });

  it("salva só o nome que mudou e volta", async () => {
    await renderComApi(<EditarPerfilScreen />, api);

    await escrever("Nome de exibição", "  Aninha  ");
    await apertar("Salvar");

    expect(await corpoDaEdicao()).toEqual({
      nomeDeExibicao: "Aninha",
      prefixo: null,
      fotoUploadId: null,
      removerFoto: null,
    });
    expect(mockRoteador.back).toHaveBeenCalled();
  });

  it("confere o prefixo novo depois que a digitação para e salva se estiver livre", async () => {
    await renderComApi(<EditarPerfilScreen />, api);

    await escrever("Arroba", "aninha");
    expect(await screen.findByText(/Seu arroba: @aninha-CC/)).toBeOnTheScreen();
    await act(async () => {
      await new Promise((pronto) => setTimeout(pronto, 600));
    });
    expect(
      new URL(disponibilidade.mock.calls[0]![0].url).searchParams.get(
        "prefixo",
      ),
    ).toBe("aninha");

    await apertar("Salvar");
    expect((await corpoDaEdicao()).prefixo).toBe("aninha");
  });

  it("prefixo de outra pessoa ou fora da forma mostra o erro no campo e não salva", async () => {
    disponibilidade.mockResolvedValue(json(200, { disponivel: false }));
    await renderComApi(<EditarPerfilScreen />, api);

    await escrever("Arroba", "brunoLima");
    expect(
      await screen.findByText(
        "Esse arroba já é de outra pessoa.",
        {},
        { timeout: 2000 },
      ),
    ).toBeOnTheScreen();
    expect(screen.getByRole("button", { name: "Salvar" })).toBeDisabled();

    await escrever("Arroba", "a-b");
    expect(screen.getByText(/Use de 3 a 30 letras/)).toBeOnTheScreen();
  });

  it("recusa da API vira mensagem amigável", async () => {
    edicao.mockResolvedValue(json(409, { codigo: "prefixo-indisponivel" }));
    await renderComApi(<EditarPerfilScreen />, api);

    await escrever("Nome de exibição", "Aninha");
    await apertar("Salvar");

    expect(await screen.findByRole("alert")).toBeOnTheScreen();
    expect(mockRoteador.back).not.toHaveBeenCalled();
  });

  it("foto nova sobe como foto de perfil e vai no pedido pelo upload", async () => {
    await renderComApi(<EditarPerfilScreen />, api);

    await apertar("Escolher foto");
    expect(await screen.findByTestId("foto-de-perfil")).toBeOnTheScreen();
    await apertar("Salvar");

    const pedidoDeUpload = api.mock.calls.find(
      ([pedido]) => new URL(pedido.url).pathname === "/api/v1/uploads",
    )!;
    expect((await pedidoDeUpload[0].clone().json()).finalidade).toBe(
      "fotoDePerfil",
    );
    expect((await corpoDaEdicao()).fotoUploadId).toBe("upload-1");
  });

  it("remover a foto manda removerFoto", async () => {
    perfil = {
      ...PERFIL,
      foto: {
        feed: "http://fotos.test/f.jpg",
        grade: "http://fotos.test/g.jpg",
      },
    };
    await renderComApi(<EditarPerfilScreen />, api);

    await apertar("Remover foto");
    await apertar("Salvar");

    expect(await corpoDaEdicao()).toEqual(
      expect.objectContaining({ removerFoto: true, fotoUploadId: null }),
    );
  });
});
