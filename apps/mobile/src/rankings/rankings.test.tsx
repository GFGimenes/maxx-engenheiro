import { act, fireEvent, screen, within } from "@testing-library/react-native";

import { json, renderComApi } from "../test/renderComApi";
import { rotuloDoSemestre, visoesPara } from "./api";
import { RankingsScreen } from "./RankingsScreen";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));

const ANA = "0193a000-0000-7000-8000-0000000000b1";
const MARIA = "0193a000-0000-7000-8000-0000000000b2";
const PEDRO = "0193a000-0000-7000-8000-0000000000b3";

const linha = (
  id: string,
  posicao: number,
  mudancas: Record<string, unknown> = {},
) => ({
  alunoId: id,
  posicao,
  pontos: 30_000 - posicao * 100,
  nomeDeExibicao: "Maria Costa",
  arroba: "@mariaCosta-PSI",
  curso: "PSI",
  nomeDoCurso: "Psicologia",
  semestre: 2,
  exAluno: false,
  foto: null,
  ...mudancas,
});

const MARIA_4 = linha(MARIA, 4);
const PEDRO_15 = linha(PEDRO, 15, {
  nomeDeExibicao: "Pedro Alves",
  curso: "ADS",
  semestre: 7,
  exAluno: true,
});
const ANA_29 = linha(ANA, 29, {
  nomeDeExibicao: "Ana Souza",
  curso: "ADS",
  semestre: 3,
  pontos: 15020,
});

let eu: Record<string, unknown>;
let meuPerfil: Record<string, unknown>;
let ranking: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  eu = {
    id: ANA,
    papeis: ["aluno"],
    arroba: "@anaSouza-ADS",
    nomeDeExibicao: "Ana Souza",
    foto: null,
  };
  meuPerfil = { id: ANA, papel: "aluno", exAluno: false };
  ranking = jest.fn(async (pedido: Request) => {
    const consulta = new URL(pedido.url).searchParams;
    return json(200, {
      escopo: consulta.get("escopo"),
      periodo: consulta.get("periodo"),
      semestre: consulta.get("periodo") === "semestre" ? "2026/2" : null,
      curso: null,
      nomeDoCurso: null,
      eu: ANA_29,
      itens: [MARIA_4, PEDRO_15, ANA_29],
      proximo: null,
    });
  });
  api = jest.fn(async (pedido: Request) => {
    const { pathname } = new URL(pedido.url);
    if (pathname === "/api/v1/eu") return json(200, eu);
    if (pathname === "/api/v1/eu/perfil") return json(200, meuPerfil);
    if (pathname === "/api/v1/rankings") return ranking(pedido);
    return new Response(null, { status: 404 });
  });
});

const consulta = (chamada = ranking.mock.calls.length - 1) =>
  new URL(ranking.mock.calls[chamada]![0].url).searchParams;

async function apertar(
  papel: "radio" | "link" | "button",
  nome: string | RegExp,
) {
  await act(async () => {
    fireEvent.press(await screen.findByRole(papel, { name: nome }));
  });
}

