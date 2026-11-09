import { act, fireEvent, screen } from "@testing-library/react-native";

import { json, renderComApi } from "../test/renderComApi";
import { BuscaScreen } from "./BuscaScreen";

const mockRoteador = {
  push: jest.fn(),
  replace: jest.fn(),
  back: jest.fn(),
  canGoBack: jest.fn(() => true),
};
jest.mock("expo-router", () => ({ useRouter: () => mockRoteador }));

const ANA = "0193a000-0000-7000-8000-0000000000b1";
const JOAO = "0193a000-0000-7000-8000-0000000000b2";

const JOAO_ENCONTRADO = {
  id: JOAO,
  nomeDeExibicao: "João Lima",
  arroba: "@joaoLima-CC",
  curso: "CC",
  semestre: 5,
  exAluno: false,
  foto: null,
  euSigo: true,
};

let busca: jest.Mock<Promise<Response>, [Request]>;
let papeis: string[];
let exAluno: boolean;
let api: jest.Mock<Promise<Response>, [Request]>;

beforeEach(() => {
  jest.clearAllMocks();
  papeis = ["aluno"];
  exAluno = false;
  busca = jest.fn(async (_pedido: Request) =>
    json(200, { itens: [JOAO_ENCONTRADO], proximo: null }),
  );
  api = jest.fn(async (pedido: Request) => {
    const { pathname } = new URL(pedido.url);
    if (pathname === "/api/v1/eu")
      return json(200, {
        id: ANA,
        papeis,
        arroba: "@anaSouza-CC",
        nomeDeExibicao: "Ana Souza",
        foto: null,
      });
    if (pathname === "/api/v1/cursos")
      return json(200, [
        {
          sigla: "ADS",
          nome: "Análise e Desenvolvimento de Sistemas",
          duracaoEmSemestres: 6,
        },
        { sigla: "CC", nome: "Ciência da Computação", duracaoEmSemestres: 8 },
        { sigla: "MED", nome: "Medicina", duracaoEmSemestres: 12 },
      ]);
    if (pathname === "/api/v1/busca/alunos") return busca(pedido);
    if (pathname === "/api/v1/eu/perfil")
      return json(200, { id: ANA, exAluno });
    return new Response(null, { status: 404 });
  });
});

const consulta = (chamada = 0) =>
  new URL(busca.mock.calls[chamada]![0].url).searchParams;

async function digitar(texto: string) {
  await act(async () => {
    fireEvent.changeText(await screen.findByLabelText("Buscar"), texto);
  });
}

