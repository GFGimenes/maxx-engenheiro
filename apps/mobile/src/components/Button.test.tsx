import { fireEvent, screen } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { renderWithTheme } from "../test/renderWithTheme";
import { cores } from "../theme/tokens";
import { Button } from "./Button";

const visual = () =>
  StyleSheet.flatten(screen.getByTestId("botao-visual").props.style);
const texto = (label: string) =>
  StyleSheet.flatten(screen.getByText(label).props.style);

// O dedo na tela, como o Pressable o recebe: sem o evento de resposta, ele não marca "pressed".
const toque = () => ({
  persist() {},
  nativeEvent: {
    locationX: 0,
    locationY: 0,
    pageX: 0,
    pageY: 0,
    timestamp: Date.now(),
    touches: [],
  },
});

test("discreto tem 32 de altura visual, raio 8, texto 13 e 44 de toque", async () => {
  await renderWithTheme(<Button label="Seguir" onPress={() => {}} />);

  expect(visual()).toMatchObject({ height: 32, borderRadius: 8 });
  expect(texto("Seguir").fontSize).toBe(13);
  expect(
    StyleSheet.flatten(
      screen.getByRole("button", { name: "Seguir" }).props.style,
    ).minHeight,
  ).toBe(44);
});

test("grande tem 50 de altura e raio 8", async () => {
  await renderWithTheme(
    <Button label="Entrar" size="large" onPress={() => {}} />,
  );

  expect(visual()).toMatchObject({ height: 50, borderRadius: 8 });
});

test("primário usa a ação do tema; secundário, a superfície", async () => {
  const { rerender } = await renderWithTheme(
    <Button label="Seguir" onPress={() => {}} />,
  );
  expect(visual().backgroundColor).toBe(cores.claro.primaryBg);
  expect(texto("Seguir").color).toBe(cores.claro.primaryFg);

  await rerender(
    <Button label="Seguir" variant="secondary" onPress={() => {}} />,
  );
  expect(visual().backgroundColor).toBe(cores.claro.surface);
  expect(texto("Seguir").color).toBe(cores.claro.ink);
});

test("tocar chama a ação", async () => {
  const onPress = jest.fn();
  await renderWithTheme(<Button label="Seguir" onPress={onPress} />);

  await fireEvent.press(screen.getByRole("button", { name: "Seguir" }));

  expect(onPress).toHaveBeenCalledTimes(1);
});

test("desativado fica em surface2 e ink3 e não responde", async () => {
  const onPress = jest.fn();
  await renderWithTheme(<Button label="Seguir" onPress={onPress} disabled />);

  expect(visual().backgroundColor).toBe(cores.claro.surface2);
  expect(texto("Seguir").color).toBe(cores.claro.ink3);

  await fireEvent.press(
    screen.getByRole("button", { name: "Seguir", disabled: true }),
  );
  expect(onPress).not.toHaveBeenCalled();
});

test("pressionado fica com opacidade 0,85", async () => {
  await renderWithTheme(<Button label="Seguir" onPress={() => {}} />);
  const botao = screen.getByRole("button", { name: "Seguir" });
  expect(visual().opacity).toBeUndefined();

  await fireEvent(botao, "responderGrant", toque());
  expect(visual().opacity).toBe(0.85);
});

test("em foco, ganha o anel do token com 2 px de afastamento, e perde ao sair", async () => {
  await renderWithTheme(<Button label="Seguir" onPress={() => {}} />);
  const botao = screen.getByRole("button", { name: "Seguir" });
  expect(visual().outlineWidth).toBeUndefined();

  await fireEvent(botao, "focus", {});
  expect(visual()).toMatchObject({
    outlineColor: cores.claro.focus,
    outlineWidth: 2,
    outlineOffset: 2,
  });

  await fireEvent(botao, "blur");
  expect(visual().outlineWidth).toBeUndefined();
});
