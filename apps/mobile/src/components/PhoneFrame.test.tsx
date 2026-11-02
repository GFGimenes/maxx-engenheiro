import { render, screen } from "@testing-library/react-native";
import { Platform, StyleSheet, Text } from "react-native";

import { PhoneFrame } from "./PhoneFrame";

// O `react-native` exporta o hook por getter, então o mock vai no módulo de origem.
const mockWindow = { width: 0, height: 0 };
jest.mock("react-native/Libraries/Utilities/useWindowDimensions", () => ({
  __esModule: true,
  default: () => ({ ...mockWindow, scale: 1, fontScale: 1 }),
}));

function renderAt(os: typeof Platform.OS, width: number, height: number) {
  jest.replaceProperty(Platform, "OS", os);
  Object.assign(mockWindow, { width, height });
  return render(
    <PhoneFrame>
      <Text>conteúdo</Text>
    </PhoneFrame>,
  );
}

afterEach(() => jest.restoreAllMocks());

test("na web a partir de 600 px, mostra a coluna de 390 px com teto de 844 px", async () => {
  await renderAt("web", 1280, 1000);

  const frame = StyleSheet.flatten(screen.getByTestId("moldura").props.style);
  expect(frame.width).toBe(390);
  expect(frame.height).toBe(844);
  expect(frame.borderWidth).toBe(1);
  expect(screen.getByText("conteúdo")).toBeTruthy();
});

test("com a janela mais baixa que 844 px, a moldura acompanha a altura da janela", async () => {
  await renderAt("web", 1280, 700);

  expect(
    StyleSheet.flatten(screen.getByTestId("moldura").props.style).height,
  ).toBe(700);
});

test("na web abaixo de 600 px, o app ocupa a largura toda, sem moldura", async () => {
  await renderAt("web", 599, 900);

  expect(screen.queryByTestId("moldura")).toBeNull();
  expect(screen.getByText("conteúdo")).toBeTruthy();
});

test("no nativo nunca há moldura, mesmo em tela larga", async () => {
  await renderAt("ios", 1024, 1366);

  expect(screen.queryByTestId("moldura")).toBeNull();
});
