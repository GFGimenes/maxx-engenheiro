import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "expo-router";
import { useState } from "react";
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  RefreshControl,
  StyleSheet,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { useEu } from "../api/useEu";
import { Icon } from "../components/Icon";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { Logo } from "../marca/Logo";
import { Sininho } from "../notificacoes/Sininho";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, tamanhos } from "../theme/tokens";
import { useFeed } from "./api";
import { CartaoDePost } from "./CartaoDePost";

// A Home (produto.md 6.2 e prancheta do feed): o logo, o "+" do novo post e o sininho das
// notificações (passo 10) no topo, e os posts de quem a pessoa segue intercalados com
// recomendações, rolando sem fim.
export function FeedScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const queryClient = useQueryClient();
  const eu = useEu();
  const feed = useFeed();
  const [atualizando, setAtualizando] = useState(false);

  const ehAluno = eu.data?.papeis.includes("aluno") ?? false;
  const posts = feed.data?.pages.flatMap((pagina) => pagina.itens) ?? [];

  // Puxar para atualizar começa uma sessão nova: outra semente, outra ordem de recomendações.
  const atualizar = async () => {
    setAtualizando(true);
    try {
      await queryClient.resetQueries({ queryKey: ["feed"] });
    } finally {
      setAtualizando(false);
    }
  };

  const cabecalho = (
    <View style={styles.topo}>
      <Logo largura={110} />
      <View style={styles.acoes}>
        {ehAluno && (
          <Pressable
            role="button"
            accessibilityLabel="Novo post"
            hitSlop={8}
            onPress={() => router.push("/novo-post")}
            style={styles.botao}
          >
            <Icon name="novo" color={colors.ink} size={tamanhos.iconeNav} />
          </Pressable>
        )}
        <Sininho />
      </View>
    </View>
  );

  return (
    <View
      style={[
        styles.fill,
        { paddingTop: insets.top, backgroundColor: colors.bg },
      ]}
    >
      <FlatList
        testID="feed"
        data={posts}
        keyExtractor={(post) => post.id}
        renderItem={({ item }) => (
          <CartaoDePost
            post={item}
            leitor={eu.data}
            onRemovido={() =>
              void queryClient.invalidateQueries({ queryKey: ["feed"] })
            }
          />
        )}
        ListHeaderComponent={cabecalho}
        ListEmptyComponent={
          feed.isPending ? (
            <ActivityIndicator style={styles.espera} color={colors.ink2} />
          ) : feed.isError ? (
            <View style={styles.estado}>
              <ErrorState
                message={falhaParaTela(feed.error).mensagem}
                onRetry={() => void feed.refetch()}
              />
            </View>
          ) : (
            <View style={styles.estado}>
              <EmptyState
                icon="home"
                title="Nenhum post ainda."
                message="Quando alguém publicar, o post aparece aqui."
                action={
                  ehAluno
                    ? {
                        label: "Novo post",
                        onPress: () => router.push("/novo-post"),
                      }
                    : undefined
                }
              />
            </View>
          )
        }
        ListFooterComponent={
          <View style={styles.rodape}>
            {feed.isFetchingNextPage && (
              <ActivityIndicator color={colors.ink2} />
            )}
            {feed.isError && posts.length > 0 && (
              <ErrorState
                message={falhaParaTela(feed.error).mensagem}
                onRetry={() => void feed.fetchNextPage()}
              />
            )}
          </View>
        }
        onEndReachedThreshold={0.5}
        onEndReached={() => {
          if (feed.hasNextPage && !feed.isFetchingNextPage && !feed.isError) {
            void feed.fetchNextPage();
          }
        }}
        refreshControl={
          <RefreshControl
            refreshing={atualizando}
            onRefresh={() => void atualizar()}
            tintColor={colors.ink2}
          />
        }
      />
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  topo: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingHorizontal: espacamento.margemLateral,
    paddingVertical: 8,
  },
  acoes: { flexDirection: "row", alignItems: "center" },
  botao: {
    width: tamanhos.alvoDeToque,
    height: tamanhos.alvoDeToque,
    alignItems: "center",
    justifyContent: "center",
    marginRight: 4,
  },
  espera: { marginTop: 48 },
  estado: { paddingHorizontal: espacamento.margemLateral, paddingTop: 48 },
  rodape: { paddingVertical: espacamento.respiroAntesDaNavegacao },
});
