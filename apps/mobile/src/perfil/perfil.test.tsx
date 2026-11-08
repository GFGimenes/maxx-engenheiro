import { act, fireEvent, screen, within } from "@testing-library/react-native";

import { json, renderComApi } from "../test/renderComApi";
import { PerfilScreen } from "./PerfilScreen";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
jest.mock("expo-router", () => ({
  useRouter: () => mockRoteador,
  useLocalSearchParams: () => ({}),
}));

const ANA = "0193a000-0000-7000-8000-0000000000b1";
const BRUNO = "0193a000-0000-7000-8000-0000000000b2";
const RENATA = "0193a000-0000-7000-8000-0000000000c1";
const TURMA = "0193a000-0000-7000-8000-0000000000aa";

function perfilDe(id: string, mudancas: Record<string, unknown> = {}) {
  return {
    id,
    nomeDeExibicao: "Ana Souza",
    arroba: "@anaSouza-CC",
    papel: "aluno",
    curso: "CC",
    nomeDoCurso: "Ciência da Computação",
    semestre: 3,
    exAluno: false,
    ra: null,
    pontos: 15020,
    seguidores: 1234,
    seguindo: 231,
    euSigo: false,
    foto: null,
    professor: null,
    ...mudancas,
  };
}

const post = (id: string, mudancas: Record<string, unknown> = {}) => ({
  id,
  autor: {
    id: ANA,
    nomeDeExibicao: "Ana Souza",
    arroba: "@anaSouza-CC",
    curso: "CC",
    semestre: 3,
    foto: null,
  },
  legenda: null,
  foto: null,
  premio: null,
  criadoEm: "2026-11-10T13:00:00Z",
  curtidas: 0,
  comentarios: 0,
  curtiu: false,
  ...mudancas,
});

const MEDALHA = {
  tipo: "medalha",
  id: "m1",
  item: "M01",
  nome: "Nota 10 em prova",
  tier: "bronze",
  pontos: 30,
  raridade: null,
  materia: "Banco de Dados",
  bimestre: 2,
};

const PREMIOS = {
  pontos: 30,
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
      raridade: { nivel: "comum", percentual: 40 },
      obtidos: [
        {
          id: "m1",
          tier: "bronze",
          pontos: 30,
          materia: "Banco de Dados",
          bimestre: 2,
          obtidoEm: "2026-11-10T13:00:00Z",
          postId: null,
        },
      ],
    },
  ],
};

let eu: Record<string, unknown>;
let perfis: Record<string, Record<string, unknown>>;
let seguir: jest.Mock<Promise<Response>, [Request]>;
let alunosDaTurma: unknown[];
let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  eu = {
    id: ANA,
    papeis: ["aluno"],
    arroba: "@anaSouza-CC",
    nomeDeExibicao: "Ana Souza",
    foto: null,
  };
  perfis = {
    eu: perfilDe(ANA, { ra: "20260101" }),
    [BRUNO]: perfilDe(BRUNO, {
      nomeDeExibicao: "Bruno Lima",
      arroba: "@brunoLima-CC",
      seguidores: 3,
      seguindo: 2,
    }),
    [RENATA]: perfilDe(RENATA, {
      nomeDeExibicao: "Renata Albuquerque",
      arroba: "@renataA-PROF",
      papel: "professor",
      curso: null,
      nomeDoCurso: null,
      semestre: null,
      pontos: null,
      seguidores: 0,
      seguindo: 12,
      professor: {
        turmas: [
          { materia: "Banco de Dados", curso: "CC", semestre: "2026/2" },
        ],
        medalhasConcedidas: 2,
      },
    }),
  };
  alunosDaTurma = [
    {
      id: BRUNO,
      nomeDeExibicao: "Bruno Lima",
      arroba: "@brunoLima-CC",
      medalhas: [],
    },
  ];
  seguir = jest.fn(
    async (_pedido: Request) => new Response(null, { status: 204 }),
  );
  api = jest.fn(async (pedido: Request) => {
    const { pathname } = new URL(pedido.url);
    if (pathname === "/api/v1/eu") return json(200, eu);
    if (pathname === "/api/v1/eu/perfil") return json(200, perfis.eu);
    if (pathname.startsWith("/api/v1/seguindo/")) return seguir(pedido);
    if (pathname === "/api/v1/professor/turmas")
      return json(200, [
        {
          id: TURMA,
          materia: "Banco de Dados",
          curso: "CC",
          semestre: "2026/2",
          alunos: 1,
        },
      ]);
    if (pathname === `/api/v1/professor/turmas/${TURMA}/alunos`)
      return json(200, alunosDaTurma);
    if (pathname === "/api/v1/professor/medalhas") return json(200, []);
    const perfil = pathname.match(/^\/api\/v1\/perfis\/([^/]+)(\/\w+)?$/);
    if (perfil?.[2] === "/posts")
      return json(200, {
        itens: [
          post("p1", {
            foto: { feed: "http://f/1.jpg", grade: "http://f/1g.jpg" },
            premio: MEDALHA,
          }),
          post("p2", { premio: MEDALHA }),
          post("p3", { legenda: "Primeiro dia no campus." }),
        ],
        proximo: null,
      });
    if (perfil?.[2] === "/premios") return json(200, PREMIOS);
    if (perfil && perfis[perfil[1]!]) return json(200, perfis[perfil[1]!]);
    if (perfil) return json(404, { codigo: "usuario-nao-encontrado" });
    if (pathname === "/api/v1/auth/logout")
      return new Response(null, { status: 204 });
    return new Response(null, { status: 404 });
  });
});

