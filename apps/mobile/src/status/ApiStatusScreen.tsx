import {
  ActivityIndicator,
  Image,
  Pressable,
  StyleSheet,
  Text,
  View,
} from "react-native";

import { useHealth } from "../api/useHealth";
import { useThemeColors } from "../theme/useThemeColors";

// Tela provisória do passo 1: prova que a web fala com a API. Sai quando chegar o login.
export function ApiStatusScreen() {
  const colors = useThemeColors();
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
            <Pressable
              accessibilityRole="button"
              onPress={() => health.refetch()}
              style={[styles.button, { backgroundColor: colors.surface }]}
            >
              <Text style={[styles.buttonText, { color: colors.ink }]}>
                Tentar de novo
              </Text>
            </Pressable>
          </>
        )}
      </View>
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
  message: { fontSize: 14, lineHeight: 20, textAlign: "center" },
  detail: { fontSize: 12, lineHeight: 16, textAlign: "center" },
  // Botão discreto da identidade: 32 de altura visual, raio 8, texto 13, 44 de toque.
  button: {
    height: 32,
    borderRadius: 8,
    paddingHorizontal: 12,
    justifyContent: "center",
    marginTop: 8,
  },
  buttonText: { fontSize: 13, fontWeight: "600" },
});
