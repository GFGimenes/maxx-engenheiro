import { act, fireEvent, screen, within } from "@testing-library/react-native";

import { abaDaRota } from "../navegacao/LayoutDasAbas";
import { json, renderComApi } from "../test/renderComApi";
import {
  detalheDaLinha,
  itensDaAba,
  textoDaRaridade,
  trilhasDaAba,
  type ItemDoAluno,
  type PremiosDoAluno,
} from "./montagem";
import { TrofeusScreen } from "./TrofeusScreen";

jest.mock("expo-router", () => ({
  useRouter: () => ({ push: jest.fn(), navigate: jest.fn() }),
}));

function item(
  parcial: Partial<ItemDoAluno> & Pick<ItemDoAluno, "codigo">,
): ItemDoAluno {
  return {
    tipo: "medalha",
    nome: parcial.codigo,
    criterio: `Critério de ${parcial.codigo}.`,
    tier: "bronze",
    pontos: 10,
    familia: null,
    ordemNaFamilia: null,
    raridade: { nivel: "lendario", percentual: 0 },
    obtidos: [],
    ...parcial,
  };
}

const obtido = (pontos: number, materia = "Banco de Dados", bimestre = 4) => ({
  id: `o-${pontos}-${bimestre}`,
  tier: "bronze",
  pontos,
  materia,
  bimestre,
  obtidoEm: "2026-11-10T13:00:00Z",
  postId: null,
});

const PREMIOS: PremiosDoAluno = {
  pontos: 15020,
  familias: [
    {
      codigo: "nota-prova",
      nome: "Nota 10 em prova",
      descricao: "",
      passos: 3,
    },
    {
      codigo: "participacao",
      nome: "Vida acadêmica",
      descricao: "",
      passos: 2,
    },
  ],
  itens: [
    item({
      codigo: "M01",
      nome: "Nota 10 em prova",
      pontos: 30,
      familia: "nota-prova",
      ordemNaFamilia: 1,
      raridade: { nivel: "comum", percentual: 50 },
      obtidos: [obtido(30), obtido(30, "Redes", 3)],
    }),
    item({ codigo: "M03", nome: "Presença integral", pontos: 25 }),
    item({
      codigo: "C02",
      tipo: "conquista",
      nome: "Três notas 10",
      tier: "prata",
      pontos: 40,
      familia: "nota-prova",
      ordemNaFamilia: 2,
    }),
    item({
      codigo: "C05",
      tipo: "conquista",
      nome: "Nota 10 em tudo",
      tier: "diamante",
      pontos: 110,
      familia: "nota-prova",
      ordemNaFamilia: 3,
      raridade: { nivel: "lendario", percentual: 0.4 },
    }),
    item({
      codigo: "P01",
      tipo: "premioParticipacao",
      nome: "Palestra",
      tier: "bronze",
      pontos: 15,
      familia: "participacao",
      ordemNaFamilia: 1,
      raridade: null,
    }),
  ],
};

describe("montagem da tela", () => {
  it("separa as abas: medalha numa, prêmio de participação e conquista na outra", () => {
    expect(
      itensDaAba(PREMIOS.itens, "medalhas", true).map((i) => i.codigo),
    ).toEqual(["M01", "M03"]);
    expect(
      itensDaAba(PREMIOS.itens, "trofeus", true).map((i) => i.codigo),
    ).toEqual(["C05", "C02", "P01"]);
  });

  it("sem mostrar não obtidos, só fica o que o aluno tem", () => {
    expect(
      itensDaAba(PREMIOS.itens, "medalhas", false).map((i) => i.codigo),
    ).toEqual(["M01"]);
    expect(itensDaAba(PREMIOS.itens, "trofeus", false)).toEqual([]);
  });

  it("a trilha só aparece na família em que o aluno já tem algo, com o próximo marcado", () => {
    const [trilha, ...outras] = trilhasDaAba(PREMIOS, "medalhas");

    expect(outras).toEqual([]);
    expect(trilha!.titulo).toBe("Nota 10 em prova");
    expect(trilha!.resumo).toBe("1 de 3 · 60 pts");
    expect(
      trilha!.passos.map((p) => [
        p.id,
        p.rotulo,
        p.obtido,
        p.glifo,
        p.detalhes,
      ]),
    ).toEqual([
      ["M01", "Bronze", true, "fita", ["+60 pts"]],
      ["C02", "Prata", false, "taca", ["40 pts", "próximo"]],
      ["C05", "Diamante", false, "taca", ["110 pts"]],
    ]);
  });

  it("a medalha obtida diz onde veio a mais recente e quantas vezes", () => {
    expect(detalheDaLinha(PREMIOS.itens[0]!)).toBe(
      "Banco de Dados · 4º bimestre · 2 vezes",
    );
    expect(detalheDaLinha(PREMIOS.itens[1]!)).toBe("Critério de M03.");
  });

  it("raridade em texto, no formato brasileiro", () => {
    expect(textoDaRaridade(0.4)).toBe("0,4% dos alunos");
    expect(textoDaRaridade(50)).toBe("50% dos alunos");
  });
});

