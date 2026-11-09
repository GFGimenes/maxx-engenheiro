import { act, fireEvent, screen, within } from "@testing-library/react-native";
import { Platform } from "react-native";

import type { Post } from "../feed/api";
import { CartaoDePost } from "../feed/CartaoDePost";
import { MenuDoPerfil } from "../perfil/MenuDoPerfil";
import { PerfilScreen } from "../perfil/PerfilScreen";
import { json, renderComApi } from "../test/renderComApi";
import { AreaDaInstituicaoScreen } from "./AreaDaInstituicaoScreen";
import { ContestacoesScreen } from "./ContestacoesScreen";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  navigate: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
jest.mock("expo-router", () => ({
  useRouter: () => mockRoteador,
  useFocusEffect: jest.fn(),
}));

const COORDENACAO = {
  id: "0193a000-0000-7000-8000-0000000000f1",
  papeis: ["instituicao"],
  arroba: "@coordenacaoUnimax-UNIMAX",
  nomeDeExibicao: "Coordenação UniMax",
  foto: null,
};

function contestacao(
  id: string,
  nome: string,
  extra: Partial<Record<string, unknown>> = {},
) {
  return {
    id,
    premioId: `premio-${id}`,
    postId: null,
    aluno: {
      id: `aluno-${id}`,
      nomeDeExibicao: nome,
      arroba: `@${nome.toLowerCase()}-CC`,
      curso: "CC",
      semestre: 3,
      foto: null,
    },
    item: "P03",
    nomeDoItem: "Palestra ou workshop opcional",
    tier: "bronze",
    pontos: 20,
    evento: {
      id: "e1",
      nome: "Workshop de carreira e currículo",
      inicio: "2026-09-09",
      fim: "2026-09-09",
      digitado: false,
    },
    materia: null,
    foto: null,
    legenda: null,
    motivo: "O nome não está na lista de presença.",
    contestadaPor: "@renataAlbuquerque-PROF",
    contestadaEm: new Date().toISOString(),
    aprovadoEm: new Date().toISOString(),
    passouDoPrazo: false,
    ...extra,
  };
}

let eu: unknown;
let fila: jest.Mock<Promise<Response>, [Request]>;
let confirmacao: jest.Mock<Promise<Response>, [Request]>;
let revogacao: jest.Mock<Promise<Response>, [Request]>;
let contestar: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  jest.replaceProperty(Platform, "OS", "web");
  eu = COORDENACAO;
  fila = jest.fn(async (_pedido: Request) =>
    json(200, {
      itens: [
        contestacao("k1", "Lucas", { passouDoPrazo: true }),
        contestacao("k2", "Ana", {
          postId: "post-k2",
          foto: {
            feed: "http://cdn.test/feed.jpg",
            grade: "http://cdn.test/grade.jpg",
          },
        }),
      ],
      proximo: null,
    }),
  );
  confirmacao = jest.fn(
    async (_pedido: Request) => new Response(null, { status: 204 }),
  );
  revogacao = jest.fn(
    async (_pedido: Request) => new Response(null, { status: 204 }),
  );
  contestar = jest.fn(async (_pedido: Request) =>
    json(201, {
      id: "k9",
      premioId: "premio-1",
      contestadaEm: new Date().toISOString(),
    }),
  );
  api = jest.fn(async (pedido: Request) => {
    const caminho = new URL(pedido.url).pathname;
    if (caminho === "/api/v1/eu") return json(200, eu);
    if (caminho === "/api/v1/eu/perfil")
      return json(404, { status: 404, codigo: "usuario-nao-encontrado" });
    if (caminho === "/api/v1/contestacoes") return fila(pedido);
    if (caminho.endsWith("/confirmacao")) return confirmacao(pedido);
    if (caminho.endsWith("/revogacao")) return revogacao(pedido);
    if (caminho.endsWith("/contestacao")) return contestar(pedido);
    return new Response(null, { status: 404 });
  });
});

