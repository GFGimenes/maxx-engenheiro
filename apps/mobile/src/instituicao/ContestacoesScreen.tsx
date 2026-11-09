import { ActivityIndicator, FlatList, StyleSheet, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento } from "../theme/tokens";
import { useFilaDeContestacoes } from "./api";
import { CartaoDeContestacao } from "./CartaoDeContestacao";
import { TelaDaArea } from "./TelaDaArea";

// A fila de contestações da instituição (produto.md 4.7): os prêmios em averiguação, das
// contestações mais antigas para as mais novas, com a marca de 7 dias e sem decisão
// automática por decurso de prazo. Quem decide é sempre a instituição.
export function ContestacoesScreen() {
  return (
    <TelaDaArea titulo="Contestações" voltarPara="/instituicao">
      <Fila />
    </TelaDaArea>
  );
}

function Fila() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  // Só monta quando a área aparece: nos outros casos o TelaDaArea mostra a explicação, e a
  // fila nem é pedida.
  const fila = useFilaDeContestacoes();
  const contestacoes = fila.data?.pages.flatMap((pagina) => pagina.itens) ?? [];

  const vazio = fila.isPending ? (
    <ActivityIndicator style={styles.espera} color={colors.ink2} />
  ) : fila.isError ? (
    <ErrorState
      message={falhaParaTela(fila.error).mensagem}
      onRetry={() => void fila.refetch()}
    />
  ) : (
    <EmptyState
      icon="inbox"
      title="Nenhuma contestação aberta."
      message="Os prêmios contestados por professores e pela instituição aparecem aqui, dos mais antigos para os mais novos."
    />
  );

  return (
    <FlatList
      style={styles.fill}
      data={contestacoes}
      keyExtractor={(contestacao) => contestacao.id}
      renderItem={({ item }) => <CartaoDeContestacao contestacao={item} />}
      ListEmptyComponent={<View style={styles.estado}>{vazio}</View>}
      ListFooterComponent={
        <View
          style={{
            height: espacamento.respiroAntesDaNavegacao + insets.bottom,
          }}
        >
          {fila.isFetchingNextPage && <ActivityIndicator color={colors.ink2} />}
        </View>
      }
      onEndReachedThreshold={0.5}
      onEndReached={() => {
        if (fila.hasNextPage && !fila.isFetchingNextPage) {
          void fila.fetchNextPage();
        }
      }}
    />
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  espera: { marginTop: 32 },
  estado: { paddingTop: 32, paddingHorizontal: espacamento.margemLateral },
});
