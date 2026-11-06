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
  expect(screen.getByText("Último troféu")).toBeTruthy();
  expect(screen.getByRole("radio", { name: "Sistema" })).toBeTruthy();
  for (const name of iconNames) {
    expect(
      screen.getAllByTestId(`icone-${name}`, { includeHiddenElements: true }),
    ).toHaveLength(2);
  }
});
