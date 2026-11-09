import { act, fireEvent, screen } from "@testing-library/react-native";
import { launchImageLibraryAsync } from "expo-image-picker";

import { json, renderComApi } from "../test/renderComApi";
import { NovoPostScreen } from "./NovoPostScreen";

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

const EU_ALUNA = {
  id: "0193a000-0000-7000-8000-0000000000b1",
  papeis: ["aluno"],
  arroba: "@anaSouza-CC",
  nomeDeExibicao: "Ana Souza",
  foto: null,
};

const MEDALHA_LIVRE = "0193a000-0000-7000-8000-0000000000d1";
const CONQUISTA_LIVRE = "0193a000-0000-7000-8000-0000000000c1";
const MEDALHA_POSTADA = "0193a000-0000-7000-8000-0000000000d2";
const UPLOAD = "0193a000-0000-7000-8000-0000000000e1";

const PREMIOS = {
  pontos: 60,
  familias: [],
  itens: [
    {
      codigo: "M01",
      tipo: "medalha",
      nome: "Nota 10 em prova",
      criterio: "Tirar 10 em uma prova da matéria.",
      tier: "bronze",
      pontos: 30,
      familia: null,
      ordemNaFamilia: null,
      raridade: null,
      obtidos: [
        {
          id: MEDALHA_LIVRE,
          tier: "bronze",
          pontos: 30,
          materia: "Banco de Dados",
          bimestre: 2,
          obtidoEm: "2026-11-10T13:00:00Z",
          postId: null,
        },
        {
          id: MEDALHA_POSTADA,
          tier: "bronze",
          pontos: 30,
          materia: "Redes",
          bimestre: 1,
          obtidoEm: "2026-09-10T13:00:00Z",
          postId: "0193a000-0000-7000-8000-0000000000f1",
        },
      ],
    },
    {
      codigo: "C01",
      tipo: "conquista",
      nome: "Cinco no bimestre",
      criterio: "Receber cinco medalhas no mesmo bimestre.",
      tier: "bronze",
      pontos: 25,
      familia: null,
      ordemNaFamilia: null,
      raridade: null,
      obtidos: [
        {
          id: CONQUISTA_LIVRE,
          tier: "bronze",
          pontos: 25,
          materia: null,
          bimestre: null,
          obtidoEm: "2026-11-09T13:00:00Z",
          postId: null,
          instancia: {
            periodo: "bimestre",
            ano: 2026,
            semestre: 2,
            bimestre: 4,
          },
        },
      ],
    },
  ],
};

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

let eu: jest.Mock<Promise<Response>, [Request]>;
let uploads: jest.Mock<Promise<Response>, [Request]>;
let posts: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;
let rede: jest.SpyInstance;
let bucket: jest.Mock<Promise<Response>, [string, RequestInit]>;

