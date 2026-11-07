import { useRouter } from "expo-router";
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  View,
} from "react-native";

import { falhaParaTela } from "../api/FalhaDaApi";
import { Cabecalho } from "../components/Cabecalho";
import { Icon } from "../components/Icon";
import { Screen } from "../components/Screen";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { tamanhos } from "../theme/tokens";
import { useTurmasDoProfessor } from "./api";
import { descreverTurma } from "./textos";

// As turmas do professor no semestre vigente (produto.md 10.1, lista de turmas e alunos do
// professor). Sem prancheta: segue a identidade visual, em lista como as do Instagram.
export function TurmasScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const turmas = useTurmasDoProfessor();

  return (
    <Screen scroll>
      <Cabecalho titulo="Minhas turmas" voltarPara="/" />

      {turmas.isPending && (
        <ActivityIndicator style={styles.espera} color={colors.ink2} />
      )}
      {turmas.isError && (
        <ErrorState
          message={falhaParaTela(turmas.error).mensagem}
          onRetry={() => void turmas.refetch()}
        />
      )}
      {turmas.isSuccess && turmas.data.length === 0 && (
        <EmptyState
          icon="inbox"
          title="Nenhuma turma neste semestre."
          message="As turmas em que você dá aula no semestre vigente aparecem aqui."
        />
      )}
      {turmas.isSuccess &&
        turmas.data.map((turma) => (
          <Pressable
            key={turma.id}
            role="button"
            accessibilityLabel={`${turma.materia}, ${descreverTurma(turma)}`}
            style={({ pressed }) => [
              styles.linha,
              { borderBottomColor: colors.line },
              pressed && styles.pressionado,
            ]}
            onPress={() => router.push(`/turmas/${turma.id}`)}
          >
            <View style={styles.texto}>
              <Text style={[textStyles.secao, { color: colors.ink }]}>
                {turma.materia}
              </Text>
              <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                {descreverTurma(turma)}
              </Text>
            </View>
            <Icon name="avancar" color={colors.ink2} size={20} />
          </Pressable>
        ))}
    </Screen>
  );
}

const styles = StyleSheet.create({
  espera: { marginTop: 32 },
  linha: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    minHeight: tamanhos.alvoDeToque + 20,
    paddingVertical: 12,
    borderBottomWidth: 1,
  },
  texto: { flex: 1, gap: 2 },
  pressionado: { opacity: 0.85 },
});
