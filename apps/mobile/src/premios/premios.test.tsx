import AsyncStorage from "@react-native-async-storage/async-storage";
import { screen } from "@testing-library/react-native";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { StyleSheet } from "react-native";

import { renderWithTheme } from "../test/renderWithTheme";
import { THEME_STORAGE_KEY } from "../theme/ThemeProvider";
import { cores, tiers, type Raridade, type Tier } from "../theme/tokens";
import { FaixaPremio } from "./FaixaPremio";
import { larguraDoMedalhao, Medalhao } from "./Medalhao";
import { linhaDoGradiente, SeloPremio } from "./SeloPremio";
import { TrilhaFamilia, type ItemDaTrilha } from "./TrilhaFamilia";

// O desenho do SVG é nativo: aqui, cada elemento vira uma View com os mesmos props.
jest.mock("react-native-svg", () => {
  const { createElement } = jest.requireActual("react");
  const { View } = jest.requireActual("react-native");
  function elemento(tipo: string) {
    return function ElementoSvg(props: object) {
      return createElement(View, { testID: tipo, ...props });
    };
  }
  return {
    __esModule: true,
    default: elemento("svg"),
    Defs: elemento("defs"),
    RadialGradient: elemento("radial"),
    LinearGradient: elemento("linear"),
    Stop: elemento("stop"),
    Path: elemento("path"),
    Circle: elemento("circle"),
    Line: elemento("line"),
    Rect: elemento("rect"),
  };
});

const TIERS: Tier[] = ["bronze", "prata", "ouro", "diamante"];
const OCULTOS = { includeHiddenElements: true };
const conta = (id: string) => screen.queryAllByTestId(id, OCULTOS).length;
function props(id: string, indice = 0) {
  const elemento = screen.getAllByTestId(id, OCULTOS)[indice];
  if (!elemento) throw new Error(`sem ${id}[${indice}]`);
  return elemento.props;
}

beforeEach(() => AsyncStorage.clear());

describe("Medalhao", () => {
  test.each(TIERS)(
    "%s usa o gradiente radial e a borda do metal",
    async (tier) => {
      await renderWithTheme(<Medalhao tier={tier} glifo="taca" />);

      expect(props("radial")).toMatchObject({ cx: "50%", cy: "30%" });
      expect(
        screen.getAllByTestId("stop", OCULTOS).map((s) => s.props.stopColor),
      ).toEqual(tiers[tier].medalhao);
      expect(props("metal").stroke).toBe(tiers[tier].borda);
      expect(props("metal").fill).toBe(`url(#${props("radial").id})`);
    },
  );

  test.each<[Raridade | undefined, number, number]>([
    [undefined, 0, 0],
    ["comum", 0, 0],
    ["raro", 1, 0],
    ["epico", 2, 0],
    ["lendario", 1, 32],
  ])(
    "a raridade %s tem %i anéis e %i raios",
    async (raridade, aneis, raios) => {
      await renderWithTheme(
        <Medalhao tier="ouro" glifo="taca" raridade={raridade} />,
      );

      expect(conta("anel")).toBe(aneis);
      expect(conta("raio")).toBe(raios);
    },
  );

  test("os anéis ficam a 3 e 8 px da borda no medalhão de 48, e escalam", async () => {
    await renderWithTheme(
      <Medalhao tier="ouro" glifo="taca" raridade="epico" />,
    );
    expect(props("anel", 0).r).toBe(24 + 3);
    expect(props("anel", 1).r).toBe(24 + 8);
    expect(props("anel", 0).strokeWidth).toBe(1.5);

    await renderWithTheme(
      <Medalhao tier="ouro" glifo="taca" raridade="epico" size={96} />,
    );
    expect(props("anel", 0).r).toBe(48 + 6);
  });

  test("os raios ficam centrados a 10,5 px da borda", async () => {
    await renderWithTheme(
      <Medalhao tier="ouro" glifo="taca" raridade="lendario" />,
    );

    const { x1, x2 } = props("raio");
    const centro = larguraDoMedalhao(48, true) / 2;
    expect(x1 - centro - 24).toBeCloseTo(9.5);
    expect(x2 - centro - 24).toBeCloseTo(11.5);
  });

  test("o anel usa rarityRing, que fica claro no escuro, e nunca a cor do tier", async () => {
    await renderWithTheme(
      <Medalhao tier="ouro" glifo="taca" raridade="raro" />,
    );
    expect(props("anel").stroke).toBe(cores.claro.rarityRing);

    await AsyncStorage.setItem(THEME_STORAGE_KEY, "escuro");
    await renderWithTheme(
      <Medalhao tier="ouro" glifo="taca" raridade="raro" />,
    );
    expect(props("anel").stroke).toBe(cores.escuro.rarityRing);
  });

  test.each([
    ["taca", "icone-taca", "Troféu Ouro, Épico"],
    ["fita", "icone-fita", "Medalha Ouro, Épico"],
  ] as const)(
    "o glifo %s tem desenho e nome próprios",
    async (glifo, id, nome) => {
      await renderWithTheme(
        <Medalhao tier="ouro" glifo={glifo} raridade="epico" />,
      );

      expect(screen.getByRole("img", { name: nome })).toBeTruthy();
      expect(screen.getByTestId(id, OCULTOS)).toBeTruthy();
      expect(props("path").stroke).toBe(tiers.ouro.glifo);
    },
  );

  test("com raridade, reserva a folga do lendário; sem raridade, não", () => {
    expect(larguraDoMedalhao(48, true)).toBe(74);
    expect(larguraDoMedalhao(48, false)).toBe(48);
  });

  test("dois medalhões na tela não dividem o id do gradiente", async () => {
    await renderWithTheme(
      <>
        <Medalhao tier="ouro" glifo="taca" />
        <Medalhao tier="prata" glifo="taca" />
      </>,
    );

    expect(props("radial", 0).id).not.toBe(props("radial", 1).id);
  });
});