async function apertar(nome: string | RegExp, papel = "button") {
  await act(async () => {
    fireEvent.press(await screen.findByRole(papel, { name: nome }));
  });
}

describe("entrada da área da instituição", () => {
  it("no navegador, lista as filas e leva a cada uma", async () => {
    await renderComApi(<AreaDaInstituicaoScreen />, api);

    expect(
      await screen.findByRole("button", { name: "Comprovações" }),
    ).toBeOnTheScreen();
    await apertar("Contestações");

    expect(mockRoteador.push).toHaveBeenCalledWith("/instituicao/contestacoes");
  });

  it("no aparelho, explica que a área abre no navegador", async () => {
    jest.replaceProperty(Platform, "OS", "ios");
    await renderComApi(<AreaDaInstituicaoScreen />, api);

    expect(
      await screen.findByText("A área da instituição abre no navegador."),
    ).toBeOnTheScreen();
    expect(screen.queryByRole("button", { name: "Contestações" })).toBeNull();
  });

  it("quem não é da instituição não entra", async () => {
    eu = { ...COORDENACAO, papeis: ["professor"] };
    await renderComApi(<AreaDaInstituicaoScreen />, api);

    expect(
      await screen.findByText("Esta área é da instituição."),
    ).toBeOnTheScreen();
  });

  it("o menu do perfil da instituição leva à área, só no navegador", async () => {
    await renderComApi(
      <MenuDoPerfil
        aberto
        professor={false}
        instituicao
        onFechar={jest.fn()}
      />,
      api,
    );

    await apertar("Área da instituição");
    expect(mockRoteador.push).toHaveBeenCalledWith("/instituicao");
  });

  it("no aparelho, o menu do perfil não tem a área", async () => {
    jest.replaceProperty(Platform, "OS", "android");
    await renderComApi(
      <MenuDoPerfil
        aberto
        professor={false}
        instituicao
        onFechar={jest.fn()}
      />,
      api,
    );

    expect(
      await screen.findByRole("button", { name: "Sair" }),
    ).toBeOnTheScreen();
    expect(
      screen.queryByRole("button", { name: "Área da instituição" }),
    ).toBeNull();
  });

  it("a aba Perfil da conta só de instituição mostra a conta e a entrada da área", async () => {
    await renderComApi(<PerfilScreen />, api);

    expect(await screen.findByText("Coordenação UniMax")).toBeOnTheScreen();
    expect(screen.getByText("@coordenacaoUnimax-UNIMAX")).toBeOnTheScreen();
    expect(screen.queryByText("Esse perfil não existe.")).toBeNull();
    await apertar("Área da instituição");
    expect(mockRoteador.push).toHaveBeenCalledWith("/instituicao");
  });
});

