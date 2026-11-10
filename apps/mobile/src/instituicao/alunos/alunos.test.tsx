import { act, fireEvent, screen, within } from "@testing-library/react-native";
import { Platform } from "react-native";

import { json, renderComApi } from "../../test/renderComApi";
import { BuscaDeAlunosScreen } from "./BuscaDeAlunosScreen";
import { ConcederPremioScreen } from "./ConcederPremioScreen";
import { FichaDoAlunoScreen } from "./FichaDoAlunoScreen";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  navigate: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
const mockParametros = { id: "cad-lucas" };
jest.mock("expo-router", () => ({
  useRouter: () => mockRoteador,
  useLocalSearchParams: () => mockParametros,
}));

const COORDENACAO = {
  id: "0193a000-0000-7000-8000-0000000000f1",
  papeis: ["instituicao"],
  arroba: "@coordenacaoUnimax-UNIMAX",
  nomeDeExibicao: "Coordenação UniMax",
  foto: null,
};

const CC = "0193a000-0000-7000-8000-0000000000c1";
const ADM = "0193a000-0000-7000-8000-0000000000c2";

function aluno(extra: Partial<Record<string, unknown>> = {}) {
  return {
    id: "cad-lucas",
    nomeCompleto: "Lucas Ferreira",
    ra: "20260102",
    email: "lucas.ferreira@al.unieduk.invalid",
    cursoId: CC,
    curso: "CC",
    semestre: 3,
    demonstracao: false,
    conta: {
      id: "usuario-lucas",
      nomeDeExibicao: "Lucas Ferreira",
      arroba: "@lucasFerreira-CC",
      exAlunoDesde: null,
      foto: null,
    },
    ...extra,
  };
}

const MEDALHA = {
  id: "m1",
  item: "M01",
  nomeDoItem: "Nota 10 em prova",
  tier: "bronze",
  pontos: 30,
  materia: "Banco de Dados",
  ano: 2026,
  bimestre: 4,
  concedidaPor: "Renata Albuquerque",
  concedidaEm: "2026-10-20T15:00:00Z",
};

let ficha: unknown;
let busca: jest.Mock<Promise<Response>, [Request]>;
let atos: jest.Mock<Promise<Response>, [Request]>;
let concessao: jest.Mock<Promise<Response>, [Request]>;
let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  jest.replaceProperty(Platform, "OS", "web");
  ficha = { aluno: aluno(), medalhas: [MEDALHA] };
  busca = jest.fn(async (_pedido: Request) =>
    json(200, [
      aluno(),
      aluno({
        id: "cad-elisa",
        nomeCompleto: "Élisa Prado",
        ra: "20260199",
        conta: null,
      }),
    ]),
  );
  atos = jest.fn(
    async (_pedido: Request) => new Response(null, { status: 204 }),
  );
  concessao = jest.fn(async (_pedido: Request) =>
    json(201, {
      id: "premio-novo",
      alunoId: "usuario-lucas",
      item: "P06",
      tier: "bronze",
      pontos: 30,
      concedidoEm: new Date().toISOString(),
    }),
  );
  api = jest.fn(async (pedido: Request) => {
    const caminho = new URL(pedido.url).pathname;
    if (caminho === "/api/v1/eu") return json(200, COORDENACAO);
    if (caminho === "/api/v1/cursos")
      return json(200, [
        { id: ADM, sigla: "ADM", nome: "Administração", duracaoEmSemestres: 8 },
        {
          id: CC,
          sigla: "CC",
          nome: "Ciência da Computação",
          duracaoEmSemestres: 8,
        },
      ]);
    if (caminho === "/api/v1/instituicao/alunos") return busca(pedido);
    if (caminho === "/api/v1/instituicao/alunos/cad-lucas")
      return json(200, ficha);
    if (caminho.endsWith("/premios-participacao") && pedido.method === "GET")
      return json(200, [
        {
          codigo: "P06",
          nome: "Semana acadêmica",
          criterio: "Participar da semana acadêmica do curso.",
          tier: "bronze",
          pontos: 30,
          exigeMateria: false,
        },
      ]);
    if (caminho.endsWith("/premios-participacao")) return concessao(pedido);
    if (caminho.endsWith("/eventos"))
      return json(200, [
        {
          id: "semana",
          nome: "Semana Acadêmica de Ciência da Computação",
          inicio: "2026-09-14",
          fim: "2026-09-18",
          itens: ["P06"],
        },
      ]);
    return atos(pedido);
  });
});

async function apertar(nome: string | RegExp, papel = "button") {
  await act(async () => {
    fireEvent.press(await screen.findByRole(papel, { name: nome }));
  });
}

