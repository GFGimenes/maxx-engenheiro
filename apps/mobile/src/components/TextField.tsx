import { useState } from "react";
import {
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from "react-native";

import { fontFamily, textStyles } from "../theme/fonts";
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
  // O que está errado neste campo, em frase curta. Sem isso, o campo está bom.
  error?: string;
}

const RECUO = 14;
const ICONE_DO_ERRO = 16;

export function TextField({
  label,
  password = false,
  disabled = false,
  error,
  onFocus,
  onBlur,
  ...input
}: TextFieldProps) {
  const { colors } = useTheme();
  const [focused, setFocused] = useState(false);
  const [revealed, setRevealed] = useState(false);
  const borda = focused ? interacao.bordaDeCampoEmFoco : interacao.bordaDeCampo;
  const comErro = !!error;

  // Exceção registrada à regra do `like` só em ícone: a borda do campo com erro também é
  // `like`, para o erro aparecer no próprio campo. O texto do erro fica em `ink`.
  const corDaBorda = comErro
    ? colors.like
    : focused
      ? colors.focus
      : colors.fieldLine;

  return (
    <View style={styles.coluna}>
      <Text style={[styles.rotulo, { color: colors.ink }]}>{label}</Text>
      <View
        testID="campo-caixa"
        style={[
          styles.caixa,
          input.multiline && styles.caixaDeVariasLinhas,
          {
            borderWidth: borda,
            borderColor: corDaBorda,
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
          aria-invalid={comErro}
          // No nativo, o leitor de tela lê o erro junto do campo.
          accessibilityHint={error}
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
            input.multiline && styles.entradaDeVariasLinhas,
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
      {comErro && (
        // Anunciado quando aparece; ícone e texto juntos, nunca só a cor.
        <View
          testID="campo-erro"
          role="alert"
          aria-live="polite"
          accessible
          style={styles.erro}
        >
          <Icon name="alert" color={colors.like} size={ICONE_DO_ERRO} />
          <Text
            style={[
              textStyles.legenda,
              styles.textoDoErro,
              { color: colors.ink },
            ]}
          >
            {error}
          </Text>
        </View>
      )}
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
  // Legenda e comentário: cresce para baixo, com o texto começando no topo.
  caixaDeVariasLinhas: {
    height: undefined,
    minHeight: 96,
    alignItems: "stretch",
  },
  entradaDeVariasLinhas: {
    height: undefined,
    minHeight: 96,
    paddingVertical: 12,
    textAlignVertical: "top",
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
  erro: { flexDirection: "row", alignItems: "flex-start", gap: 8 },
  // Legenda tem linha de 16, a altura do ícone.
  textoDoErro: { flex: 1 },
});
