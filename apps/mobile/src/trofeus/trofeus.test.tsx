import { act, fireEvent, screen, within } from "@testing-library/react-native";

import { abaDaRota } from "../navegacao/LayoutDasAbas";
import { json, renderComApi } from "../test/renderComApi";
import {
  detalheDaLinha,
  itensDaAba,
  rotuloDaInstancia,
  textoDaRaridade,
  textoDoProgresso,
  trilhasDaAba,
  type Instancia,
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
    exigeMateria: false,
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

const conquistaEm = (instancia: Instancia) => ({
  id: `c-${instancia.periodo}-${instancia.bimestre ?? 0}`,
  tier: "bronze",
  pontos: 25,
  materia: null,
  bimestre: null,
  obtidoEm: "2026-11-10T13:00:00Z",
  postId: null,
  instancia,
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

  it("a conquista que falta diz que é regra automática e o critério", () => {
    expect(detalheDaLinha(PREMIOS.itens[3]!)).toBe(
      "Regra automática · Critério de C05.",
    );
  });

  it("a conquista obtida diz a instância da mais recente e quantas vezes", () => {
    const c01 = item({
      codigo: "C01",
      tipo: "conquista",
      obtidos: [
        conquistaEm({
          periodo: "bimestre",
          ano: 2026,
          semestre: 2,
          bimestre: 4,
        }),
        conquistaEm({
          periodo: "bimestre",
          ano: 2026,
          semestre: 2,
          bimestre: 3,
        }),
      ],
    });

    expect(detalheDaLinha(c01)).toBe(
      "Regra automática · 4º bimestre de 2026 · 2 vezes",
    );
  });

  it("o rótulo da instância por período", () => {
    expect(
      rotuloDaInstancia({
        periodo: "semestre",
        ano: 2026,
        semestre: 2,
        bimestre: null,
      }),
    ).toBe("2026/2");
    expect(
      rotuloDaInstancia({
        periodo: "ano",
        ano: 2026,
        semestre: null,
        bimestre: null,
      }),
    ).toBe("2026");
    expect(
      rotuloDaInstancia({
        periodo: "curso",
        ano: null,
        semestre: null,
        bimestre: null,
      }),
    ).toBe("no curso");
  });

  it("o progresso diz quanto falta na instância vigente", () => {
    const comProgresso = (periodo: string, contagem: number, alvo: number) =>
      textoDoProgresso(
        item({
          codigo: "C",
          tipo: "conquista",
          progresso: { periodo, contagem, alvo },
        }),
      );

    expect(comProgresso("bimestre", 2, 3)).toBe("2 de 3 neste bimestre");
    expect(comProgresso("semestre", 0, 10)).toBe("0 de 10 neste semestre");
    expect(comProgresso("ano", 5, 20)).toBe("5 de 20 neste ano");
    expect(comProgresso("curso", 48, 1000)).toBe("48 de 1.000 no curso");
    expect(
      textoDoProgresso(
        item({
          codigo: "C03",
          tipo: "conquista",
          progresso: {
            periodo: "bimestre",
            contagem: 3,
            alvo: 4,
            unidade: "materias",
          },
        }),
      ),
    ).toBe("3 de 4 matérias neste bimestre");
    expect(textoDoProgresso(PREMIOS.itens[3]!)).toBeNull();
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
    expect(
      within(c05).getByText("Regra automática · Critério de C05."),
    ).toBeOnTheScreen();
    expect(within(c05).getByText("110")).toBeOnTheScreen();
    // Sem raridade calculada ainda, só o tier.
    expect(
      within(screen.getByTestId("premio-P01")).queryByText(/dos alunos/),
    ).toBeNull();
  });

  it("a conquista obtida mostra a instância, e a que falta, a linha de progresso", async () => {
    premios.mockResolvedValueOnce(
      json(200, {
        ...PREMIOS,
        itens: [
          ...PREMIOS.itens.map((i) =>
            i.codigo === "C02"
              ? {
                  ...i,
                  progresso: { periodo: "bimestre", contagem: 2, alvo: 3 },
                }
              : i,
          ),
          item({
            codigo: "C01",
            tipo: "conquista",
            nome: "Cinco no bimestre",
            pontos: 25,
            obtidos: [
              conquistaEm({
                periodo: "bimestre",
                ano: 2026,
                semestre: 2,
                bimestre: 4,
              }),
            ],
          }),
        ],
      }),
    );
    await renderComApi(<TrofeusScreen />, api);

    const c01 = await screen.findByTestId("premio-C01");
    expect(
      within(c01).getByText("Regra automática · 4º bimestre de 2026"),
    ).toBeOnTheScreen();
    expect(within(c01).getByText("+25")).toBeOnTheScreen();
    expect(within(c01).queryByTestId("progresso-C01")).toBeNull();

    const c02 = screen.getByTestId("premio-C02");
    expect(
      within(c02).getByText("Regra automática · Critério de C02."),
    ).toBeOnTheScreen();
    expect(within(c02).getByText("2 de 3 neste bimestre")).toBeOnTheScreen();
    expect(c02).toHaveProp(
      "accessibilityLabel",
      "Três notas 10, Prata, Lendário, não obtido, 2 de 3 neste bimestre",
    );
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