async function corpo(mock: jest.Mock<Promise<Response>, [Request]>) {
  return (mock.mock.calls.at(-1)![0] as Request).clone().json();
}

describe("busca de alunos da instituição", () => {
  it("busca pelo termo e lista com e sem conta, e tocar abre a ficha", async () => {
    await renderComApi(<BuscaDeAlunosScreen />, api);

    await act(async () => {
      fireEvent.changeText(await screen.findByLabelText("Buscar aluno"), "lu");
    });

    expect(await screen.findByText("Élisa Prado")).toBeOnTheScreen();
    expect(
      new URL(busca.mock.calls.at(-1)![0].url).searchParams.get("busca"),
    ).toBe("lu");
    expect(
      screen.getByText("CC · 3º semestre · RA 20260199 · Sem conta"),
    ).toBeOnTheScreen();
    await apertar("Lucas Ferreira");
    expect(mockRoteador.push).toHaveBeenCalledWith(
      "/instituicao/alunos/cad-lucas",
    );
  });

  it("antes de 2 letras, nada a buscar", async () => {
    await renderComApi(<BuscaDeAlunosScreen />, api);

    expect(await screen.findByText("Busque um aluno.")).toBeOnTheScreen();
    expect(busca).not.toHaveBeenCalled();
  });
});

