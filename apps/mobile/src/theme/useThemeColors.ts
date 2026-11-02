import { useColorScheme } from "react-native";

import tokens from "../../../../design/tokens.json";

// Provisório até o passo 4 (tema tipado e troca de tema): lê as cores direto do
// tokens.json e segue o tema do sistema.
export type ThemeColors = typeof tokens.cores.claro;

export function useThemeColors(): ThemeColors {
  return useColorScheme() === "dark" ? tokens.cores.escuro : tokens.cores.claro;
}
