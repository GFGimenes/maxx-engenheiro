import { act, render, screen } from "@testing-library/react-native";
import { SafeAreaProvider } from "react-native-safe-area-context";

import { iconNames } from "../components/Icon";
import { ThemeProvider } from "../theme/ThemeProvider";
import { VitrineScreen } from "./VitrineScreen";

test("mostra a escala tipográfica, os ícones nos dois modos e a troca de tema", async () => {
  await render(
    <SafeAreaProvider
      initialMetrics={{
        insets: { top: 0, bottom: 0, left: 0, right: 0 },
        frame: { x: 0, y: 0, width: 390, height: 844 },
      }}
    >
      <ThemeProvider>
        <VitrineScreen />
      </ThemeProvider>
    </SafeAreaProvider>,
  );
  await act(async () => {});

  expect(screen.getByText("15.020 pts")).toBeTruthy();
  expect(screen.getAllByText("Último troféu")).toHaveLength(2);
  expect(screen.getAllByRole("img", { name: /Lendário/ })).toHaveLength(5);
  expect(screen.getAllByText("Techday 2026")).toHaveLength(2);
  expect(screen.getByRole("radio", { name: "Sistema" })).toBeTruthy();
  expect(screen.getAllByRole("button", { name: "Entrar" })).toHaveLength(2);
  expect(screen.getByLabelText("Senha")).toBeTruthy();
  expect(screen.getByRole("checkbox", { name: "Todos" })).toBeTruthy();
  expect(screen.getByRole("tab", { name: "Mural, com novidade" })).toBeTruthy();
  expect(screen.getByRole("alert")).toBeTruthy();
  expect(screen.getByText("Nenhum aviso por aqui")).toBeTruthy();
  for (const name of iconNames) {
    expect(
      screen.getAllByTestId(`icone-${name}`, { includeHiddenElements: true }),
    ).not.toHaveLength(0);
  }
});
