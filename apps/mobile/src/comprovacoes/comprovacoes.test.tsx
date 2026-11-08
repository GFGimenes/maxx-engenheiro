import { act, fireEvent, screen, within } from "@testing-library/react-native";

import type { Post } from "../feed/api";
import { CartaoDePost } from "../feed/CartaoDePost";
import { MenuDoPerfil } from "../perfil/MenuDoPerfil";
import { json, renderComApi } from "../test/renderComApi";
import { ComprovacoesScreen } from "./ComprovacoesScreen";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  navigate: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));

const PROFESSORA = {
  id: "0193a000-0000-7000-8000-0000000000c9",
  papeis: ["professor"],
  arroba: "@renataA-PROF",
  nomeDeExibicao: "Renata Albuquerque",
  foto: null,
};

const SEMANA = "0193a000-0000-7000-8000-0000000000a1";

function comprovacao(
  id: string,
  nome: string,
  extra: Partial<Record<string, unknown>> = {},
) {
  return {
    id,
    premioId: `premio-${id}`,
    postId: `post-${id}`,
    aluno: {
      id: `aluno-${id}`,
      nomeDeExibicao: nome,
      arroba: `@${nome.toLowerCase()}-CC`,
      curso: "CC",
      semestre: 3,
      foto: null,
    },
    item: "P06",
    nomeDoItem: "Semana acadêmica",
    tier: "bronze",
    pontos: 30,
    evento: {
      id: SEMANA,
      nome: "Semana Acadêmica de Computação",
      inicio: "2026-09-14",
      fim: "2026-09-18",
      digitado: false,
    },
    materia: null,
    foto: {
      feed: "http://cdn.test/feed.jpg",
      grade: "http://cdn.test/grade.jpg",
    },
    legenda: "Na semana acadêmica.",
    reivindicadaEm: new Date().toISOString(),
    passouDoPrazo: false,
    materias: ["Banco de Dados"],
    ...extra,
  };
}

let fila: jest.Mock<Promise<Response>, [Request]>;
let aprovacao: jest.Mock<Promise<Response>, [Request]>;
let reprovacao: jest.Mock<Promise<Response>, [Request]>;
let contestacao: jest.Mock<Promise<Response>, [Request]>;
let eu: unknown;
let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  eu = PROFESSORA;
  fila = jest.fn(async (_pedido: Request) =>
    json(200, {
      itens: [
        comprovacao("c1", "Ana", { passouDoPrazo: true }),
        comprovacao("c2", "Bruno", {
          item: "P01",
          nomeDoItem: "Palestra obrigatória",
          evento: {
            id: "livre",
            nome: "Palestra de LGPD",
            inicio: "2026-11-03",
            fim: "2026-11-03",
            digitado: true,
          },
        }),
      ],
      proximo: null,
    }),
  );
  aprovacao = jest.fn(
    async (_pedido: Request) => new Response(null, { status: 204 }),
  );
  reprovacao = jest.fn(
    async (_pedido: Request) => new Response(null, { status: 204 }),
  );
  contestacao = jest.fn(async (_pedido: Request) =>
    json(201, {
      id: "k1",
      premioId: "p1",
      contestadaEm: new Date().toISOString(),
    }),
  );
  api = jest.fn(async (pedido: Request) => {
    const caminho = new URL(pedido.url).pathname;
    if (caminho === "/api/v1/eu") return json(200, eu);
    if (caminho === "/api/v1/comprovacoes") return fila(pedido);
    if (caminho === "/api/v1/comprovacoes/eventos")
      return json(200, [
        {
          id: SEMANA,
          nome: "Semana Acadêmica de Computação",
          inicio: "2026-09-14",
          pendentes: 1,
        },
        {
          id: "livre",
          nome: "Palestra de LGPD",
          inicio: "2026-11-03",
          pendentes: 1,
        },
      ]);
    if (caminho.endsWith("/aprovacao")) return aprovacao(pedido);
    if (caminho.endsWith("/reprovacao")) return reprovacao(pedido);
    if (caminho.endsWith("/contestacao")) return contestacao(pedido);
    return new Response(null, { status: 404 });
  });
});

async function apertar(nome: string | RegExp, papel = "button") {
  await act(async () => {
    fireEvent.press(await screen.findByRole(papel, { name: nome }));
  });
}

