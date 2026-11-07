import { SegmentedControl } from "../components/SegmentedControl";
import { useTheme, type ThemePreference } from "./ThemeProvider";

const OPTIONS: { value: ThemePreference; label: string }[] = [
  { value: "sistema", label: "Sistema" },
  { value: "claro", label: "Claro" },
  { value: "escuro", label: "Escuro" },
];

// Escolha do tema. Fica na tela de status, no início provisório e na vitrine até o passo 7,
// quando vai para o perfil.
export function ThemePreferenceControl() {
  const { preference, setPreference } = useTheme();

  return (
    <SegmentedControl
      label="Tema"
      options={OPTIONS}
      value={preference}
      onChange={setPreference}
    />
  );
}
