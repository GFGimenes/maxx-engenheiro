import { useState } from "react";
import {
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from "react-native";

import { fontFamily } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { interacao, raios, tamanhos } from "../theme/tokens";
import { focusRingStyle, noOutline, useFocusRing } from "./focusRing";
import { Icon } from "./Icon";

interface TextFieldProps extends Omit<
  TextInputProps,
  "style" | "editable" | "secureTextEntry" | "placeholderTextColor"
> {
  label: string;
  // Esconde o texto e oferece o botão de mostrar.
  password?: boolean;
  disabled?: boolean;
}

const RECUO = 14;

export function TextField({
  label,
  password = false,
  disabled = false,
  onFocus,
  onBlur,
  ...input
}: TextFieldProps) {
  const { colors } = useTheme();
  const [focused, setFocused] = useState(false);
  const [revealed, setRevealed] = useState(false);
  const borda = focused ? interacao.bordaDeCampoEmFoco : interacao.bordaDeCampo;

  return (
    <View style={styles.coluna}>
      <Text style={[styles.rotulo, { color: colors.ink }]}>{label}</Text>
      <View
        testID="campo-caixa"
        style={[
          styles.caixa,
          {
            borderWidth: borda,
            borderColor: focused ? colors.focus : colors.fieldLine,
            backgroundColor: disabled ? colors.surface2 : colors.fieldBg,
            // A borda mais grossa do foco não pode empurrar o texto.
            paddingLeft: RECUO - borda,
          },
        ]}
      >
        <TextInput
          autoCapitalize={password ? "none" : undefined}
          autoCorrect={password ? false : undefined}
          {...input}
          aria-label={label}
          editable={!disabled}
          secureTextEntry={password && !revealed}
          placeholderTextColor={colors.ink3}
          onFocus={(event) => {
            setFocused(true);
            onFocus?.(event);
          }}
          onBlur={(event) => {
            setFocused(false);
            onBlur?.(event);
          }}
          style={[
            styles.entrada,
            noOutline,
            { color: disabled ? colors.ink3 : colors.ink },
          ]}
        />
        {password && (
          <RevealButton
            revealed={revealed}
            disabled={disabled}
            onPress={() => setRevealed((atual) => !atual)}
          />
        )}
      </View>
    </View>
  );
}

function RevealButton({
  revealed,
  disabled,
  onPress,
}: {
  revealed: boolean;
  disabled: boolean;
  onPress: () => void;
}) {
  const { colors } = useTheme();
  const { focusVisible, focusProps } = useFocusRing();

  return (
    <Pressable
      role="button"
      aria-label={revealed ? "Ocultar senha" : "Mostrar senha"}
      disabled={disabled}
      onPress={onPress}
      {...focusProps}
      style={({ pressed }) => [
        styles.mostrar,
        noOutline,
        pressed && { opacity: interacao.opacidadePressionado },
        focusVisible && focusRingStyle(colors.focus),
      ]}
    >
      <Icon
        name={revealed ? "eyeOff" : "eye"}
        color={disabled ? colors.ink3 : colors.ink2}
      />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  coluna: { gap: 8 },
  rotulo: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 600 }),
    fontSize: 14,
    lineHeight: 20,
  },
  caixa: {
    height: tamanhos.campo,
    borderRadius: raios.campos,
    flexDirection: "row",
    alignItems: "center",
  },
  // 16 fora da escala: abaixo disso, o Safari do iPhone dá zoom na tela ao focar o campo.
  entrada: {
    flex: 1,
    height: "100%",
    paddingVertical: 0,
    paddingHorizontal: 0,
    paddingRight: RECUO,
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 400 }),
    fontSize: 16,
  },
  mostrar: {
    width: tamanhos.alvoDeToque,
    height: tamanhos.alvoDeToque,
    borderRadius: raios.campos,
    alignItems: "center",
    justifyContent: "center",
  },
});
