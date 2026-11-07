import { ActivityIndicator, StyleSheet, View } from "react-native";

import { Logo } from "../marca/Logo";
import { useTheme } from "../theme/ThemeProvider";

// Enquanto o app descobre se há sessão: o logo e um indicador, nunca a tela parada.
export function EsperaDaSessao() {
  const { colors } = useTheme();

  return (
    <View
      style={styles.centro}
      accessible
      aria-busy
      aria-label="Carregando"
      testID="espera-da-sessao"
    >
      <Logo largura={160} />
      <ActivityIndicator color={colors.ink2} />
    </View>
  );
}

const styles = StyleSheet.create({
  centro: { flex: 1, alignItems: "center", justifyContent: "center", gap: 24 },
});
