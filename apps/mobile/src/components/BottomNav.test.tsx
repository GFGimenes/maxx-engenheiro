import { act, fireEvent, render, screen } from "@testing-library/react-native";
import type { ComponentProps } from "react";
import { StyleSheet } from "react-native";
import { SafeAreaProvider } from "react-native-safe-area-context";

import { ThemeProvider } from "../theme/ThemeProvider";
import { cores } from "../theme/tokens";
import { BottomNav } from "./BottomNav";

async function renderNav(
  props: Partial<ComponentProps<typeof BottomNav>> = {},
  bottom = 0,
) {
  await render(
    <SafeAreaProvider
      initialMetrics={{
        insets: { top: 0, bottom, left: 0, right: 0 },
        frame: { x: 0, y: 0, width: 390, height: 844 },
      }}
    >
      <ThemeProvider>
        <BottomNav
          active="home"
          onSelect={() => {}}
          nome="Ana Souza"
          {...props}
        />
      </ThemeProvider>
    </SafeAreaProvider>,
  );
  await act(async () => {});
}

const barra = () =>
  StyleSheet.flatten(screen.getByTestId("navegacao").props.style);
const realce = (rotulo: string) =>
  StyleSheet.flatten(screen.getByTestId(`realce-${rotulo}`).props.style);

test("traz as cinco abas, na ordem da identidade", async () => {
  await renderNav();

  expect(
    screen
      .getAllByRole("tab")
      .map((aba) => aba.props.accessibilityLabel ?? aba.props["aria-label"]),
  ).toEqual(["Home", "Busca", "Mural", "Troféus", "Perfil"]);
});

test("tem 84 de altura e borda superior de 1 px em line", async () => {
  await renderNav();

  expect(barra()).toMatchObject({
    height: 84,
    borderTopWidth: 1,
    borderTopColor: cores.claro.line,
  });
});

test.each([
  [0, 84],
  [34, 84],
  [48, 98],
])("com área segura de %i, a altura é %i", async (bottom, altura) => {
  await renderNav({}, bottom);

  expect(barra().height).toBe(altura);
});

test("a aba ativa tem realce de 44 x 44, raio 14, em navHighlight", async () => {
  await renderNav({ active: "mural" });

  expect(realce("Mural")).toMatchObject({
    width: 44,
    height: 44,
    borderRadius: 14,
    backgroundColor: cores.claro.navHighlight,
  });
  expect(realce("Home").backgroundColor).toBeUndefined();
  expect(
    screen.getByRole("tab", { name: "Mural", selected: true }),
  ).toBeTruthy();
  expect(
    screen.getByRole("tab", { name: "Home", selected: false }),
  ).toBeTruthy();
});

test("Perfil ativo mantém o avatar sobre o realce", async () => {
  await renderNav({ active: "perfil" });

  expect(realce("Perfil").backgroundColor).toBe(cores.claro.navHighlight);
  expect(screen.getByText("AS")).toBeTruthy();
});

test("tocar uma aba avisa qual foi", async () => {
  const onSelect = jest.fn();
  await renderNav({ onSelect });

  await fireEvent.press(screen.getByRole("tab", { name: "Troféus" }));
  expect(onSelect).toHaveBeenLastCalledWith("trofeus");

  await fireEvent.press(screen.getByRole("tab", { name: "Perfil" }));
  expect(onSelect).toHaveBeenLastCalledWith("perfil");
});

test("a novidade é uma bolinha de 8 em gold, com halo de 2 na cor do fundo, e também vai no rótulo", async () => {
  await renderNav({ novidades: { mural: true } });

  expect(
    StyleSheet.flatten(screen.getByTestId("novidade-Mural").props.style),
  ).toMatchObject({
    width: 12,
    height: 12,
    borderWidth: 2,
    backgroundColor: cores.claro.gold,
    borderColor: cores.claro.bg,
  });
  expect(screen.getByRole("tab", { name: "Mural, com novidade" })).toBeTruthy();
  expect(screen.queryByTestId("novidade-Troféus")).toBeNull();
});

test("a novidade aparece também na aba ativa", async () => {
  await renderNav({ active: "trofeus", novidades: { trofeus: true } });

  expect(screen.getByTestId("novidade-Troféus")).toBeTruthy();
});

test("em foco, o realce ganha o anel do token", async () => {
  await renderNav();

  await fireEvent(screen.getByRole("tab", { name: "Busca" }), "focus", {});

  expect(realce("Busca")).toMatchObject({
    outlineColor: cores.claro.focus,
    outlineWidth: 2,
    outlineOffset: 2,
  });
});
