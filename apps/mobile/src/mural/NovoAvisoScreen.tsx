import { useRouter } from "expo-router";
import { useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

import { falhaParaTela } from "../api/FalhaDaApi";
import { useEu } from "../api/useEu";
import { Cabecalho } from "../components/Cabecalho";
import { FilterChip } from "../components/FilterChip";
import { Screen } from "../components/Screen";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { useTurmasDoProfessor } from "../professor/api";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { FormularioDoAviso } from "./FormularioDoAviso";

export {
  TEXTO_MAXIMO,
  TITULO_MAXIMO,
  TITULO_MINIMO,
} from "./FormularioDoAviso";

// Novo aviso do professor (produto.md 7), pelo "+" do Mural. Sem prancheta: segue a
// identidade visual e o novo post. O professor escolhe uma ou mais turmas suas do semestre
// vigente, escreve título e texto e, se quiser, uma foto, que sobe como a do post. O aviso
// da faculdade e dos cursos sai pela área da instituição (passo 12).
export function NovoAvisoScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const eu = useEu();
  const ehProfessor = eu.data?.papeis.includes("professor") ?? false;
  const turmas = useTurmasDoProfessor(ehProfessor);
  const [escolhidas, setEscolhidas] = useState<string[]>([]);

  if (eu.isSuccess && !ehProfessor) {
    return (
      <Screen>
        <Cabecalho titulo="Novo aviso" voltarPara="/mural" />
        <EmptyState
          icon="newspaper"
          title="Só professor publica aviso aqui."
          message="Os avisos da faculdade e dos cursos saem pela área da instituição."
        />
      </Screen>
    );
  }

  return (
    <Screen scroll>
      <Cabecalho titulo="Novo aviso" voltarPara="/mural" />
      <FormularioDoAviso
        alvo={
          escolhidas.length > 0
            ? { escopo: "turma", turmaIds: escolhidas }
            : null
        }
        placeholderDoTexto="Escreva o aviso para a turma."
        onPublicado={() => router.dismissTo("/mural")}
        paraQuem={(ocupado, mudou) => (
          <View style={styles.secao}>
            <Text
              role="heading"
              style={[textStyles.secao, { color: colors.ink }]}
            >
              Para quais turmas
            </Text>
            <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
              Escolha uma ou mais turmas suas deste semestre.
            </Text>
            {turmas.isPending && ehProfessor && (
              <ActivityIndicator style={styles.espera} color={colors.ink2} />
            )}
            {turmas.isError && (
              <ErrorState
                message={falhaParaTela(turmas.error).mensagem}
                onRetry={() => void turmas.refetch()}
              />
            )}
            {turmas.isSuccess && turmas.data.length === 0 && (
              <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
                Você não tem turma neste semestre.
              </Text>
            )}
            {turmas.isSuccess && turmas.data.length > 0 && (
              <View style={styles.chips}>
                {turmas.data.map((turma) => (
                  <FilterChip
                    key={turma.id}
                    label={`${turma.materia} · ${turma.curso}`}
                    selected={escolhidas.includes(turma.id)}
                    disabled={ocupado}
                    onPress={() => {
                      mudou();
                      setEscolhidas((atuais) =>
                        atuais.includes(turma.id)
                          ? atuais.filter((id) => id !== turma.id)
                          : [...atuais, turma.id],
                      );
                    }}
                  />
                ))}
              </View>
            )}
          </View>
        )}
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  secao: { gap: 8 },
  chips: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
  espera: { marginTop: 8 },
});
