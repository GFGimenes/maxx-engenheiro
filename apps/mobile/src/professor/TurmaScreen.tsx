import { useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

import { falhaParaTela } from "../api/FalhaDaApi";
import { Avatar } from "../components/Avatar";
import { Button } from "../components/Button";
import { Cabecalho } from "../components/Cabecalho";
import { Icon } from "../components/Icon";
import { Screen } from "../components/Screen";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { useAlunosDaTurma, useTurmasDoProfessor } from "./api";
import { ModalDeConcessao } from "./ModalDeConcessao";
import { descreverTurma, plural } from "./textos";

// Os alunos de uma turma do professor, com a medalha concedida a partir daqui no passo 6
// (produto.md 4.8). Só aparece quem tem conta e não é ex-aluno: a API já filtra.
export function TurmaScreen({ turmaId }: { turmaId: string }) {
  const { colors } = useTheme();
  const turmas = useTurmasDoProfessor();
  const alunos = useAlunosDaTurma(turmaId);
  const [aluno, setAluno] = useState<{
    id: string;
    nomeDeExibicao: string;
  } | null>(null);
  const [confirmacao, setConfirmacao] = useState<string | null>(null);

  const turma = turmas.data?.find((linha) => linha.id === turmaId);

  return (
    <Screen scroll>
      <Cabecalho titulo={turma?.materia ?? "Turma"} voltarPara="/turmas" />
      {turma && (
        <Text style={[textStyles.legenda, styles.sub, { color: colors.ink2 }]}>
          {descreverTurma(turma)}
        </Text>
      )}

      {confirmacao && (
        <View role="status" aria-live="polite" style={styles.confirmacao}>
          <Icon name="check" color={colors.ink} size={16} />
          <Text
            style={[textStyles.legenda, styles.flex, { color: colors.ink }]}
          >
            {confirmacao}
          </Text>
        </View>
      )}

      {alunos.isPending && (
        <ActivityIndicator style={styles.espera} color={colors.ink2} />
      )}
      {alunos.isError && (
        <ErrorState
          message={falhaParaTela(alunos.error).mensagem}
          onRetry={() => void alunos.refetch()}
        />
      )}
      {alunos.isSuccess && alunos.data.length === 0 && (
        <EmptyState
          icon="inbox"
          title="Nenhum aluno com conta nesta turma."
          message="Quem ainda não se cadastrou no Maxx aparece aqui depois do cadastro."
        />
      )}
      {alunos.isSuccess &&
        alunos.data.map((linha) => (
          <View
            key={linha.id}
            style={[styles.linha, { borderBottomColor: colors.line }]}
          >
            <Avatar nome={linha.nomeDeExibicao} />
            <View style={styles.flex}>
              <Text style={[textStyles.secao, { color: colors.ink }]}>
                {linha.nomeDeExibicao}
              </Text>
              <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                {linha.arroba}
              </Text>
              <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                {linha.medalhas.length === 0
                  ? "Nenhuma medalha nesta turma"
                  : `${plural(linha.medalhas.length, "medalha", "medalhas")} nesta turma`}
              </Text>
            </View>
            <Button
              label="Conceder"
              variant="secondary"
              accessibilityLabel={`Conceder medalha a ${linha.nomeDeExibicao}`}
              onPress={() => {
                setConfirmacao(null);
                setAluno({
                  id: linha.id,
                  nomeDeExibicao: linha.nomeDeExibicao,
                });
              }}
            />
          </View>
        ))}

      <ModalDeConcessao
        turmaId={turmaId}
        aluno={aluno}
        onFechar={() => setAluno(null)}
        onConcedida={(mensagem) => {
          setAluno(null);
          setConfirmacao(mensagem);
        }}
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  sub: { marginBottom: 8 },
  espera: { marginTop: 32 },
  confirmacao: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
    paddingVertical: 8,
  },
  linha: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    paddingVertical: 12,
    borderBottomWidth: 1,
  },
  flex: { flex: 1 },
});
