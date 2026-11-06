import { fireEvent, screen } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { renderWithTheme } from "../test/renderWithTheme";
import { cores } from "../theme/tokens";
import { FilterChip } from "./FilterChip";

const visual = () =>
  StyleSheet.flatten(screen.getByTestId("chip-visual").props.style);
const texto = () => StyleSheet.flatten(screen.getByText("Curso").props.style);

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

test("tem raio 8 e 44 de toque", async () => {
  await renderWithTheme(
    <FilterChip label="Curso" selected={false} onPress={() => {}} />,
  );

  expect(visual().borderRadius).toBe(8);
  expect(
    StyleSheet.flatten(screen.getByRole("checkbox").props.style).minHeight,
  ).toBe(44);
});

test("ativo fica em primaryBg com texto primaryFg", async () => {
  await renderWithTheme(
    <FilterChip label="Curso" selected onPress={() => {}} />,
  );

  expect(visual().backgroundColor).toBe(cores.claro.primaryBg);
  expect(texto().color).toBe(cores.claro.primaryFg);
  expect(
    screen.getByRole("checkbox", { name: "Curso", checked: true }),
  ).toBeTruthy();
});

test("inativo fica em contorno, sem preenchimento", async () => {
  await renderWithTheme(
    <FilterChip label="Curso" selected={false} onPress={() => {}} />,
  );

  expect(visual()).toMatchObject({
    backgroundColor: "transparent",
    borderWidth: 1,
    borderColor: cores.claro.line,
  });
  expect(texto().color).toBe(cores.claro.ink2);
});

test("tocar avisa; desativado fica em surface2 e ink3 e não responde", async () => {
  const onPress = jest.fn();
  const { rerender } = await renderWithTheme(
    <FilterChip label="Curso" selected={false} onPress={onPress} />,
  );
  await fireEvent.press(screen.getByRole("checkbox"));
  expect(onPress).toHaveBeenCalledTimes(1);

  await rerender(
    <FilterChip label="Curso" selected={false} onPress={onPress} disabled />,
  );
  expect(visual().backgroundColor).toBe(cores.claro.surface2);
  expect(texto().color).toBe(cores.claro.ink3);
  await fireEvent.press(screen.getByRole("checkbox", { disabled: true }));
  expect(onPress).toHaveBeenCalledTimes(1);
});

test("pressionado fica com 0,85; em foco, ganha o anel do token", async () => {
  await renderWithTheme(
    <FilterChip label="Curso" selected={false} onPress={() => {}} />,
  );
  const chip = screen.getByRole("checkbox");

  await fireEvent(chip, "responderGrant", toque());
  expect(visual().opacity).toBe(0.85);

  await fireEvent(chip, "focus", {});
  expect(visual()).toMatchObject({
    outlineColor: cores.claro.focus,
    outlineWidth: 2,
    outlineOffset: 2,
  });
});
