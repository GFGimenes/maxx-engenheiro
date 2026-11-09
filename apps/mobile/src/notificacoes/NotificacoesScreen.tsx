import { useRouter, type Href } from "expo-router";
import { useEffect, useRef } from "react";
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { Cabecalho } from "../components/Cabecalho";
import { Icon } from "../components/Icon";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { tempoDesde } from "../feed/textos";
import { fontFamily, textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, interacao, raios, tamanhos } from "../theme/tokens";
import { useLerNotificacoes, useNotificacoes, type Notificacao } from "./api";
import { apresentar } from "./textos";

// O sininho aberto (produto.md 8). Sem prancheta: segue a identidade visual e a atividade do
// Instagram, uma linha por notificação, da mais nova à mais antiga. Abrir marca tudo como
// lido (passo 10, pergunta 6): o que estava não lido continua em negrito, com a bolinha,
// enquanto a tela está aberta; a próxima visita já mostra lido.
export function NotificacoesScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const notificacoes = useNotificacoes();
  const ler = useLerNotificacoes();
  const lidaAoAbrir = useRef(false);

  const itens =
    notificacoes.data?.pages.flatMap((pagina) => pagina.itens) ?? [];
  const maisNova = itens[0]?.criadaEm;
  const temNaoLida = itens.some((notificacao) => !notificacao.lida);
  const { mutate } = ler;
  useEffect(() => {
    if (!lidaAoAbrir.current && maisNova) {
      lidaAoAbrir.current = true;
      if (temNaoLida) {
        mutate(maisNova);
      }
    }
  }, [maisNova, temNaoLida, mutate]);

  return (
    <View
      style={[
        styles.fill,
        { paddingTop: insets.top, backgroundColor: colors.bg },
      ]}
    >
      <FlatList
        testID="notificacoes"
        data={itens}
        keyExtractor={(notificacao) => notificacao.id}
        ListHeaderComponent={
          <View style={styles.margem}>
            <Cabecalho titulo="Notificações" voltarPara="/" />
          </View>
        }
        renderItem={({ item }) => (
          <LinhaDeNotificacao
            notificacao={item}
            onPress={(destino) => router.push(destino as Href)}
          />
        )}
        ListEmptyComponent={
          notificacoes.isPending ? (
            <ActivityIndicator style={styles.espera} color={colors.ink2} />
          ) : notificacoes.isError ? (
            <View style={styles.margem}>
              <ErrorState
                message={falhaParaTela(notificacoes.error).mensagem}
                onRetry={() => void notificacoes.refetch()}
              />
            </View>
          ) : (
            <View style={styles.margem}>
              <EmptyState
                icon="sino"
                title="Nenhuma notificação ainda."
                message="Medalhas, prêmios, conquistas e avisos novos aparecem aqui."
              />
            </View>
          )
        }
        ListFooterComponent={
          <View style={styles.rodape}>
            {notificacoes.isFetchingNextPage && (
              <ActivityIndicator color={colors.ink2} />
            )}
          </View>
        }
        onEndReachedThreshold={0.5}
        onEndReached={() => {
          if (
            notificacoes.hasNextPage &&
            !notificacoes.isFetchingNextPage &&
            !notificacoes.isError
          ) {
            void notificacoes.fetchNextPage();
          }
        }}
      />
    </View>
  );
}

function LinhaDeNotificacao({
  notificacao,
  onPress,
}: {
  notificacao: Notificacao;
  onPress: (destino: string) => void;
}) {
  const { colors } = useTheme();
  const { texto, icone, destino } = apresentar(notificacao);
  const naoLida = !notificacao.lida;
  const quando = tempoDesde(notificacao.criadaEm);

  return (
    <Pressable
      role={destino ? "button" : undefined}
      disabled={!destino}
      accessibilityLabel={`${texto} ${quando}${naoLida ? ", não lida" : ""}`}
      onPress={() => destino && onPress(destino)}
      style={({ pressed }) => [
        styles.linha,
        pressed && { opacity: interacao.opacidadePressionado },
      ]}
    >
      <View
        testID={`notificacao-${notificacao.id}`}
        style={[styles.icone, { backgroundColor: colors.surface }]}
      >
        <Icon name={icone} color={colors.ink} size={22} />
      </View>
      <View style={styles.textos}>
        <Text
          style={[
            textStyles.corpo,
            naoLida && styles.negrito,
            { color: colors.ink },
          ]}
        >
          {texto}
        </Text>
        <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
          {quando}
        </Text>
      </View>
      {naoLida && (
        <View
          testID="bolinha-nao-lida"
          style={[styles.bolinha, { backgroundColor: colors.gold }]}
        />
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  margem: { paddingHorizontal: espacamento.margemLateral },
  espera: { marginTop: 32 },
  rodape: { paddingVertical: espacamento.respiroAntesDaNavegacao },
  linha: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    paddingHorizontal: espacamento.margemLateral,
    paddingVertical: 12,
    minHeight: tamanhos.alvoDeToque + 16,
  },
  icone: {
    width: 44,
    height: 44,
    borderRadius: raios.pill,
    alignItems: "center",
    justifyContent: "center",
  },
  textos: { flex: 1, gap: 2 },
  negrito: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 700 }),
  },
  bolinha: {
    width: tamanhos.bolinhaNovidade.diametro,
    height: tamanhos.bolinhaNovidade.diametro,
    borderRadius: tamanhos.bolinhaNovidade.diametro / 2,
  },
});
