import { act, fireEvent, screen, within } from "@testing-library/react-native";
import { Share } from "react-native";

import { ComentariosScreen } from "../posts/ComentariosScreen";
import { PostScreen } from "../posts/PostScreen";
import { json, renderComApi } from "../test/renderComApi";
import { FeedScreen } from "./FeedScreen";

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

const ANA = "0193a000-0000-7000-8000-0000000000b1";
const BRUNO = "0193a000-0000-7000-8000-0000000000b2";
const COORDENACAO = "0193a000-0000-7000-8000-0000000000b9";

const eu = (id: string, papeis: string[]) => ({
  id,
  papeis,
  arroba: "@x-CC",
  nomeDeExibicao: "X",
  foto: null,
});

function post(id: string, mudancas: Record<string, unknown> = {}) {
  return {
    id,
    autor: {
      id: BRUNO,
      nomeDeExibicao: "Bruno Lima",
      arroba: "@brunoLima-CC",
      curso: "CC",
      semestre: 3,
      foto: null,
      posicao: 29,
    },
    legenda: "Nota 10 em Banco de Dados!",
    foto: {
      feed: "http://fotos.test/X/feed.jpg",
      grade: "http://fotos.test/X/grade.jpg",
    },
    premio: {
      tipo: "medalha",
      id: "m1",
      item: "M01",
      nome: "Nota 10 em prova",
      tier: "bronze",
      pontos: 30,
      raridade: { nivel: "raro", percentual: 12.5 },
      materia: "Banco de Dados",
      bimestre: 2,
    },
    criadoEm: new Date(Date.now() - 2 * 3600_000).toISOString(),
    curtidas: 127,
    comentarios: 12,
    curtiu: false,
    ...mudancas,
  };
}

let quemSou: ReturnType<typeof eu>;
let feed: jest.Mock<Promise<Response>, [Request]>;
let curtida: jest.Mock<Promise<Response>, [Request]>;
let remocao: jest.Mock<Promise<Response>, [Request]>;
let comentarios: jest.Mock<Promise<Response>, [Request]>;
let comentar: jest.Mock<Promise<Response>, [Request]>;
let umPost: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  mockParametros = {};
  quemSou = eu(ANA, ["aluno"]);
  feed = jest.fn(async (_pedido: Request) =>
    json(200, { itens: [post("p1")], proximo: null }),
  );
  curtida = jest.fn(async (pedido: Request) =>
    json(200, {
      curtidas: pedido.method === "PUT" ? 128 : 127,
      curtiu: pedido.method === "PUT",
    }),
  );
  remocao = jest.fn(
    async (_pedido: Request) => new Response(null, { status: 204 }),
  );
  comentarios = jest.fn(async (_pedido: Request) =>
    json(200, {
      itens: [
        {
          id: "c1",
          autor: {
            id: BRUNO,
            nomeDeExibicao: "Bruno Lima",
            arroba: "@brunoLima-CC",
            foto: null,
          },
          texto: "Parabéns!",
          criadoEm: new Date().toISOString(),
        },
        {
          id: "c2",
          autor: {
            id: ANA,
            nomeDeExibicao: "Ana Souza",
            arroba: "@anaSouza-CC",
            foto: null,
          },
          texto: "Valeu!",
          criadoEm: new Date().toISOString(),
        },
      ],
      proximo: null,
    }),
  );
  comentar = jest.fn(async (_pedido: Request) => json(201, {}));
  umPost = jest.fn(async (_pedido: Request) => json(200, post("p1")));
  api = jest.fn(async (pedido: Request) => {
    const { pathname } = new URL(pedido.url);
    if (pathname === "/api/v1/eu") return json(200, quemSou);
    if (pathname === "/api/v1/feed") return feed(pedido);
    if (pathname.endsWith("/curtida")) return curtida(pedido);
    if (pathname.endsWith("/comentarios") && pedido.method === "GET")
      return comentarios(pedido);
    if (pathname.endsWith("/comentarios")) return comentar(pedido);
    if (pathname.includes("/comentarios/")) return remocao(pedido);
    if (pathname.startsWith("/api/v1/posts/") && pedido.method === "DELETE")
      return remocao(pedido);
    if (pathname.startsWith("/api/v1/posts/")) return umPost(pedido);
    return new Response(null, { status: 404 });
  });
});

async function apertar(nome: string | RegExp) {
  await act(async () => {
    fireEvent.press(await screen.findByRole("button", { name: nome }));
  });
}

