import { Pressable, StyleSheet, Text, View } from "react-native";

import { fontFamily } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { interacao, raios, tamanhos } from "../theme/tokens";
import { focusRingStyle, noOutline, useFocusRing } from "./focusRing";

interface ButtonProps {
  label: string;
  onPress: () => void;
  variant?: "primary" | "secondary";
  // `large` é só a ação principal das telas de autenticação. O discreto ocupa a largura
  // que o pai der.
  size?: "compact" | "large";
  disabled?: boolean;
}

export function Button({
  label,
  onPress,
  variant = "primary",
  size = "compact",
  disabled = false,
}: ButtonProps) {
  const { colors } = useTheme();
  const { focusVisible, focusProps } = useFocusRing();

  const cor = disabled
    ? { fundo: colors.surface2, texto: colors.ink3 }
    : variant === "primary"
      ? { fundo: colors.primaryBg, texto: colors.primaryFg }
      : { fundo: colors.surface, texto: colors.ink };

  return (
    <Pressable
      role="button"
      disabled={disabled}
      onPress={onPress}
      {...focusProps}
      style={[size === "large" ? styles.toqueGrande : styles.toque, noOutline]}
    >
      {({ pressed }) => (
        <View
          testID="botao-visual"
          style={[
            size === "large" ? styles.grande : styles.discreto,
            { backgroundColor: cor.fundo },
            pressed && { opacity: interacao.opacidadePressionado },
            focusVisible && focusRingStyle(colors.focus),
          ]}
        >
          <Text
            numberOfLines={1}
            style={[
              size === "large" ? styles.textoGrande : styles.texto,
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
  toqueGrande: { alignSelf: "stretch" },
  discreto: {
    height: tamanhos.botaoDiscreto.altura,
    borderRadius: raios.botoes,
    paddingHorizontal: 12,
    alignItems: "center",
    justifyContent: "center",
  },
  grande: {
    height: tamanhos.botaoGrande,
    borderRadius: raios.botoes,
    paddingHorizontal: 16,
    alignItems: "center",
    justifyContent: "center",
  },
  texto: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 600 }),
    fontSize: tamanhos.botaoDiscreto.texto,
  },
  // A identidade não dá o tamanho deste texto: 15 é o da escala que mais se aproxima do login.
  textoGrande: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 700 }),
    fontSize: 15,
  },
});
