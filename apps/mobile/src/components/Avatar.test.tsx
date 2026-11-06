import { screen } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { renderWithTheme } from "../test/renderWithTheme";
import { cores, tamanhos } from "../theme/tokens";
import { Avatar, iniciais } from "./Avatar";

test.each([
  ["Ana Souza", "AS"],
  ["Ana Paula Souza", "AS"],
  ["  ana  souza ", "AS"],
  ["Ana", "A"],
  ["Élida Ângelo", "ÉÂ"],
])("as iniciais de %j são %s", (nome, esperado) => {
  expect(iniciais(nome)).toBe(esperado);
});

test("usa só as cores do token e o tamanho pedido", async () => {
  await renderWithTheme(<Avatar nome="Ana Souza" size={tamanhos.avatarNav} />);

  expect(
    StyleSheet.flatten(
      screen.getByRole("img", { name: "Ana Souza" }).props.style,
    ),
  ).toMatchObject({
    width: 30,
    height: 30,
    backgroundColor: cores.claro.avatarBg,
  });
  expect(StyleSheet.flatten(screen.getByText("AS").props.style).color).toBe(
    cores.claro.avatarFg,
  );
});

test("o tamanho padrão é o do post", async () => {
  await renderWithTheme(<Avatar nome="Ana Souza" />);

  expect(StyleSheet.flatten(screen.getByRole("img").props.style).width).toBe(
    36,
  );
});
