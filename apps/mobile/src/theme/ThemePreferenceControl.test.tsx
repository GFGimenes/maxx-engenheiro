import AsyncStorage from "@react-native-async-storage/async-storage";
import { fireEvent, screen } from "@testing-library/react-native";

import { renderWithTheme } from "../test/renderWithTheme";
import { ThemePreferenceControl } from "./ThemePreferenceControl";
import { THEME_STORAGE_KEY } from "./ThemeProvider";

beforeEach(() => AsyncStorage.clear());

test("oferece Sistema, Claro e Escuro, com Sistema marcado por padrão", async () => {
  await renderWithTheme(<ThemePreferenceControl />);

  expect(
    screen.getByRole("radio", { name: "Sistema", checked: true }),
  ).toBeTruthy();
  expect(
    screen.getByRole("radio", { name: "Claro", checked: false }),
  ).toBeTruthy();
  expect(
    screen.getByRole("radio", { name: "Escuro", checked: false }),
  ).toBeTruthy();
});

test("tocar numa opção marca e guarda a escolha", async () => {
  await renderWithTheme(<ThemePreferenceControl />);

  await fireEvent.press(screen.getByRole("radio", { name: "Escuro" }));

  expect(
    screen.getByRole("radio", { name: "Escuro", checked: true }),
  ).toBeTruthy();
  expect(await AsyncStorage.getItem(THEME_STORAGE_KEY)).toBe("escuro");
});