describe("busca", () => {
  it("sem termo nem filtro, explica e não pergunta à API", async () => {
    await renderComApi(<BuscaScreen />, api);

    expect(await screen.findByText("Busque alunos.")).toBeOnTheScreen();
    await digitar("a");
    expect(screen.getByText("Busque alunos.")).toBeOnTheScreen();
    expect(busca).not.toHaveBeenCalled();
  });

  it("procura pelo termo depois que a digitação para e abre o perfil de quem aparece", async () => {
    await renderComApi(<BuscaScreen />, api);

    await digitar("joa");

    expect(await screen.findByText("João Lima")).toBeOnTheScreen();
    expect(
      screen.getByText("@joaoLima-CC · CC · 5º semestre"),
    ).toBeOnTheScreen();
    expect(screen.getByText("Seguindo")).toBeOnTheScreen();
    expect(busca).toHaveBeenCalledTimes(1);
    expect(consulta().get("termo")).toBe("joa");

    await act(async () => {
      fireEvent.press(screen.getByTestId(`aluno-${JOAO}`));
    });
    expect(mockRoteador.push).toHaveBeenCalledWith(`/usuarios/${JOAO}`);
  });

  it("filtra por curso e por semestre sem termo, e tocar de novo tira o filtro", async () => {
    await renderComApi(<BuscaScreen />, api);

    await act(async () => {
      fireEvent.press(await screen.findByRole("radio", { name: "CC" }));
    });
    await screen.findByText("João Lima");
    expect(consulta().get("curso")).toBe("CC");
    expect(consulta().get("termo")).toBeNull();

    await act(async () => {
      fireEvent.press(screen.getByRole("radio", { name: "5º semestre" }));
    });
    await act(async () => {});
    expect(consulta(busca.mock.calls.length - 1).get("semestre")).toBe("5");

    await act(async () => {
      fireEvent.press(screen.getByRole("radio", { name: "CC" }));
      fireEvent.press(screen.getByRole("radio", { name: "5º semestre" }));
    });
    expect(screen.getByText("Busque alunos.")).toBeOnTheScreen();
  });

  it("os semestres seguem o curso: sem curso até o 12º de Medicina, com CC até o 8º", async () => {
    await renderComApi(<BuscaScreen />, api);

    expect(
      await screen.findByRole("radio", { name: "12º semestre" }),
    ).toBeOnTheScreen();
    expect(screen.queryByRole("radio", { name: "13º semestre" })).toBeNull();

    await act(async () => {
      fireEvent.press(screen.getByRole("radio", { name: "CC" }));
    });
    expect(
      screen.getByRole("radio", { name: "8º semestre" }),
    ).toBeOnTheScreen();
    expect(screen.queryByRole("radio", { name: "9º semestre" })).toBeNull();

    await act(async () => {
      fireEvent.press(screen.getByRole("radio", { name: "MED" }));
    });
    await act(async () => {
      fireEvent.press(screen.getByRole("radio", { name: "12º semestre" }));
    });
    await screen.findByText("João Lima");
    const ultima = consulta(busca.mock.calls.length - 1);
    expect([ultima.get("curso"), ultima.get("semestre")]).toEqual([
      "MED",
      "12",
    ]);

    // Trocar para um curso mais curto tira o semestre que ele não tem.
    await act(async () => {
      fireEvent.press(screen.getByRole("radio", { name: "CC" }));
    });
    await act(async () => {});
    const depois = consulta(busca.mock.calls.length - 1);
    expect([depois.get("curso"), depois.get("semestre")]).toEqual(["CC", null]);
  });

  it("sem resultado, diz isso", async () => {
    busca.mockResolvedValue(json(200, { itens: [], proximo: null }));
    await renderComApi(<BuscaScreen />, api);

    await digitar("ninguem");

    expect(
      await screen.findByText("Nenhum aluno encontrado."),
    ).toBeOnTheScreen();
  });

  it("falha vira mensagem amigável", async () => {
    busca.mockRejectedValue(new TypeError("Network request failed"));
    await renderComApi(<BuscaScreen />, api);

    await digitar("joa");

    expect(await screen.findByText(/Sem conexão/)).toBeOnTheScreen();
  });
});

describe("atalhos dos rankings", () => {
  it("o aluno tem o ranking geral e o do curso", async () => {
    await renderComApi(<BuscaScreen />, api);

    await act(async () => {
      fireEvent.press(
        await screen.findByRole("button", { name: "Ranking geral" }),
      );
    });
    expect(mockRoteador.push).toHaveBeenLastCalledWith("/rankings?visao=geral");

    await act(async () => {
      fireEvent.press(screen.getByRole("button", { name: "Ranking do curso" }));
    });
    expect(mockRoteador.push).toHaveBeenLastCalledWith("/rankings?visao=curso");
  });

  it("o ex-aluno vai ao histórico do curso", async () => {
    exAluno = true;
    await renderComApi(<BuscaScreen />, api);
    await act(async () => {});

    await act(async () => {
      fireEvent.press(
        await screen.findByRole("button", { name: "Ranking do curso" }),
      );
    });
    expect(mockRoteador.push).toHaveBeenLastCalledWith(
      "/rankings?visao=historico-curso",
    );
  });

  it("o professor tem só o ranking geral", async () => {
    papeis = ["professor"];
    await renderComApi(<BuscaScreen />, api);

    expect(
      await screen.findByRole("button", { name: "Ranking geral" }),
    ).toBeOnTheScreen();
    expect(
      screen.queryByRole("button", { name: "Ranking do curso" }),
    ).toBeNull();
  });
});
