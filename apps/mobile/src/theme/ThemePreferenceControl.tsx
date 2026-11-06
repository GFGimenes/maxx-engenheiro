import { Pressable, StyleSheet, Text, View } from "react-native";

import { useTheme, type ThemePreference } from "./ThemeProvider";
import { raios, tamanhos } from "./tokens";

const OPTIONS: { value: ThemePreference; label: string }[] = [
  { value: "sistema", label: "Sistema" },
  { value: "claro", label: "Claro" },
  { value: "escuro", label: "Escuro" },
];

const TRACK_PADDING = 4;

// Escolha do tema. Fica na tela de status e na vitrine até o passo 7, quando vai para o perfil.
export function ThemePreferenceControl() {
  const { preference, setPreference, name, colors } = useTheme();

  return (
    <View
      role="radiogroup"
      aria-label="Tema"
      style={[styles.track, { backgroundColor: colors.surface }]}
    >
      {OPTIONS.map(({ value, label }) => {
        const selected = value === preference;
        return (
          <Pressable
            key={value}
            role="radio"
            aria-checked={selected}
            aria-label={label}
            onPress={() => setPreference(value)}
            style={[
              styles.segment,
              selected && { backgroundColor: colors.segmentOn },
              // A única sombra da interface: controle segmentado, só no claro.
              selected &&
                name === "claro" && { boxShadow: colors.segmentShadow },
            ]}
          >
            <Text
              style={[
                styles.label,
                { color: selected ? colors.ink : colors.ink2 },
              ]}
            >
              {label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  track: {
    flexDirection: "row",
    borderRadius: raios.controleSegmentado,
    padding: TRACK_PADDING,
  },
  // Com o trilho, cada segmento chega aos 44 de toque.
  segment: {
    minHeight: tamanhos.alvoDeToque - 2 * TRACK_PADDING,
    paddingHorizontal: 16,
    justifyContent: "center",
    borderRadius: raios.segmentoAtivo,
  },
  label: { fontSize: 13, fontWeight: "600" },
});
