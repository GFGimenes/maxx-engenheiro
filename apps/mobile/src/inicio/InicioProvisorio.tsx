import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

import { useEu } from "../api/useEu";
import { Button } from "../components/Button";
import { Screen } from "../components/Screen";
import { ErrorState } from "../components/StateMessage";
import { Logo } from "../marca/Logo";
import { useSessao } from "../sessao/SessaoProvider";
import { textStyles } from "../theme/fonts";
import { ThemePreferenceControl } from "../theme/ThemePreferenceControl";
import { useTheme } from "../theme/ThemeProvider";

// O início até o feed chegar, no passo 7: quem entrou, o tema e a saída.
export function InicioProvisorio() {
  const { colors } = useTheme();
  const { sair } = useSessao();
  const eu = useEu();

  return (
    <Screen>
      <View style={styles.coluna}>
        <Logo largura={160} />

        {eu.isPending && <ActivityIndicator color={colors.ink2} />}
        {eu.isError && <ErrorState onRetry={() => void eu.refetch()} />}
        {eu.isSuccess && (
          <View style={styles.quem}>
            <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
              Você entrou como
            </Text>
            <Text
              testID="arroba"
              style={[textStyles.titulo, { color: colors.ink }]}
            >
              {eu.data.arroba}
            </Text>
            <Text
              style={[textStyles.corpo, styles.centro, { color: colors.ink2 }]}
            >
              Em breve, o seu feed aparece aqui.
            </Text>
          </View>
        )}

        <ThemePreferenceControl />
        <Button label="Sair" variant="secondary" onPress={() => void sair()} />
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  coluna: { flex: 1, justifyContent: "center", alignItems: "center", gap: 24 },
  quem: { alignItems: "center", gap: 4 },
  centro: { textAlign: "center" },
});