describe("fila de contestações", () => {
  it("mostra as abertas na ordem da API, com motivo, quem contestou e a marca de 7 dias", async () => {
    await renderComApi(<ContestacoesScreen />, api);

    const lucas = await screen.findByTestId("contestacao-k1");
    expect(
      within(lucas).getByText("O nome não está na lista de presença."),
    ).toBeOnTheScreen();
    expect(
      within(lucas).getByText("Contestado por @renataAlbuquerque-PROF"),
    ).toBeOnTheScreen();
    expect(within(lucas).getByTestId("marca-mais-de-7-dias")).toBeOnTheScreen();
    expect(
      within(lucas).getByText(
        "Concedido direto pela instituição, sem comprovação.",
      ),
    ).toBeOnTheScreen();

    const ana = screen.getByTestId("contestacao-k2");
    expect(within(ana).getByTestId("foto-da-contestacao")).toBeOnTheScreen();
    expect(within(ana).queryByTestId("marca-mais-de-7-dias")).toBeNull();
  });

  it("confirmar manda o pedido e relê a fila", async () => {
    await renderComApi(<ContestacoesScreen />, api);
    await screen.findByTestId("contestacao-k1");
    const antes = fila.mock.calls.length;

    await apertar("Confirmar o prêmio de Lucas");

    expect(new URL(confirmacao.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/contestacoes/k1/confirmacao",
    );
    expect(fila.mock.calls.length).toBeGreaterThan(antes);
  });

  it("revogar abre a folha e manda o motivo aparado", async () => {
    await renderComApi(<ContestacoesScreen />, api);

    await apertar("Revogar o prêmio de Lucas");
    const folha = await screen.findByTestId("folha-de-motivo");
    await act(async () => {
      fireEvent.changeText(
        within(folha).getByLabelText("Motivo"),
        "  Não estava no workshop.  ",
      );
    });
    await act(async () => {
      fireEvent.press(within(folha).getByRole("button", { name: "Revogar" }));
    });

    expect(new URL(revogacao.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/contestacoes/k1/revogacao",
    );
    expect(
      await (revogacao.mock.calls[0]![0] as Request).clone().json(),
    ).toEqual({ motivo: "Não estava no workshop." });
  });

  it("se outra pessoa decidiu antes, diz isso no cartão", async () => {
    confirmacao.mockResolvedValue(
      json(409, { status: 409, codigo: "contestacao-ja-decidida" }),
    );
    await renderComApi(<ContestacoesScreen />, api);

    await apertar("Confirmar o prêmio de Lucas");

    const lucas = screen.getByTestId("contestacao-k1");
    expect(await within(lucas).findByText(/já foi decidida/)).toBeOnTheScreen();
  });

  it("sem nada aberto, um estado vazio", async () => {
    fila.mockResolvedValue(json(200, { itens: [], proximo: null }));
    await renderComApi(<ContestacoesScreen />, api);

    expect(
      await screen.findByText("Nenhuma contestação aberta."),
    ).toBeOnTheScreen();
  });
});

describe("contestar pelo menu do post, como instituição", () => {
  function post(estado: string): Post {
    return {
      id: "p1",
      autor: {
        id: "aluna",
        nomeDeExibicao: "Ana Souza",
        arroba: "@anaSouza-CC",
        curso: "CC",
        semestre: 3,
        foto: null,
      },
      legenda: null,
      foto: {
        feed: "http://cdn.test/feed.jpg",
        grade: "http://cdn.test/grade.jpg",
      },
      premio: {
        tipo: "premioParticipacao",
        id: "premio-1",
        item: "P06",
        nome: "Semana acadêmica",
        tier: "bronze",
        pontos: 30,
        raridade: null,
        materia: null,
        bimestre: null,
        estado,
        evento: "Semana Acadêmica de Computação",
      },
      criadoEm: new Date().toISOString(),
      curtidas: 0,
      comentarios: 0,
      curtiu: false,
    };
  }

  it("no prêmio válido, o menu tem contestar e remover, e contestar vai direto ao motivo", async () => {
    await renderComApi(
      <CartaoDePost
        post={post("valido")}
        leitor={{ id: COORDENACAO.id, papeis: ["instituicao"] }}
      />,
      api,
    );

    await apertar("Mais opções");
    expect(
      await screen.findByRole("button", { name: "Remover post" }),
    ).toBeOnTheScreen();
    await apertar("Contestar prêmio");
    const folha = await screen.findByTestId("folha-de-motivo");
    await act(async () => {
      fireEvent.changeText(
        within(folha).getByLabelText("Motivo"),
        "Foto de outro evento.",
      );
    });
    await act(async () => {
      fireEvent.press(within(folha).getByRole("button", { name: "Contestar" }));
    });

    expect(new URL(contestar.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/premios-participacao/premio-1/contestacao",
    );
  });

  it("no prêmio que não está válido, o menu só remove", async () => {
    await renderComApi(
      <CartaoDePost
        post={post("definitivo")}
        leitor={{ id: COORDENACAO.id, papeis: ["instituicao"] }}
      />,
      api,
    );

    await apertar("Mais opções");
    expect(
      await screen.findByRole("button", { name: "Remover post" }),
    ).toBeOnTheScreen();
    expect(
      screen.queryByRole("button", { name: "Contestar prêmio" }),
    ).toBeNull();
  });
});
