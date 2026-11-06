import AsyncStorage from "@react-native-async-storage/async-storage";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { useColorScheme } from "react-native";

import { cores, type ThemeColors, type ThemeName } from "./tokens";

export type ThemePreference = "sistema" | ThemeName;

export const THEME_STORAGE_KEY = "maxx.tema";

interface ThemeContextValue {
  preference: ThemePreference;
  setPreference: (preference: ThemePreference) => void;
  // O tema efetivo: a preferência, ou o do aparelho quando ela é "sistema".
  name: ThemeName;
  colors: ThemeColors;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

function parsePreference(stored: string | null): ThemePreference {
  return stored === "claro" || stored === "escuro" ? stored : "sistema";
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const system = useColorScheme();
  const [preference, setPreferenceState] = useState<ThemePreference | null>(
    null,
  );

  useEffect(() => {
    // Armazenamento indisponível não pode travar o app: fica o padrão, Sistema.
    AsyncStorage.getItem(THEME_STORAGE_KEY)
      .then(parsePreference)
      .catch((): ThemePreference => "sistema")
      .then((loaded) => setPreferenceState((current) => current ?? loaded));
  }, []);

  const setPreference = useCallback((next: ThemePreference) => {
    setPreferenceState(next);
    // Se a gravação falhar, a troca vale até fechar o app.
    AsyncStorage.setItem(THEME_STORAGE_KEY, next).catch(() => {});
  }, []);

  const value = useMemo<ThemeContextValue | null>(() => {
    if (preference === null) return null;
    const systemName: ThemeName = system === "dark" ? "escuro" : "claro";
    const name = preference === "sistema" ? systemName : preference;
    return { preference, setPreference, name, colors: cores[name] };
  }, [preference, setPreference, system]);

  // Até ler a preferência, não pinta nada: evita piscar o tema errado.
  if (value === null) return null;

  return (
    <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
  );
}

export function useTheme(): ThemeContextValue {
  const value = useContext(ThemeContext);
  if (value === null) {
    throw new Error("useTheme precisa estar dentro de ThemeProvider.");
  }
  return value;
}