describe("abas da navegação", () => {
  it("cada rota acende a sua aba, e o início é a Home", () => {
    expect(abaDaRota("/")).toBe("home");
    expect(abaDaRota("/trofeus")).toBe("trofeus");
    expect(abaDaRota("/perfil")).toBe("perfil");
  });
});

describe("tela de Troféus", () => {
  let premios: jest.Mock<Promise<Response>, [Request]>;
  let papeis: string[];
  let api: jest.Mock<Promise<Response>, [Request]>;

  beforeEach(() => {
    papeis = ["aluno"];
    premios = jest.fn(async (_pedido: Request) => json(200, PREMIOS));
    api = jest.fn(async (pedido: Request) => {
      const caminho = new URL(pedido.url).pathname;
      if (caminho === "/api/v1/eu")
        return json(200, {
          id: "a",
          papeis,
          arroba: "@anaSouza-CC",
          nomeDeExibicao: "Ana",
        });
      if (caminho === "/api/v1/eu/premios") return premios(pedido);
      return new Response(null, { status: 404 });
    });
  });

  it("abre em Troféus, com os pontos, o critério e a raridade do que falta", async () => {
    await renderComApi(<TrofeusScreen />, api);

    expect(await screen.findByText("15.020 pts")).toBeOnTheScreen();
    expect(
      screen.getByRole("tab", { name: "Troféus", selected: true }),
    ).toBeOnTheScreen();
    const c05 = screen.getByTestId("premio-C05");
    expect(within(c05).getByText("Nota 10 em tudo")).toBeOnTheScreen();
    expect(
      within(c05).getByText(" · Lendário · 0,4% dos alunos"),
    ).toBeOnTheScreen();
    expect(within(c05).getByText("Critério de C05.")).toBeOnTheScreen();
    expect(within(c05).getByText("110")).toBeOnTheScreen();
    // Sem raridade calculada ainda, só o tier.
    expect(
      within(screen.getByTestId("premio-P01")).queryByText(/dos alunos/),
    ).toBeNull();
  });

  it("na aba Medalhas, a obtida vem com o valor ganho e a trilha da família", async () => {
    await renderComApi(<TrofeusScreen />, api);
    await act(async () => {
      fireEvent.press(await screen.findByRole("tab", { name: "Medalhas" }));
    });

    const m01 = screen.getByTestId("premio-M01");
    expect(within(m01).getByText("+60")).toBeOnTheScreen();
    expect(
      within(m01).getByText("Banco de Dados · 4º bimestre · 2 vezes"),
    ).toBeOnTheScreen();
    expect(screen.getByText("1 de 3 · 60 pts")).toBeOnTheScreen();
  });

  it("desligar Mostrar não obtidos deixa só o que o aluno tem, e explica a lista vazia", async () => {
    await renderComApi(<TrofeusScreen />, api);
    const alternar = await screen.findByLabelText("Mostrar não obtidos");

    await act(async () => {
      fireEvent(alternar, "valueChange", false);
    });

    expect(screen.queryByTestId("premio-C05")).toBeNull();
    expect(screen.getByText("Nenhum troféu ainda.")).toBeOnTheScreen();
  });

  it("falha vira mensagem amigável com tentar de novo", async () => {
    premios.mockRejectedValueOnce(new TypeError("Network request failed"));
    await renderComApi(<TrofeusScreen />, api);

    expect(
      await screen.findByText(
        "Sem conexão. Confira a internet e tente de novo.",
      ),
    ).toBeOnTheScreen();
    await act(async () => {
      fireEvent.press(screen.getByRole("button", { name: "Tentar de novo" }));
    });
    expect(await screen.findByText("15.020 pts")).toBeOnTheScreen();
  });

  it("para quem não é aluno, explica em vez de pedir os prêmios", async () => {
    papeis = ["professor"];
    await renderComApi(<TrofeusScreen />, api);

    expect(
      await screen.findByText("Troféus e medalhas são dos alunos."),
    ).toBeOnTheScreen();
    expect(premios).not.toHaveBeenCalled();
  });
});
