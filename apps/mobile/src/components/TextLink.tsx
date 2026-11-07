import { Pressable, StyleSheet, Text } from "react-native";

import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { interacao, raios, tamanhos } from "../theme/tokens";
import { focusRingStyle, noOutline, useFocusRing } from "./focusRing";

interface TextLinkProps {
  label: string;
  onPress: () => void;
  disabled?: boolean;
}

// Ação em texto, como o "Esqueci minha senha" da prancheta do login: discreta, em ink2,
// com os 44 de toque de todo controle.
export function TextLink({ label, onPress, disabled = false }: TextLinkProps) {
  const { colors } = useTheme();
  const { focusVisible, focusProps } = useFocusRing();

  return (
    <Pressable
      role="link"
      aria-label={label}
      disabled={disabled}
      onPress={onPress}
      {...focusProps}
      style={({ pressed }) => [
        styles.toque,
        noOutline,
        pressed && { opacity: interacao.opacidadePressionado },
        focusVisible && focusRingStyle(colors.focus),
      ]}
    >
      <Text
        style={[
          textStyles.corpo,
          { color: disabled ? colors.ink3 : colors.ink2 },
        ]}
      >
        {label}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  toque: {
    minHeight: tamanhos.alvoDeToque,
    justifyContent: "center",
    borderRadius: raios.botoes,
  },
});