describe("rankings", () => {
  it("abre no geral do semestre, com o card da posição e a linha do aluno destacada", async () => {
    await renderComApi(<RankingsScreen inicial="geral" />, api);

    expect(await screen.findByText("Semestre 2/2026")).toBeOnTheScreen();
    expect([consulta().get("escopo"), consulta().get("periodo")]).toEqual([
      "geral",
      "semestre",
    ]);
    expect(consulta().get("seguidos")).toBeNull();

    const card = screen.getByTestId("card-da-posicao");
    expect(within(card).getByText("#29")).toBeOnTheScreen();
    expect(within(card).getByText("15.020 pts")).toBeOnTheScreen();
    expect(within(card).getByText("Sua posição · geral")).toBeOnTheScreen();

    const propria = screen.getByTestId(`linha-${ANA}`);
    expect(within(propria).getByText("Ana Souza · você")).toBeOnTheScreen();
    expect(within(propria).getByTestId("anel-do-avatar")).toBeOnTheScreen();
    expect(
      within(screen.getByTestId(`linha-${MARIA}`)).queryByTestId(
        "anel-do-avatar",
      ),
    ).toBeNull();
    expect(screen.getByText("#4")).toBeOnTheScreen();
    expect(screen.getByText("PSI · 2º semestre")).toBeOnTheScreen();
  });

  it("os quatro chips trocam a visão, e o histórico não mostra o semestre", async () => {
    await renderComApi(<RankingsScreen inicial="geral" />, api);

    for (const chip of ["Geral", "Curso", "Histórico", "Histórico do curso"]) {
      expect(
        await screen.findByRole("radio", { name: chip }),
      ).toBeOnTheScreen();
    }
    expect(screen.getByRole("radio", { name: "Geral" })).toBeChecked();

    await apertar("radio", "Histórico do curso");
    expect([consulta().get("escopo"), consulta().get("periodo")]).toEqual([
      "curso",
      "historico",
    ]);
    expect(
      await screen.findByText("Posição no histórico do curso"),
    ).toBeOnTheScreen();
    expect(screen.queryByText("Semestre 2/2026")).toBeNull();
  });

  it("abre no chip pedido pela rota: o perfil abre o histórico", async () => {
    await renderComApi(<RankingsScreen inicial="historico" />, api);

    await screen.findByText("Posição no histórico");
    expect([consulta().get("escopo"), consulta().get("periodo")]).toEqual([
      "geral",
      "historico",
    ]);
  });

  it("Quem eu sigo filtra a mesma visão", async () => {
    await renderComApi(<RankingsScreen inicial="geral" />, api);
    await screen.findByText("Semestre 2/2026");

    await apertar("radio", "Quem eu sigo");

    expect(consulta().get("seguidos")).toBe("true");
    expect([consulta().get("escopo"), consulta().get("periodo")]).toEqual([
      "geral",
      "semestre",
    ]);
  });

  it("o ex-aluno do histórico leva a marca", async () => {
    await renderComApi(<RankingsScreen inicial="historico" />, api);

    const pedro = await screen.findByTestId(`linha-${PEDRO}`);
    expect(within(pedro).getByText("Ex-aluno")).toBeOnTheScreen();
    expect(
      within(pedro).getByText("ADS · 7º semestre · não pontua mais"),
    ).toBeOnTheScreen();
  });

  it("sem pontos na visão, o card diz Sem posição ainda", async () => {
    ranking.mockImplementation(async () =>
      json(200, {
        escopo: "geral",
        periodo: "semestre",
        semestre: "2026/2",
        curso: null,
        nomeDoCurso: null,
        eu: null,
        itens: [MARIA_4],
        proximo: null,
      }),
    );
    await renderComApi(<RankingsScreen inicial="geral" />, api);

    const card = await screen.findByTestId("card-da-posicao");
    expect(
      await within(card).findByText("Sem posição ainda"),
    ).toBeOnTheScreen();
    expect(
      within(card).getByText("Quando você pontuar, sua posição aparece aqui."),
    ).toBeOnTheScreen();
  });

  it("o ex-aluno não tem o chip Curso e, no semestre, sabe que aparece só no histórico", async () => {
    meuPerfil = { ...meuPerfil, exAluno: true };
    ranking.mockImplementation(async () =>
      json(200, {
        escopo: "geral",
        periodo: "semestre",
        semestre: "2026/2",
        curso: null,
        nomeDoCurso: null,
        eu: null,
        itens: [MARIA_4],
        proximo: null,
      }),
    );
    await renderComApi(<RankingsScreen inicial="geral" />, api);

    expect(
      await screen.findByText("Ex-aluno aparece só no histórico."),
    ).toBeOnTheScreen();
    expect(screen.queryByRole("radio", { name: "Curso" })).toBeNull();
    expect(
      screen.getByRole("radio", { name: "Histórico do curso" }),
    ).toBeOnTheScreen();
  });

  it("o professor vê só o geral e o histórico, sem card de posição", async () => {
    eu = { ...eu, papeis: ["professor"] };
    await renderComApi(<RankingsScreen inicial="curso" />, api);

    await screen.findByText("Posição no geral");
    expect(screen.queryByRole("radio", { name: "Curso" })).toBeNull();
    expect(
      screen.queryByRole("radio", { name: "Histórico do curso" }),
    ).toBeNull();
    expect(screen.queryByTestId("card-da-posicao")).toBeNull();
    expect(consulta().get("escopo")).toBe("geral");
  });

  it("tocar numa linha abre o perfil, e Ver prêmios abre Troféus", async () => {
    await renderComApi(<RankingsScreen inicial="geral" />, api);

    await act(async () => {
      fireEvent.press(await screen.findByTestId(`linha-${MARIA}`));
    });
    expect(mockRoteador.push).toHaveBeenLastCalledWith(`/usuarios/${MARIA}`);

    await act(async () => {
      fireEvent.press(screen.getByTestId(`linha-${ANA}`));
    });
    expect(mockRoteador.push).toHaveBeenLastCalledWith("/perfil");

    await apertar("link", "Ver prêmios");
    expect(mockRoteador.push).toHaveBeenLastCalledWith("/trofeus");
  });

  it("vazio diz que ninguém pontuou, e no filtro, que ninguém que segue pontuou", async () => {
    ranking.mockImplementation(async (pedido: Request) =>
      json(200, {
        escopo: "geral",
        periodo: "semestre",
        semestre: "2026/2",
        curso: null,
        nomeDoCurso: null,
        eu: null,
        itens: [],
        proximo: null,
        seguidos: new URL(pedido.url).searchParams.get("seguidos"),
      }),
    );
    await renderComApi(<RankingsScreen inicial="geral" />, api);

    expect(
      await screen.findByText("Ninguém pontuou aqui ainda."),
    ).toBeOnTheScreen();
    await apertar("radio", "Quem eu sigo");
    expect(
      await screen.findByText("Ninguém que você segue pontuou aqui."),
    ).toBeOnTheScreen();
  });

  it("falha vira mensagem amigável com tentar de novo", async () => {
    ranking.mockRejectedValue(new TypeError("Network request failed"));
    await renderComApi(<RankingsScreen inicial="geral" />, api);

    expect(await screen.findByText(/Sem conexão/)).toBeOnTheScreen();
    expect(
      screen.getByRole("button", { name: /Tentar de novo/ }),
    ).toBeOnTheScreen();
  });

  it("o voltar volta, ou vai ao perfil quando a tela abriu direto", async () => {
    mockRoteador.canGoBack.mockReturnValueOnce(false);
    await renderComApi(<RankingsScreen inicial="geral" />, api);

    await apertar("button", "Voltar");
    expect(mockRoteador.replace).toHaveBeenCalledWith("/perfil");
  });
});

describe("regras das visões", () => {
  it("quem vê o quê segue a pergunta 6 do passo 11", () => {
    expect(visoesPara({ aluno: true, exAluno: false })).toEqual([
      "geral",
      "curso",
      "historico",
      "historico-curso",
    ]);
    expect(visoesPara({ aluno: true, exAluno: true })).toEqual([
      "geral",
      "historico",
      "historico-curso",
    ]);
    expect(visoesPara({ aluno: false, exAluno: false })).toEqual([
      "geral",
      "historico",
    ]);
  });

  it("o semestre vem como na prancheta", () => {
    expect(rotuloDoSemestre("2026/2")).toBe("Semestre 2/2026");
    expect(rotuloDoSemestre(null)).toBeNull();
  });
});