describe("SeloPremio", () => {
  test("mostra o nome na cor do tier e um medalhão sem ornamento", async () => {
    await renderWithTheme(
      <SeloPremio nome="Techday 2026" tier="ouro" glifo="taca" />,
    );

    expect(
      StyleSheet.flatten(screen.getByText("Techday 2026").props.style).color,
    ).toBe(tiers.ouro.pillTexto);
    expect(conta("anel")).toBe(0);
    expect(
      StyleSheet.flatten(screen.getByTestId("selo").props.style).borderRadius,
    ).toBe(999);
  });

  test("a linha do gradiente de 105° na pílula segue o CSS", () => {
    const { x1, y1, x2, y2 } = linhaDoGradiente(105, 200, 26);
    const meio =
      (200 * Math.sin((105 * Math.PI) / 180) +
        26 * Math.cos((75 * Math.PI) / 180)) /
      2;

    expect((x1 + x2) / 2).toBeCloseTo(100);
    expect((y1 + y2) / 2).toBeCloseTo(13);
    expect(Math.hypot(x2 - x1, y2 - y1)).toBeCloseTo(2 * meio);
    expect(x2).toBeGreaterThan(x1);
    expect(y2).toBeGreaterThan(y1);
  });
});

describe("FaixaPremio", () => {
  const faixa = (
    <FaixaPremio
      nome="Pássaro Raro no Campus"
      tier="diamante"
      raridade="lendario"
      glifo="taca"
      pontos={150}
      nota="foto validada"
      rotulo="Último troféu"
    />
  );

  test("traz nome, tier e raridade em texto, pontos e nota", async () => {
    await renderWithTheme(faixa);

    expect(screen.getByText("Pássaro Raro no Campus")).toBeTruthy();
    expect(screen.getByText("Diamante")).toBeTruthy();
    expect(screen.getByText(/· Lendário/)).toBeTruthy();
    expect(screen.getByText("+150")).toBeTruthy();
    expect(screen.getByText("foto validada")).toBeTruthy();
    expect(screen.getByText("Último troféu")).toBeTruthy();
  });

  test.each(["claro", "escuro"] as const)(
    "no %s, o nome do tier usa a cor de texto do token",
    async (tema) => {
      await AsyncStorage.setItem(THEME_STORAGE_KEY, tema);
      await renderWithTheme(faixa);

      expect(
        StyleSheet.flatten(screen.getByText("Diamante").props.style).color,
      ).toBe(tiers.diamante.texto[tema]);
    },
  );

  test("o texto do tier Ouro no claro não é ouro de marca", async () => {
    await renderWithTheme(
      <FaixaPremio
        nome="Techday"
        tier="ouro"
        raridade="raro"
        glifo="taca"
        pontos={80}
      />,
    );

    expect(
      StyleSheet.flatten(screen.getByText("Ouro").props.style).color,
    ).not.toBe(cores.claro.gold);
  });
});

