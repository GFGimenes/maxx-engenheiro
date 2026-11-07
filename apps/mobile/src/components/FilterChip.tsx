import { Pressable, StyleSheet, Text, View } from "react-native";

import { fontFamily } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { interacao, raios, tamanhos } from "../theme/tokens";
import { focusRingStyle, noOutline, useFocusRing } from "./focusRing";

interface FilterChipProps {
  label: string;
  selected: boolean;
  onPress: () => void;
  disabled?: boolean;
  // `radio` quando o grupo é de escolha única, como o curso da demonstração.
  role?: "checkbox" | "radio";
}

export function FilterChip({
  label,
  selected,
  onPress,
  disabled = false,
  role = "checkbox",
}: FilterChipProps) {
  const { colors } = useTheme();
  const { focusVisible, focusProps } = useFocusRing();

  const cor = disabled
    ? { fundo: colors.surface2, borda: colors.surface2, texto: colors.ink3 }
    : selected
      ? {
          fundo: colors.primaryBg,
          borda: colors.primaryBg,
          texto: colors.primaryFg,
        }
      : { fundo: "transparent", borda: colors.line, texto: colors.ink2 };

  return (
    <Pressable
      role={role}
      aria-checked={selected}
      aria-label={label}
      disabled={disabled}
      onPress={onPress}
      {...focusProps}
      style={[styles.toque, noOutline]}
    >
      {({ pressed }) => (
        <View
          testID="chip-visual"
          style={[
            styles.chip,
            { backgroundColor: cor.fundo, borderColor: cor.borda },
            pressed && { opacity: interacao.opacidadePressionado },
            focusVisible && focusRingStyle(colors.focus),
          ]}
        >
          <Text
            numberOfLines={1}
            style={[
              selected ? styles.textoAtivo : styles.texto,
              { color: cor.texto },
            ]}
          >
            {label}
          </Text>
        </View>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  toque: { minHeight: tamanhos.alvoDeToque, justifyContent: "center" },
  chip: {
    height: tamanhos.botaoDiscreto.altura,
    borderRadius: raios.filtros,
    borderWidth: 1,
    paddingHorizontal: 12,
    alignItems: "center",
    justifyContent: "center",
  },
  texto: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 500 }),
    fontSize: tamanhos.botaoDiscreto.texto,
  },
  textoAtivo: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 600 }),
    fontSize: tamanhos.botaoDiscreto.texto,
  },
});
