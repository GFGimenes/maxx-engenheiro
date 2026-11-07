import { fireEvent, screen } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { renderWithTheme } from "../test/renderWithTheme";
import { cores } from "../theme/tokens";
import { TextLink } from "./TextLink";

const texto = (label: string) =>
  StyleSheet.flatten(screen.getByText(label).props.style);

test("é um link com 44 de toque, em ink2, que responde ao toque", async () => {
  const onPress = jest.fn();
  await renderWithTheme(
    <TextLink label="Esqueci minha senha" onPress={onPress} />,
  );

  const link = screen.getByRole("link", { name: "Esqueci minha senha" });
  expect(StyleSheet.flatten(link.props.style).minHeight).toBe(44);
  expect(texto("Esqueci minha senha").color).toBe(cores.claro.ink2);

  await fireEvent.press(link);
  expect(onPress).toHaveBeenCalledTimes(1);
});

test("desativado fica em ink3 e não responde", async () => {
  const onPress = jest.fn();
  await renderWithTheme(
    <TextLink label="Esqueci minha senha" onPress={onPress} disabled />,
  );

  await fireEvent.press(
    screen.getByRole("link", { name: "Esqueci minha senha", disabled: true }),
  );

  expect(onPress).not.toHaveBeenCalled();
  expect(texto("Esqueci minha senha").color).toBe(cores.claro.ink3);
});