describe("fila de comprovações do professor", () => {
  it("mostra as pendentes na ordem da API, com evento, matérias e a marca de 7 dias", async () => {
    await renderComApi(<ComprovacoesScreen />, api);

    const ana = await screen.findByTestId("comprovacao-c1");
    expect(
      within(ana).getByText("@ana-CC · CC · 3º semestre · Banco de Dados"),
    ).toBeOnTheScreen();
    expect(
      within(ana).getByText("Semana Acadêmica de Computação · 14/09 a 18/09"),
    ).toBeOnTheScreen();
    expect(within(ana).getByTestId("marca-mais-de-7-dias")).toHaveTextContent(
      "Mais de 7 dias",
    );
    const bruno = screen.getByTestId("comprovacao-c2");
    expect(
      within(bruno).getByText("Evento digitado pelo aluno."),
    ).toBeOnTheScreen();
    expect(within(bruno).queryByTestId("marca-mais-de-7-dias")).toBeNull();
  });

  it("filtra pelo evento escolhido nos chips", async () => {
    await renderComApi(<ComprovacoesScreen />, api);

    await apertar("Palestra de LGPD (1)", "radio");

    const ultima = fila.mock.calls.at(-1)![0];
    expect(new URL(ultima.url).searchParams.get("eventoId")).toBe("livre");
    expect(screen.getByRole("radio", { name: "Todos" })).not.toBeChecked();
  });

  it("aprovar manda o pedido e relê a fila", async () => {
    await renderComApi(<ComprovacoesScreen />, api);
    const antes = fila.mock.calls.length;

    await apertar("Aprovar a comprovação de Ana");

    expect(new URL(aprovacao.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/comprovacoes/c1/aprovacao",
    );
    expect(fila.mock.calls.length).toBeGreaterThan(antes);
  });

  it("se outra pessoa decidiu antes, diz isso no cartão", async () => {
    aprovacao.mockResolvedValue(
      json(409, { status: 409, codigo: "comprovacao-ja-decidida" }),
    );
    await renderComApi(<ComprovacoesScreen />, api);

    await apertar("Aprovar a comprovação de Ana");

    expect(
      await screen.findByText("Outra pessoa já decidiu essa comprovação."),
    ).toBeOnTheScreen();
  });

  it("reprovar abre a folha, exige o motivo e manda o motivo aparado", async () => {
    await renderComApi(<ComprovacoesScreen />, api);

    await apertar("Reprovar a comprovação de Ana");
    const folha = await screen.findByTestId("folha-de-motivo");
    expect(
      within(folha).getByRole("button", { name: "Reprovar" }),
    ).toBeDisabled();

    await act(async () => {
      fireEvent.changeText(
        within(folha).getByLabelText("Motivo"),
        "  A foto não mostra o evento.  ",
      );
    });
    await act(async () => {
      fireEvent.press(within(folha).getByRole("button", { name: "Reprovar" }));
    });

    expect(
      await (reprovacao.mock.calls[0]![0] as Request).clone().json(),
    ).toEqual({
      motivo: "A foto não mostra o evento.",
    });
  });

  it("recusa da reprovação fica dentro da folha", async () => {
    reprovacao.mockResolvedValue(
      json(400, { status: 400, codigo: "motivo-invalido" }),
    );
    await renderComApi(<ComprovacoesScreen />, api);

    await apertar("Reprovar a comprovação de Ana");
    const folha = await screen.findByTestId("folha-de-motivo");
    await act(async () => {
      fireEvent.changeText(within(folha).getByLabelText("Motivo"), "Não.");
    });
    await act(async () => {
      fireEvent.press(within(folha).getByRole("button", { name: "Reprovar" }));
    });

    expect(
      await within(folha).findByText(
        "Escreva o motivo, com até 500 caracteres.",
      ),
    ).toBeOnTheScreen();
  });

  it("sem nada pendente, um estado vazio", async () => {
    fila.mockResolvedValue(json(200, { itens: [], proximo: null }));
    await renderComApi(<ComprovacoesScreen />, api);

    expect(
      await screen.findByText("Nenhuma comprovação pendente."),
    ).toBeOnTheScreen();
  });

  it("aluno não tem fila", async () => {
    eu = { ...PROFESSORA, papeis: ["aluno"] };
    await renderComApi(<ComprovacoesScreen />, api);

    expect(
      await screen.findByText("Comprovações são para quem aprova."),
    ).toBeOnTheScreen();
  });

  it("o menu do perfil da professora leva às comprovações", async () => {
    await renderComApi(
      <MenuDoPerfil aberto professor onFechar={jest.fn()} />,
      api,
    );

    await apertar("Comprovações");

    expect(mockRoteador.push).toHaveBeenCalledWith("/comprovacoes");
  });
});

describe("contestar pelo menu do post", () => {
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

  it("a professora contesta o prêmio válido com motivo", async () => {
    await renderComApi(
      <CartaoDePost
        post={post("valido")}
        leitor={{ id: PROFESSORA.id, papeis: ["professor"] }}
      />,
      api,
    );

    await apertar("Mais opções");
    await apertar("Contestar prêmio");
    const folha = await screen.findByTestId("folha-de-motivo");
    await act(async () => {
      fireEvent.changeText(
        within(folha).getByLabelText("Motivo"),
        "Revi a foto.",
      );
    });
    await act(async () => {
      fireEvent.press(within(folha).getByRole("button", { name: "Contestar" }));
    });

    const pedido = contestacao.mock.calls[0]![0] as Request;
    expect(new URL(pedido.url).pathname).toBe(
      "/api/v1/premios-participacao/premio-1/contestacao",
    );
    expect(await pedido.clone().json()).toEqual({ motivo: "Revi a foto." });
  });

  it("fora do escopo, a recusa aparece na folha", async () => {
    contestacao.mockResolvedValue(
      json(403, { status: 403, codigo: "fora-do-escopo" }),
    );
    await renderComApi(
      <CartaoDePost
        post={post("valido")}
        leitor={{ id: PROFESSORA.id, papeis: ["professor"] }}
      />,
      api,
    );

    await apertar("Mais opções");
    await apertar("Contestar prêmio");
    const folha = await screen.findByTestId("folha-de-motivo");
    await act(async () => {
      fireEvent.changeText(
        within(folha).getByLabelText("Motivo"),
        "Revi a foto.",
      );
    });
    await act(async () => {
      fireEvent.press(within(folha).getByRole("button", { name: "Contestar" }));
    });

    expect(await within(folha).findByRole("alert")).toBeOnTheScreen();
  });

  it("sem contestar no pendente nem para aluno", async () => {
    await renderComApi(
      <CartaoDePost
        post={post("reivindicado")}
        leitor={{ id: PROFESSORA.id, papeis: ["professor"] }}
      />,
      api,
    );
    expect(screen.queryByRole("button", { name: "Mais opções" })).toBeNull();

    await renderComApi(
      <CartaoDePost
        post={post("valido")}
        leitor={{ id: "outro", papeis: ["aluno"] }}
      />,
      api,
    );
    expect(screen.queryByRole("button", { name: "Mais opções" })).toBeNull();
  });
});
