import { useState } from "react";
import {
  ActivityIndicator,
  FlatList,
  ScrollView,
  StyleSheet,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { useEu } from "../api/useEu";
import { Cabecalho } from "../components/Cabecalho";
import { FilterChip } from "../components/FilterChip";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento } from "../theme/tokens";
import { useEventosDaFila, useFilaDeComprovacoes } from "./api";
import { CartaoDeComprovacao } from "./CartaoDeComprovacao";

// A fila de comprovações do professor (produto.md 4.7, D8): as reivindicações dos alunos
// dele no semestre vigente, das mais antigas para as mais novas, com a marca de 7 dias e
// sem decisão automática. Chips de evento no topo, pensando na aprovação em lote da fase 2.
// Sem prancheta: segue a identidade visual, com cartões como os do feed.
export function ComprovacoesScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const eu = useEu();
  const aprova =
    eu.data?.papeis.some(
      (papel) => papel === "professor" || papel === "instituicao",
    ) ?? false;
  const instituicao = eu.data?.papeis.includes("instituicao") ?? false;
  // A instituição chega pela área dela (passo 12); o professor, pelo menu do perfil.
  const voltarPara = instituicao ? "/instituicao" : "/perfil";
  const [eventoId, setEventoId] = useState<string | null>(null);
  const eventos = useEventosDaFila();
  const fila = useFilaDeComprovacoes(eventoId);
  const comprovacoes = fila.data?.pages.flatMap((pagina) => pagina.itens) ?? [];

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
      title={
        eventoId
          ? "Nada pendente neste evento."
          : "Nenhuma comprovação pendente."
      }
      message={
        instituicao
          ? "As reivindicações dos alunos aparecem aqui, das mais antigas para as mais novas."
          : "As reivindicações dos seus alunos aparecem aqui, das mais antigas para as mais novas."
      }
    />
  );

  if (eu.isSuccess && !aprova) {
    return (
      <View
        style={[
          styles.fill,
          { paddingTop: insets.top, backgroundColor: colors.bg },
        ]}
      >
        <View style={styles.margem}>
          <Cabecalho titulo="Comprovações" voltarPara={voltarPara} />
        </View>
        <EmptyState
          icon="inbox"
          title="Comprovações são para quem aprova."
          message="Professores e a instituição decidem as reivindicações dos alunos."
        />
      </View>
    );
  }

  return (
    <View
      style={[
        styles.fill,
        { paddingTop: insets.top, backgroundColor: colors.bg },
      ]}
    >
      <View style={[styles.margem, styles.topo]}>
        <Cabecalho titulo="Comprovações" voltarPara={voltarPara} />
        {(eventos.data?.length ?? 0) > 0 && (
          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            contentContainerStyle={styles.chips}
          >
            <FilterChip
              label="Todos"
              role="radio"
              selected={eventoId === null}
              onPress={() => setEventoId(null)}
            />
            {eventos.data!.map((evento) => (
              <FilterChip
                key={evento.id}
                label={`${evento.nome} (${evento.pendentes})`}
                role="radio"
                selected={eventoId === evento.id}
                onPress={() =>
                  setEventoId((atual) =>
                    atual === evento.id ? null : evento.id,
                  )
                }
              />
            ))}
          </ScrollView>
        )}
      </View>
      <FlatList
        style={styles.fill}
        data={comprovacoes}
        keyExtractor={(comprovacao) => comprovacao.id}
        renderItem={({ item }) => <CartaoDeComprovacao comprovacao={item} />}
        ListEmptyComponent={<View style={styles.estado}>{vazio}</View>}
        ListFooterComponent={
          <View
            style={{
              height: espacamento.respiroAntesDaNavegacao + insets.bottom,
            }}
          >
            {fila.isFetchingNextPage && (
              <ActivityIndicator color={colors.ink2} />
            )}
          </View>
        }
        onEndReachedThreshold={0.5}
        onEndReached={() => {
          if (fila.hasNextPage && !fila.isFetchingNextPage) {
            void fila.fetchNextPage();
          }
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  margem: { paddingHorizontal: espacamento.margemLateral },
  topo: { gap: 4, paddingBottom: 8 },
  chips: { gap: 8 },
  espera: { marginTop: 32 },
  estado: { paddingTop: 32, paddingHorizontal: espacamento.margemLateral },
});