async function apertar(
  nome: string | RegExp,
  papel: "button" | "tab" = "button",
) {
  await act(async () => {
    fireEvent.press(await screen.findByRole(papel, { name: nome }));
  });
}

describe("o próprio perfil do aluno", () => {
  it("mostra o anel dourado, pontos, contagens, nome, arroba, curso, semestre e RA", async () => {
    await renderComApi(<PerfilScreen />, api);

    expect(await screen.findByTestId("anel-do-avatar")).toBeOnTheScreen();
    expect(screen.getByText("15.020")).toBeOnTheScreen();
    expect(screen.getByLabelText("1234 seguidores")).toBeOnTheScreen();
    expect(screen.getByText("1.234")).toBeOnTheScreen();
    expect(screen.getByTestId("arroba")).toHaveTextContent("@anaSouza-CC");
    expect(
      screen.getByText("CC · 3º semestre · RA 20260101"),
    ).toBeOnTheScreen();
    expect(screen.queryByRole("button", { name: /^Seguir/ })).toBeNull();

    await apertar("Editar perfil");
    expect(mockRoteador.push).toHaveBeenCalledWith("/editar-perfil");
  });

  it("a grade tem foto com o medalhão no canto, o medalhão no metal e a legenda, e abre o post", async () => {
    await renderComApi(<PerfilScreen />, api);

    const comFoto = await screen.findByTestId("celula-p1");
    expect(within(comFoto).getAllByTestId("metal")[0]).toBeOnTheScreen();
    expect(
      within(screen.getByTestId("celula-p2")).getAllByTestId("metal")[0],
    ).toBeOnTheScreen();
    expect(
      within(screen.getByTestId("celula-p3")).getByText(
        "Primeiro dia no campus.",
      ),
    ).toBeOnTheScreen();

    await act(async () => {
      fireEvent.press(comFoto);
    });
    expect(mockRoteador.push).toHaveBeenCalledWith("/post/p1");
  });

  it("as abas por ícone trocam a grade por medalhas e troféus obtidos", async () => {
    await renderComApi(<PerfilScreen />, api);

    await apertar("Medalhas", "tab");
    expect(await screen.findByTestId("premio-M01")).toBeOnTheScreen();

    await apertar("Troféus", "tab");
    expect(await screen.findByText("Nenhum troféu ainda.")).toBeOnTheScreen();
  });

  it("o menu tem o tema e a saída", async () => {
    await renderComApi(<PerfilScreen />, api);
    await screen.findByText("15.020");

    await apertar("Menu");
    expect(
      await screen.findByRole("radio", { name: "Escuro" }),
    ).toBeOnTheScreen();
    expect(screen.queryByRole("button", { name: "Minhas turmas" })).toBeNull();

    await apertar("Sair");
    expect(
      api.mock.calls.some(
        ([pedido]) => new URL(pedido.url).pathname === "/api/v1/auth/logout",
      ),
    ).toBe(true);
  });
});