describe("feed", () => {
  it("mostra o post como na prancheta: autor, selo, foto, faixa, curtidas, legenda, comentários e tempo", async () => {
    await renderComApi(<FeedScreen />, api);

    const cartao = await screen.findByTestId("post-p1");
    expect(within(cartao).getAllByText("brunoLima").length).toBeGreaterThan(0);
    expect(
      within(cartao).getByText(/CC · 3º semestre · #29/),
    ).toBeOnTheScreen();
    expect(within(cartao).getAllByText("Nota 10 em prova").length).toBe(2);
    expect(within(cartao).getByTestId("foto-do-post")).toBeOnTheScreen();
    expect(
      within(cartao).getByText("Banco de Dados · 2º bimestre"),
    ).toBeOnTheScreen();
    expect(within(cartao).getByText("127 curtidas")).toBeOnTheScreen();
    expect(
      within(cartao).getByText(/Nota 10 em Banco de Dados!/),
    ).toBeOnTheScreen();
    expect(
      within(cartao).getByText("Ver todos os 12 comentários"),
    ).toBeOnTheScreen();
    expect(within(cartao).getByText("há 2 horas")).toBeOnTheScreen();
    expect(within(cartao).queryByRole("button", { name: "Salvar" })).toBeNull();
  });

  it("o + abre o novo post para o aluno", async () => {
    await renderComApi(<FeedScreen />, api);
    await screen.findByTestId("post-p1");

    await apertar("Novo post");

    expect(mockRoteador.push).toHaveBeenCalledWith("/novo-post");
  });

  it("o professor não tem o + do novo post", async () => {
    quemSou = eu(BRUNO, ["professor"]);
    await renderComApi(<FeedScreen />, api);
    await screen.findByTestId("post-p1");
    expect(screen.queryByRole("button", { name: "Novo post" })).toBeNull();
  });

  it("sem post nenhum, diz isso e convida o aluno a publicar", async () => {
    feed.mockResolvedValue(json(200, { itens: [], proximo: null }));
    await renderComApi(<FeedScreen />, api);

    expect(await screen.findByText("Nenhum post ainda.")).toBeOnTheScreen();
  });

  it("falha vira mensagem amigável com tentar de novo", async () => {
    feed.mockRejectedValueOnce(new TypeError("Network request failed"));
    await renderComApi(<FeedScreen />, api);

    expect(await screen.findByText(/Sem conexão/)).toBeOnTheScreen();
  });

  it("rolando até o fim, pede a página seguinte com o cursor; puxar para atualizar começa sem cursor", async () => {
    feed.mockResolvedValueOnce(
      json(200, { itens: [post("p1")], proximo: "CURSOR-2" }),
    );
    feed.mockResolvedValueOnce(
      json(200, { itens: [post("p2")], proximo: null }),
    );
    await renderComApi(<FeedScreen />, api);
    await screen.findByTestId("post-p1");

    await act(async () => {
      fireEvent(screen.getByTestId("feed"), "onEndReached");
    });
    expect(await screen.findByTestId("post-p2")).toBeOnTheScreen();
    expect(new URL(feed.mock.calls[1]![0].url).searchParams.get("cursor")).toBe(
      "CURSOR-2",
    );

    await act(async () => {
      await screen.getByTestId("feed").props.refreshControl.props.onRefresh();
    });
    expect(
      new URL(feed.mock.calls.at(-1)![0].url).searchParams.get("cursor"),
    ).toBeNull();
  });

  it("curtir mostra na hora e acerta com a resposta da API", async () => {
    await renderComApi(<FeedScreen />, api);
    await screen.findByTestId("post-p1");

    await apertar("Curtir");

    expect(curtida.mock.calls[0]![0].method).toBe("PUT");
    expect(await screen.findByText("128 curtidas")).toBeOnTheScreen();
    expect(screen.getByRole("button", { name: "Descurtir" })).toBeOnTheScreen();
  });

  it("curtida que falha volta ao que era e avisa", async () => {
    curtida.mockRejectedValueOnce(new TypeError("Network request failed"));
    await renderComApi(<FeedScreen />, api);
    await screen.findByTestId("post-p1");

    await apertar("Curtir");

    expect(
      await screen.findByText("Não deu para curtir agora. Tente de novo."),
    ).toBeOnTheScreen();
    expect(screen.getByText("127 curtidas")).toBeOnTheScreen();
    expect(screen.getByRole("button", { name: "Curtir" })).toBeOnTheScreen();
  });

  it("a instituição não curte, mas remove o post com confirmação", async () => {
    quemSou = eu(COORDENACAO, ["instituicao"]);
    await renderComApi(<FeedScreen />, api);
    await screen.findByTestId("post-p1");
    expect(screen.queryByRole("button", { name: "Curtir" })).toBeNull();

    await apertar("Mais opções");
    await apertar("Remover post");
    expect(screen.getByText("Remover este post do feed?")).toBeOnTheScreen();
    await apertar("Remover");

    expect(remocao.mock.calls[0]![0].method).toBe("DELETE");
    expect(new URL(remocao.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/posts/p1",
    );
  });

  it("o autor apaga o próprio post; quem não é autor nem instituição não vê o menu", async () => {
    feed.mockResolvedValue(
      json(200, {
        itens: [
          post("p1", { autor: { ...post("p1").autor, id: ANA } }),
          post("p2"),
        ],
        proximo: null,
      }),
    );
    await renderComApi(<FeedScreen />, api);
    await screen.findByTestId("post-p1");

    expect(
      within(screen.getByTestId("post-p2")).queryByRole("button", {
        name: "Mais opções",
      }),
    ).toBeNull();
    await act(async () => {
      fireEvent.press(
        within(screen.getByTestId("post-p1")).getByRole("button", {
          name: "Mais opções",
        }),
      );
    });
    await apertar("Apagar post");
    await apertar("Apagar");

    expect(new URL(remocao.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/posts/p1",
    );
  });

  it("enviar abre a folha de compartilhamento com o link do post", async () => {
    const compartilhar = jest.spyOn(Share, "share").mockResolvedValue({
      action: Share.sharedAction,
      activityType: undefined,
    });
    await renderComApi(<FeedScreen />, api);
    await screen.findByTestId("post-p1");

    await apertar("Enviar");

    expect(compartilhar.mock.calls[0]![0]).toEqual(
      expect.objectContaining({
        message: expect.stringMatching(/\/post\/p1$/),
      }),
    );
  });

  it("os comentários abrem na tela própria", async () => {
    await renderComApi(<FeedScreen />, api);
    await screen.findByTestId("post-p1");

    await apertar("Comentários");

    expect(mockRoteador.push).toHaveBeenCalledWith("/post/p1/comentarios");
  });
});

describe("post pelo link", () => {
  it("mostra o post do link", async () => {
    mockParametros = { id: "p1" };
    await renderComApi(<PostScreen />, api);

    expect(await screen.findByTestId("post-p1")).toBeOnTheScreen();
  });

  it("o post que não existe mais diz isso", async () => {
    mockParametros = { id: "p1" };
    umPost.mockResolvedValue(json(404, { codigo: "post-nao-encontrado" }));
    await renderComApi(<PostScreen />, api);
    expect(
      await screen.findByText("Esse post não existe mais."),
    ).toBeOnTheScreen();
  });
});

describe("comentários", () => {
  beforeEach(() => {
    mockParametros = { id: "p1" };
  });

  it("lista do mais antigo ao mais novo e só o autor do comentário vê Apagar", async () => {
    await renderComApi(<ComentariosScreen />, api);

    const doBruno = await screen.findByTestId("comentario-c1");
    expect(within(doBruno).getByText(/Parabéns!/)).toBeOnTheScreen();
    expect(within(doBruno).queryByRole("link", { name: "Apagar" })).toBeNull();
    expect(within(doBruno).queryByText("Apagar")).toBeNull();

    await act(async () => {
      fireEvent.press(
        within(screen.getByTestId("comentario-c2")).getByText("Apagar"),
      );
    });
    expect(new URL(remocao.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/posts/p1/comentarios/c2",
    );
  });

  it("comentar envia o texto e limpa o campo", async () => {
    await renderComApi(<ComentariosScreen />, api);

    await act(async () => {
      fireEvent.changeText(
        await screen.findByLabelText("Comentário"),
        "Muito bom!",
      );
    });
    await apertar("Publicar");

    expect(await comentar.mock.calls[0]![0].clone().json()).toEqual({
      texto: "Muito bom!",
    });
    expect(screen.getByLabelText("Comentário").props.value).toBe("");
  });

  it("comentário acima de 500 caracteres não sai", async () => {
    await renderComApi(<ComentariosScreen />, api);

    await act(async () => {
      fireEvent.changeText(
        await screen.findByLabelText("Comentário"),
        "a".repeat(501),
      );
    });

    expect(
      screen.getByText("O comentário passou de 500 caracteres."),
    ).toBeOnTheScreen();
    expect(screen.getByRole("button", { name: "Publicar" })).toBeDisabled();
  });

  it("a instituição não comenta, mas remove qualquer comentário", async () => {
    quemSou = eu(COORDENACAO, ["instituicao"]);
    await renderComApi(<ComentariosScreen />, api);

    await screen.findByTestId("comentario-c1");
    expect(screen.queryByLabelText("Comentário")).toBeNull();
    expect(
      within(screen.getByTestId("comentario-c1")).getByText("Remover"),
    ).toBeOnTheScreen();
  });
});
