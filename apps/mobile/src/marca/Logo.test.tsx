import AsyncStorage from "@react-native-async-storage/async-storage";
import { screen } from "@testing-library/react-native";
import { readFileSync } from "fs";
import { join } from "path";
import { StyleSheet } from "react-native";

import { renderWithTheme } from "../test/renderWithTheme";
import { THEME_STORAGE_KEY } from "../theme/ThemeProvider";
import { cores } from "../theme/tokens";
import { ALTURA_MINIMA_DO_LOGO, Logo } from "./Logo";
import { logoVetor } from "./logoVetor";

// O desenho do SVG é nativo: aqui, cada elemento vira uma View com os mesmos props, como
// no teste do Icon.
jest.mock("react-native-svg", () => {
  const { createElement } = jest.requireActual("react");
  const { View } = jest.requireActual("react-native");
  function elemento(tipo: string) {
    return function ElementoSvg(props: object) {
      return createElement(View, { testID: tipo, ...props });
    };
  }
  return { __esModule: true, default: elemento("svg"), Path: elemento("path") };
});

beforeEach(() => AsyncStorage.clear());

const logo = () => StyleSheet.flatten(screen.getByTestId("logo").props.style);

test("o traçado é o do SVG do design: trocar o SVG sem gerar de novo falha aqui", () => {
  const svg = readFileSync(
    join(__dirname, "../../../../design/assets/maxx-ouro.svg"),
    "utf8",
  );

  expect(svg).toContain(
    `viewBox="0 0 ${logoVetor.largura} ${logoVetor.altura}"`,
  );
  expect(svg).toContain(`d="${logoVetor.caminho}"`);
});

test("guarda a proporção do desenho e se anuncia como Maxx", async () => {
  await renderWithTheme(<Logo largura={245} />);

  expect(screen.getByRole("img", { name: "Maxx" })).toBeTruthy();
  const { width, height } = logo();
  expect(Number(width) / Number(height)).toBeCloseTo(
    logoVetor.largura / logoVetor.altura,
    1,
  );
  expect(Number(width)).toBeCloseTo(245, -1);
});

test("nunca fica abaixo da altura mínima da identidade", async () => {
  await renderWithTheme(<Logo largura={40} />);

  expect(Number(logo().height)).toBe(ALTURA_MINIMA_DO_LOGO);
});

test.each(["claro", "escuro"] as const)(
  "sai no ouro do tema %s",
  async (tema) => {
    await AsyncStorage.setItem(THEME_STORAGE_KEY, tema);
    await renderWithTheme(<Logo largura={245} />);

    expect(
      screen.getByTestId("logo-tracado", { includeHiddenElements: true }).props
        .fill,
    ).toBe(cores[tema].gold);
  },
);