describe("ficha do aluno", () => {
  it("mostra a base, a conta, as medalhas e os atos do aluno ativo", async () => {
    await renderComApi(<FichaDoAlunoScreen />, api);

    expect(
      await screen.findByText("lucas.ferreira@al.unieduk.invalid"),
    ).toBeOnTheScreen();
    expect(screen.getByText("CC · 3º semestre")).toBeOnTheScreen();
    expect(screen.getByText("Ativo")).toBeOnTheScreen();
    expect(
      within(screen.getByTestId("medalha-m1")).getByText(
        "Banco de Dados · 4º bimestre de 2026 · Renata Albuquerque",
      ),
    ).toBeOnTheScreen();
    for (const ato of [
      "Corrigir e-mail",
      "Trocar de curso",
      "Conceder prêmio",
      "Marcar ex-aluno",
    ]) {
      expect(screen.getByRole("button", { name: ato })).toBeOnTheScreen();
    }
  });

  it("sem conta, só corrige e-mail e troca de curso", async () => {
    ficha = { aluno: aluno({ conta: null }), medalhas: [] };
    await renderComApi(<FichaDoAlunoScreen />, api);

    expect(
      await screen.findByText("Ainda não criou a conta"),
    ).toBeOnTheScreen();
    expect(
      screen.getByRole("button", { name: "Trocar de curso" }),
    ).toBeOnTheScreen();
    expect(
      screen.queryByRole("button", { name: "Conceder prêmio" }),
    ).toBeNull();
    expect(
      screen.queryByRole("button", { name: "Marcar ex-aluno" }),
    ).toBeNull();
  });

  it("ex-aluno, só corrige e-mail", async () => {
    ficha = {
      aluno: aluno({
        conta: { ...aluno().conta, exAlunoDesde: "2026-06-28T03:00:00Z" },
      }),
      medalhas: [],
    };
    await renderComApi(<FichaDoAlunoScreen />, api);

    expect(
      await screen.findByText("Ex-aluno desde 28/06/2026"),
    ).toBeOnTheScreen();
    expect(
      screen.getByRole("button", { name: "Corrigir e-mail" }),
    ).toBeOnTheScreen();
    expect(
      screen.queryByRole("button", { name: "Trocar de curso" }),
    ).toBeNull();
    expect(
      screen.queryByRole("button", { name: "Marcar ex-aluno" }),
    ).toBeNull();
  });

  it("corrigir e-mail manda o novo e mostra a recusa dentro da folha", async () => {
    atos.mockResolvedValueOnce(
      json(409, { status: 409, codigo: "email-em-uso" }),
    );
    await renderComApi(<FichaDoAlunoScreen />, api);

    await apertar("Corrigir e-mail");
    const folha = await screen.findByTestId("folha-de-ato");
    await act(async () => {
      fireEvent.changeText(
        within(folha).getByLabelText("E-mail"),
        "lucas.f@al.unieduk.invalid",
      );
    });
    await act(async () => {
      fireEvent.press(
        within(folha).getByRole("button", { name: "Salvar e-mail" }),
      );
    });

    expect(new URL(atos.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/instituicao/alunos/cad-lucas/email",
    );
    expect(await corpo(atos)).toEqual({ email: "lucas.f@al.unieduk.invalid" });
    expect(
      await within(folha).findByText(
        "Esse e-mail já é de outra pessoa da base.",
      ),
    ).toBeOnTheScreen();
  });

  it("corrigir para domínio fora da lista mostra a recusa dentro da folha", async () => {
    atos.mockResolvedValueOnce(
      json(400, { status: 400, codigo: "dominio-nao-aceito" }),
    );
    await renderComApi(<FichaDoAlunoScreen />, api);

    await apertar("Corrigir e-mail");
    const folha = await screen.findByTestId("folha-de-ato");
    await act(async () => {
      fireEvent.changeText(
        within(folha).getByLabelText("E-mail"),
        "lucas@gmail.com",
      );
    });
    await act(async () => {
      fireEvent.press(
        within(folha).getByRole("button", { name: "Salvar e-mail" }),
      );
    });

    expect(
      await within(folha).findByText("Esse domínio de e-mail não é aceito."),
    ).toBeOnTheScreen();
  });

  it("trocar de curso pede o curso novo e o semestre nele", async () => {
    await renderComApi(<FichaDoAlunoScreen />, api);

    await apertar("Trocar de curso");
    const folha = await screen.findByTestId("folha-de-ato");
    expect(within(folha).queryByRole("radio", { name: "CC" })).toBeNull();
    await act(async () => {
      fireEvent.press(within(folha).getByRole("radio", { name: "ADM" }));
    });
    await act(async () => {
      fireEvent.press(within(folha).getByRole("radio", { name: "2º" }));
    });
    await act(async () => {
      fireEvent.press(
        within(folha).getByRole("button", { name: "Trocar de curso" }),
      );
    });

    expect(new URL(atos.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/instituicao/alunos/cad-lucas/curso",
    );
    expect(await corpo(atos)).toEqual({ cursoId: ADM, semestre: 2 });
  });

  it("marcar ex-aluno confirma na folha e manda o pedido", async () => {
    await renderComApi(<FichaDoAlunoScreen />, api);

    await apertar("Marcar ex-aluno");
    const folha = await screen.findByTestId("folha-de-ato");
    expect(within(folha).getByText(/Não dá para desfazer\./)).toBeOnTheScreen();
    await act(async () => {
      fireEvent.press(
        within(folha).getByRole("button", { name: "Marcar ex-aluno" }),
      );
    });

    expect(new URL(atos.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/instituicao/alunos/cad-lucas/ex-aluno",
    );
  });

  it("anular a medalha confirma na folha e manda o pedido da medalha", async () => {
    await renderComApi(<FichaDoAlunoScreen />, api);

    await apertar(/^Anular Nota 10 em prova/);
    const folha = await screen.findByTestId("folha-de-ato");
    await act(async () => {
      fireEvent.press(within(folha).getByRole("button", { name: "Anular" }));
    });

    expect(new URL(atos.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/instituicao/medalhas/m1/anulacao",
    );
  });
});

describe("conceder prêmio", () => {
  it("escolhe o prêmio e o evento do aluno, concede e confirma", async () => {
    await renderComApi(<ConcederPremioScreen />, api);

    expect(
      await screen.findByText(
        "O prêmio que a instituição concede, já aprovado.",
      ),
    ).toBeOnTheScreen();
    expect(
      screen.getByRole("button", { name: "Conceder prêmio" }),
    ).toBeDisabled();
    await apertar(/^Semana acadêmica/, "radio");
    await apertar(/^Semana Acadêmica de Ciência da Computação/, "radio");
    await apertar("Conceder prêmio");

    expect(new URL(concessao.mock.calls[0]![0].url).pathname).toBe(
      "/api/v1/instituicao/alunos/cad-lucas/premios-participacao",
    );
    expect(await corpo(concessao)).toEqual({
      item: "P06",
      eventoId: "semana",
      eventoDigitado: null,
      materiaId: null,
    });
    expect(await screen.findByText("Prêmio concedido.")).toBeOnTheScreen();
  });

  it("a recusa da API aparece na tela", async () => {
    concessao.mockResolvedValueOnce(
      json(409, { status: 409, codigo: "premio-ja-reivindicado" }),
    );
    await renderComApi(<ConcederPremioScreen />, api);

    await apertar(/^Semana acadêmica/, "radio");
    await apertar(/^Semana Acadêmica de Ciência da Computação/, "radio");
    await apertar("Conceder prêmio");

    expect(
      await screen.findByText("O aluno já tem esse prêmio nesse evento."),
    ).toBeOnTheScreen();
    expect(screen.queryByText("Prêmio concedido.")).toBeNull();
  });
});
