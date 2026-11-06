import AsyncStorage from "@react-native-async-storage/async-storage";
import { act, renderHook } from "@testing-library/react-native";

import {
  THEME_STORAGE_KEY,
  ThemeProvider,
  useTheme,
  type ThemePreference,
} from "./ThemeProvider";
import { cores } from "./tokens";

let mockSystem: "light" | "dark" = "light";
jest.mock("react-native/Libraries/Utilities/useColorScheme", () => ({
  __esModule: true,
  default: () => mockSystem,
}));

// Espera a leitura da preferência guardada, que é o que libera os filhos do provedor.
async function renderTheme() {
  const hook = await renderHook(() => useTheme(), { wrapper: ThemeProvider });
  await act(async () => {});
  return hook;
}

beforeEach(async () => {
  mockSystem = "light";
  await AsyncStorage.clear();
});

test("sem preferência guardada, o padrão é Sistema e segue o aparelho", async () => {
  mockSystem = "dark";
  const { result } = await renderTheme();

  expect(result.current.preference).toBe("sistema");
  expect(result.current.name).toBe("escuro");
  expect(result.current.colors).toEqual(cores.escuro);
});

test("Sistema acompanha a troca de tema do aparelho", async () => {
  const { result, rerender } = await renderTheme();
  expect(result.current.name).toBe("claro");

  mockSystem = "dark";
  await rerender({});

  expect(result.current.name).toBe("escuro");
  expect(result.current.colors).toEqual(cores.escuro);
});

test.each<[ThemePreference, "light" | "dark"]>([
  ["claro", "dark"],
  ["escuro", "light"],
])(
  "a preferência %s vale mesmo com o aparelho em %s",
  async (preference, system) => {
    mockSystem = system;
    await AsyncStorage.setItem(THEME_STORAGE_KEY, preference);
    const { result } = await renderTheme();

    expect(result.current.preference).toBe(preference);
    expect(result.current.name).toBe(preference);
  },
);

test("a troca aplica na hora e fica guardada", async () => {
  const { result } = await renderTheme();

  await act(async () => result.current.setPreference("escuro"));

  expect(result.current.name).toBe("escuro");
  expect(result.current.colors).toEqual(cores.escuro);
  expect(await AsyncStorage.getItem(THEME_STORAGE_KEY)).toBe("escuro");
});

test("valor desconhecido guardado cai no padrão", async () => {
  await AsyncStorage.setItem(THEME_STORAGE_KEY, "sepia");
  const { result } = await renderTheme();

  expect(result.current.preference).toBe("sistema");
});

test("falha ao ler o armazenamento não trava o app", async () => {
  jest
    .mocked(AsyncStorage.getItem)
    .mockRejectedValueOnce(new Error("sem disco"));
  const { result } = await renderTheme();

  expect(result.current.preference).toBe("sistema");
  expect(result.current.name).toBe("claro");
});

test("falha ao gravar mantém a troca até fechar o app", async () => {
  const { result } = await renderTheme();
  jest
    .mocked(AsyncStorage.setItem)
    .mockRejectedValueOnce(new Error("sem disco"));

  await act(async () => result.current.setPreference("escuro"));

  expect(result.current.name).toBe("escuro");
});

test("useTheme fora do provedor avisa com erro claro", async () => {
  const silence = jest.spyOn(console, "error").mockImplementation(() => {});

  await expect(renderHook(() => useTheme())).rejects.toThrow(/ThemeProvider/);

  silence.mockRestore();
});
