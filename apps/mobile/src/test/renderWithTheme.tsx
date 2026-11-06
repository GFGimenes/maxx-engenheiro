import { act, render } from "@testing-library/react-native";
import type { ReactElement } from "react";

import { ThemeProvider } from "../theme/ThemeProvider";

// Renderiza dentro do ThemeProvider e espera a leitura da preferência guardada.
export async function renderWithTheme(ui: ReactElement) {
  const wrap = (element: ReactElement) => (
    <ThemeProvider>{element}</ThemeProvider>
  );
  const result = await render(wrap(ui));
  await act(async () => {});
  return {
    ...result,
    rerender: (element: ReactElement) => result.rerender(wrap(element)),
  };
}