beforeEach(() => {
  jest.clearAllMocks();
  eu = jest.fn(async (_pedido: Request) => json(200, EU_ALUNA));
  uploads = jest.fn(async (_pedido: Request) => json(201, ASSINADO));
  posts = jest.fn(async (_pedido: Request) => json(201, { id: "novo" }));
  api = jest.fn(async (pedido: Request) => {
    const caminho = new URL(pedido.url).pathname;
    if (caminho === "/api/v1/eu") return eu(pedido);
    if (caminho === "/api/v1/eu/premios") return json(200, PREMIOS);
    if (caminho === "/api/v1/uploads") return uploads(pedido);
    if (caminho === "/api/v1/posts") return posts(pedido);
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

async function apertar(nome: string | RegExp) {
  await act(async () => {
    fireEvent.press(await screen.findByRole("button", { name: nome }));
  });
}

const corpoDe = async (
  mock: jest.Mock<Promise<Response>, [Request]>,
  chamada = 0,
) => (mock.mock.calls[chamada]?.[0] as Request).clone().json();

describe("novo post", () => {
  it("sem nada escolhido, Publicar fica desativado", async () => {
    await renderComApi(<NovoPostScreen />, api);

    expect(
      await screen.findByRole("button", { name: "Publicar" }),
    ).toBeDisabled();
  });

  it("só com legenda, publica sem foto nem prêmio e volta ao início", async () => {
    await renderComApi(<NovoPostScreen />, api);

    await act(async () => {
      fireEvent.changeText(
        await screen.findByLabelText("Legenda"),
        "Primeiro dia no campus.",
      );
    });
    await apertar("Publicar");

    expect(await corpoDe(posts)).toEqual({
      legenda: "Primeiro dia no campus.",
      uploadId: null,
      medalhaId: null,
      conquistaId: null,
    });
    expect(uploads).not.toHaveBeenCalled();
    expect(mockRoteador.dismissTo).toHaveBeenCalledWith("/");
  });

  it("com foto, pede a assinatura com os tamanhos, envia as duas derivadas ao bucket e publica com o upload", async () => {
    await renderComApi(<NovoPostScreen />, api);

    await apertar("Escolher foto");
    expect(await screen.findByTestId("previa-da-foto")).toBeOnTheScreen();
    await apertar("Publicar");

    expect(await corpoDe(uploads)).toEqual({
      finalidade: "post",
      tipoDeConteudo: "image/jpeg",
      tamanhoDoFeed: 300,
      tamanhoDaGrade: 40,
    });
    expect(bucket.mock.calls.map(([url, init]) => [url, init.method])).toEqual([
      [ASSINADO.feed.url, "PUT"],
      [ASSINADO.grade.url, "PUT"],
    ]);
    expect(bucket.mock.calls[0]?.[1].headers).toEqual({
      "Content-Type": "image/jpeg",
    });
    expect((bucket.mock.calls[0]?.[1].body as Blob).size).toBe(300);
    expect(await corpoDe(posts)).toEqual({
      legenda: null,
      uploadId: UPLOAD,
      medalhaId: null,
      conquistaId: null,
    });
  });

  it("oferece só as medalhas sem post e publica a escolhida", async () => {
    await renderComApi(<NovoPostScreen />, api);

    expect(
      await screen.findByText("Banco de Dados · 2º bimestre"),
    ).toBeOnTheScreen();
    expect(screen.queryByText("Redes · 1º bimestre")).toBeNull();

    await act(async () => {
      fireEvent.press(
        screen.getByRole("checkbox", { name: /^Nota 10 em prova/ }),
      );
    });
    await apertar("Publicar");

    expect(await corpoDe(posts)).toMatchObject({
      medalhaId: MEDALHA_LIVRE,
      conquistaId: null,
    });
  });

  it("oferece a conquista sem post, com a instância, e publica como conquista", async () => {
    await renderComApi(<NovoPostScreen />, api);

    const opcao = await screen.findByRole("checkbox", {
      name: "Cinco no bimestre, Bronze, 25 pontos, 4º bimestre de 2026",
    });
    await act(async () => {
      fireEvent.press(opcao);
    });
    await apertar("Publicar");

    expect(await corpoDe(posts)).toMatchObject({
      medalhaId: null,
      conquistaId: CONQUISTA_LIVRE,
    });
  });

  it("legenda acima de 1.000 caracteres mostra o erro e não deixa publicar", async () => {
    await renderComApi(<NovoPostScreen />, api);

    await act(async () => {
      fireEvent.changeText(
        await screen.findByLabelText("Legenda"),
        "a".repeat(1001),
      );
    });

    expect(
      screen.getByText("A legenda passou de 1.000 caracteres."),
    ).toBeOnTheScreen();
    expect(screen.getByRole("button", { name: "Publicar" })).toBeDisabled();
  });

  it("limite de envio vira mensagem amigável e deixa tentar de novo", async () => {
    uploads.mockResolvedValueOnce(json(429, { codigo: "limite-de-taxa" }));
    await renderComApi(<NovoPostScreen />, api);

    await apertar("Escolher foto");
    await screen.findByTestId("previa-da-foto");
    await apertar("Publicar");

    expect(
      await screen.findByText(
        "Muitas tentativas seguidas. Espere um pouco e tente de novo.",
      ),
    ).toBeOnTheScreen();
    expect(posts).not.toHaveBeenCalled();

    await apertar("Publicar");
    expect(posts).toHaveBeenCalledTimes(1);
  });

  it("se o post falha depois do envio, tentar de novo reaproveita a foto já enviada", async () => {
    posts.mockRejectedValueOnce(new TypeError("Network request failed"));
    await renderComApi(<NovoPostScreen />, api);

    await apertar("Escolher foto");
    await screen.findByTestId("previa-da-foto");
    await apertar("Publicar");
    expect(await screen.findByText(/Sem conexão/)).toBeOnTheScreen();

    await apertar("Publicar");

    expect(uploads).toHaveBeenCalledTimes(1);
    expect(bucket).toHaveBeenCalledTimes(2);
    expect((await corpoDe(posts, 1)).uploadId).toBe(UPLOAD);
  });

  it("se o bucket recusa o envio, avisa e a próxima tentativa sobe a foto de novo", async () => {
    bucket.mockResolvedValueOnce(new Response(null, { status: 403 }));
    await renderComApi(<NovoPostScreen />, api);

    await apertar("Escolher foto");
    await screen.findByTestId("previa-da-foto");
    await apertar("Publicar");
    expect(
      await screen.findByText("A foto não terminou de subir. Tente de novo."),
    ).toBeOnTheScreen();
    expect(posts).not.toHaveBeenCalled();

    await apertar("Publicar");
    expect(uploads).toHaveBeenCalledTimes(2);
    expect(posts).toHaveBeenCalledTimes(1);
  });

  it("desistir na galeria não muda nada", async () => {
    jest
      .mocked(launchImageLibraryAsync)
      .mockResolvedValueOnce({ canceled: true, assets: null } as never);
    await renderComApi(<NovoPostScreen />, api);

    await apertar("Escolher foto");

    expect(screen.queryByTestId("previa-da-foto")).toBeNull();
    expect(screen.getByRole("button", { name: "Publicar" })).toBeDisabled();
  });

  it("professor não publica: a tela explica", async () => {
    eu.mockResolvedValue(
      json(200, {
        ...EU_ALUNA,
        papeis: ["professor"],
        arroba: "@renataA-PROF",
      }),
    );
    await renderComApi(<NovoPostScreen />, api);

    expect(
      await screen.findByText("Só aluno publica no feed."),
    ).toBeOnTheScreen();
  });
});