describe("o perfil de outro aluno", () => {
  it("sem anel e sem RA; Seguir segue, e Seguindo deixa de seguir", async () => {
    await renderComApi(<PerfilScreen usuarioId={BRUNO} />, api);

    expect(await screen.findByText("Bruno Lima")).toBeOnTheScreen();
    expect(screen.queryByTestId("anel-do-avatar")).toBeNull();
    expect(screen.getByText("CC · 3º semestre")).toBeOnTheScreen();
    expect(screen.queryByRole("button", { name: "Editar perfil" })).toBeNull();

    await apertar("Seguir Bruno Lima");
    expect(seguir.mock.calls[0]![0].method).toBe("PUT");
    expect(new URL(seguir.mock.calls[0]![0].url).pathname).toBe(
      `/api/v1/seguindo/${BRUNO}`,
    );
  });

  it("quem já segue vê Seguindo e deixa de seguir", async () => {
    perfis[BRUNO] = { ...perfis[BRUNO]!, euSigo: true };
    await renderComApi(<PerfilScreen usuarioId={BRUNO} />, api);

    await apertar("Deixar de seguir Bruno Lima");

    expect(seguir.mock.calls[0]![0].method).toBe("DELETE");
  });

  it("perfil que não existe diz isso", async () => {
    await renderComApi(
      <PerfilScreen usuarioId="0193a000-0000-7000-8000-00000000dead" />,
      api,
    );

    expect(
      await screen.findByText("Esse perfil não existe."),
    ).toBeOnTheScreen();
  });
});

describe("o professor", () => {
  beforeEach(() => {
    eu = {
      id: RENATA,
      papeis: ["professor"],
      arroba: "@renataA-PROF",
      nomeDeExibicao: "Renata Albuquerque",
      foto: null,
    };
  });

  it("no perfil de um aluno dele, segue e concede medalha pelo modal", async () => {
    await renderComApi(<PerfilScreen usuarioId={BRUNO} />, api);

    await apertar("Conceder medalha");
    expect(
      await screen.findByLabelText("Conceder medalha", { exact: true }),
    ).toBeOnTheScreen();
    expect(
      screen.getByRole("button", { name: "Seguir Bruno Lima" }),
    ).toBeOnTheScreen();
  });

  it("no perfil de quem não é aluno dele, nem segue nem concede", async () => {
    alunosDaTurma = [];
    await renderComApi(<PerfilScreen usuarioId={BRUNO} />, api);

    await screen.findByText("Bruno Lima");
    expect(
      screen.queryByRole("button", { name: "Conceder medalha" }),
    ).toBeNull();
    expect(screen.queryByRole("button", { name: /^Seguir/ })).toBeNull();
  });

  it("o perfil do professor mostra as turmas e as medalhas concedidas, sem pontos nem abas", async () => {
    await renderComApi(<PerfilScreen usuarioId={RENATA} />, api);

    expect(await screen.findByText("Turmas neste semestre")).toBeOnTheScreen();
    expect(screen.getByText("CC · 2026/2")).toBeOnTheScreen();
    expect(
      screen.getByText("Professor · 2 medalhas concedidas"),
    ).toBeOnTheScreen();
    expect(screen.queryByText("pts")).toBeNull();
    expect(screen.queryByRole("tab", { name: "Medalhas" })).toBeNull();
  });

  it("no próprio perfil, o menu leva às turmas", async () => {
    perfis.eu = perfis[RENATA]!;
    await renderComApi(<PerfilScreen />, api);
    await screen.findByText("Turmas neste semestre");

    await apertar("Menu");
    await apertar("Minhas turmas");

    expect(mockRoteador.push).toHaveBeenCalledWith("/turmas");
  });
});
