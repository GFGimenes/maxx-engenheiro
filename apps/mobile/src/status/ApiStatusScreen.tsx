import { ActivityIndicator, Image, StyleSheet, Text, View } from "react-native";

import { useHealth } from "../api/useHealth";
import { Button } from "../components/Button";
import { textStyles } from "../theme/fonts";
import { ThemePreferenceControl } from "../theme/ThemePreferenceControl";
import { useTheme } from "../theme/ThemeProvider";

// Tela de status, em /status: prova que a web fala com a API. Nasceu no passo 1 como
// a página inicial e saiu de lá quando o login chegou, no passo 5.
export function ApiStatusScreen() {
  const { colors } = useTheme();
  const health = useHealth();

  return (
    <View style={styles.container}>
      <Image
        source={require("../../../../design/assets/maxx-ouro.png")}
        style={styles.logo}
        resizeMode="contain"
        accessibilityLabel="Maxx"
      />

      <View testID="estado-api" style={styles.status}>
        {health.isPending && (
          <>
            <ActivityIndicator color={colors.ink2} />
            <Text style={[styles.message, { color: colors.ink2 }]}>
              Conectando...
            </Text>
          </>
        )}

        {health.isSuccess && (
          <>
            <Text style={[styles.message, { color: colors.ink }]}>
              Tudo certo por aqui.
            </Text>
            <Text style={[styles.detail, { color: colors.ink2 }]}>
              A API respondeu {health.data.httpStatus}.
            </Text>
          </>
        )}

        {health.isError && (
          <>
            <Text style={[styles.message, { color: colors.ink }]}>
              Não conseguimos falar com o servidor agora.
            </Text>
            <Button
              label="Tentar de novo"
              variant="secondary"
              onPress={() => health.refetch()}
            />
          </>
        )}
      </View>

      <ThemePreferenceControl />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    paddingHorizontal: 16,
    gap: 24,
  },
  logo: { width: 106, height: 28 },
  status: { alignItems: "center", gap: 8 },
  message: { ...textStyles.corpo, textAlign: "center" },
  detail: { ...textStyles.legenda, textAlign: "center" },
});
