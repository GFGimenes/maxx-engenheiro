import { useLocalSearchParams, useRouter } from "expo-router";
import { useMemo, useState } from "react";
import {
  ActivityIndicator,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../../api/FalhaDaApi";
import { AvisoDeErro } from "../../autenticacao/AvisoDeErro";
import { Button } from "../../components/Button";
import { EmptyState, ErrorState } from "../../components/StateMessage";
import {
  ESCOLHA_VAZIA,
  EscolhaDaReivindicacao,
  eventoDaEscolha,
  type Escolha,
} from "../../premiosParticipacao/EscolhaDaReivindicacao";
import { textStyles } from "../../theme/fonts";
import { useTheme } from "../../theme/ThemeProvider";
import { espacamento } from "../../theme/tokens";
import { TelaDaArea } from "../TelaDaArea";
import {
  fontesDoAluno,
  useConceder,
  useFicha,
  useItensParaConcessao,
} from "./api";

// A concessão direta de prêmio de participação pela instituição (produto.md 4.7, P2 do
// passo 12): já aprovado, sem foto e sem comprovação, com a janela de 15 dias. A escolha é a
// mesma da reivindicação do aluno (prêmio, evento da lista ou digitado, e a matéria quando o
// prêmio pede), com os eventos e as matérias do aluno da ficha. Sem prancheta.
export function ConcederPremioScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return (
    <TelaDaArea
      titulo="Conceder prêmio"
      voltarPara={`/instituicao/alunos/${id}`}
    >
      <Concessao cadastroId={id} />
    </TelaDaArea>
  );
}

function Concessao({ cadastroId }: { cadastroId: string }) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const ficha = useFicha(cadastroId);
  const itens = useItensParaConcessao(cadastroId);
  const conceder = useConceder(cadastroId);
  const fontes = useMemo(() => fontesDoAluno(cadastroId), [cadastroId]);
  const [escolha, setEscolha] = useState<Escolha>(ESCOLHA_VAZIA);
  const item = itens.data?.find(
    (candidato) => candidato.codigo === escolha.item,
  );
  const evento = item ? eventoDaEscolha(escolha, item.exigeMateria) : null;
  const pronto =
    !!item && !!evento && (!item.exigeMateria || !!escolha.materiaId);
  const nome = ficha.data?.aluno.nomeCompleto ?? "o aluno";

  if (itens.isPending) {
    return <ActivityIndicator style={styles.espera} color={colors.ink2} />;
  }

  if (itens.isError) {
    return (
      <View style={styles.margem}>
        <ErrorState
          message={falhaParaTela(itens.error).mensagem}
          onRetry={() => void itens.refetch()}
        />
      </View>
    );
  }

  if (conceder.isSuccess) {
    return (
      <View style={[styles.margem, styles.coluna]}>
        <EmptyState
          icon="taca"
          title="Prêmio concedido."
          message={`${nome} já pontua com ele e recebe a notificação. O prêmio pode ser contestado por 15 dias.`}
        />
        <Button
          label="Voltar à ficha"
          variant="secondary"
          onPress={() =>
            // A ficha está embaixo, na pilha: voltar a ela, e não empilhar outra.
            router.canGoBack()
              ? router.back()
              : router.replace(`/instituicao/alunos/${cadastroId}`)
          }
        />
      </View>
    );
  }

  return (
    <ScrollView
      contentContainerStyle={[
        styles.margem,
        styles.coluna,
        { paddingBottom: espacamento.respiroAntesDaNavegacao + insets.bottom },
      ]}
    >
      <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
        {`Para ${nome}. O prêmio nasce aprovado, sem foto, e ${nome.split(" ")[0]} decide se posta.`}
      </Text>
      <EscolhaDaReivindicacao
        itens={itens.data}
        valor={escolha}
        onChange={(nova) => {
          conceder.reset();
          setEscolha(nova);
        }}
        fontes={fontes}
        quem="instituicao"
      />
      {conceder.isError && (
        <AvisoDeErro mensagem={mensagemDaConcessao(conceder.error)} />
      )}
      <Button
        label={conceder.isPending ? "Concedendo..." : "Conceder prêmio"}
        disabled={!pronto || conceder.isPending}
        onPress={() =>
          conceder.mutate({
            item: item!.codigo,
            evento: evento!,
            materiaId: item!.exigeMateria ? escolha.materiaId : null,
          })
        }
      />
    </ScrollView>
  );
}

// A mensagem da recusa vem da tabela do app, escrita para o aluno que reivindica. A do prêmio
// repetido fala do aluno, aqui.
function mensagemDaConcessao(erro: unknown): string {
  const falha = falhaParaTela(erro);
  return falha.codigo === "premio-ja-reivindicado"
    ? "O aluno já tem esse prêmio nesse evento."
    : falha.mensagem;
}

const styles = StyleSheet.create({
  margem: { paddingHorizontal: espacamento.margemLateral },
  coluna: { gap: 16 },
  espera: { marginTop: 32 },
});