describe("TrilhaFamilia", () => {
  const item = (id: string, tier: Tier, obtido: boolean): ItemDaTrilha => ({
    id,
    rotulo: id,
    tier,
    glifo: "taca",
    obtido,
  });
  const CINCO = [
    item("Um", "bronze", true),
    item("Dois", "prata", true),
    item("Três", "ouro", false),
    item("Quatro", "ouro", false),
    item("Cinco", "diamante", false),
  ];

  test.each([2, 3, 4, 5])(
    "mostra %i passos, em rolagem horizontal",
    async (n) => {
      await renderWithTheme(
        <TrilhaFamilia titulo="Família" itens={CINCO.slice(0, n)} />,
      );

      expect(conta("passo")).toBe(n);
      expect(screen.getByText("Família")).toBeTruthy();
    },
  );

  test("só o próximo tem cadeado; os demais não obtidos ficam só tracejados", async () => {
    await renderWithTheme(<TrilhaFamilia titulo="Família" itens={CINCO} />);

    expect(conta("metal")).toBe(2);
    expect(conta("tracejado")).toBe(3);
    expect(conta("icone-lock")).toBe(1);
    expect(
      screen.getByRole("img", { name: "Próximo, bloqueado" }),
    ).toBeTruthy();
    expect(screen.getAllByRole("img", { name: "Bloqueado" })).toHaveLength(2);
  });

  test("o círculo não obtido tem traço de 2 px em ink2, sem preenchimento", async () => {
    await renderWithTheme(<TrilhaFamilia titulo="Família" itens={CINCO} />);

    expect(props("tracejado")).toMatchObject({
      stroke: cores.claro.ink2,
      strokeWidth: 2,
      fill: "none",
    });
    expect(props("tracejado").strokeDasharray).toBeTruthy();
  });

  test("linha contínua só entre dois obtidos; o resto é tracejado", async () => {
    await renderWithTheme(<TrilhaFamilia titulo="Família" itens={CINCO} />);

    // Cada ligação tem duas metades, uma em cada passo.
    expect(conta("ligacao-continua")).toBe(2);
    expect(conta("ligacao-tracejada")).toBe(6);
  });

  test("tudo obtido não tem cadeado nem tracejado", async () => {
    await renderWithTheme(
      <TrilhaFamilia
        titulo="Família"
        itens={[item("Um", "bronze", true), item("Dois", "prata", true)]}
      />,
    );

    expect(conta("icone-lock")).toBe(0);
    expect(conta("tracejado")).toBe(0);
    expect(conta("ligacao-tracejada")).toBe(0);
  });

  test("mostra o resumo e os detalhes de cada passo", async () => {
    await renderWithTheme(
      <TrilhaFamilia
        titulo="Média 10"
        resumo="1 de 2 · 5 pts"
        itens={[
          { ...item("Um", "bronze", true), detalhes: ["+5 pts", "1º sem."] },
          item("Dois", "prata", false),
        ]}
      />,
    );

    expect(screen.getByText("1 de 2 · 5 pts")).toBeTruthy();
    expect(screen.getByText("+5 pts")).toBeTruthy();
    expect(screen.getByText("1º sem.")).toBeTruthy();
  });
});

describe("regras da interface no código", () => {
  function arquivos(pasta: string): string[] {
    return readdirSync(pasta).flatMap((nome) => {
      const caminho = join(pasta, nome);
      return statSync(caminho).isDirectory() ? arquivos(caminho) : [caminho];
    });
  }
  const fontes = arquivos(join(__dirname, "..")).filter(
    (f) => /\.tsx?$/.test(f) && !/\.test\.tsx?$/.test(f),
  );
  const usam = (padrao: RegExp) =>
    fontes
      .filter((f) => padrao.test(readFileSync(f, "utf8")))
      .map((f) => f.replace(/^.*[\\/]/, ""))
      .sort();

  test("gradiente só no metal dos prêmios", () => {
    expect(usam(/(Radial|Linear)Gradient/)).toEqual([
      "Medalhao.tsx",
      "SeloPremio.tsx",
    ]);
  });

  test("sombra só no controle segmentado", () => {
    expect(usam(/boxShadow|shadowColor|elevation:/)).toEqual([
      "SegmentedControl.tsx",
    ]);
  });
});
