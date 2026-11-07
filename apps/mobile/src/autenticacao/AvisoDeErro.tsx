import { StyleSheet, Text, View } from "react-native";

import { Icon } from "../components/Icon";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";

// O erro de um formulário que não é de um campo só (sem conexão, muitas tentativas). O
// mesmo desenho do erro de campo: ícone em `like`, texto em `ink`, anunciado ao aparecer.
export function AvisoDeErro({ mensagem }: { mensagem: string }) {
  const { colors } = useTheme();

  return (
    <View role="alert" aria-live="polite" accessible style={styles.linha}>
      <Icon name="alert" color={colors.like} size={16} />
      <Text style={[textStyles.legenda, styles.texto, { color: colors.ink }]}>
        {mensagem}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  linha: { flexDirection: "row", alignItems: "flex-start", gap: 8 },
  texto: { flex: 1 },
});
