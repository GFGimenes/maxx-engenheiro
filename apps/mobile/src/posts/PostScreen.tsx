import { useLocalSearchParams, useRouter } from "expo-router";
import { ActivityIndicator, ScrollView, StyleSheet, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { useEu } from "../api/useEu";
import { Cabecalho } from "../components/Cabecalho";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { usePost } from "../feed/api";
import { CartaoDePost } from "../feed/CartaoDePost";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento } from "../theme/tokens";

// Um post só, aberto pelo link /post/:id (produto.md 6.1). A rota está atrás do login: quem
// chega sem sessão entra e volta para cá.
export function PostScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const eu = useEu();
  const post = usePost(id);
  const naoExiste =
    post.isError && falhaParaTela(post.error).codigo === "post-nao-encontrado";

  return (
    <ScrollView
      style={{ backgroundColor: colors.bg }}
      contentContainerStyle={{
        paddingTop: insets.top,
        paddingBottom: espacamento.respiroAntesDaNavegacao + insets.bottom,
      }}
    >
      <View style={styles.margem}>
        <Cabecalho titulo="Post" voltarPara="/" />
      </View>
      {post.isPending && (
        <ActivityIndicator style={styles.espera} color={colors.ink2} />
      )}
      {naoExiste && (
        <View style={styles.margem}>
          <EmptyState
            icon="imagem"
            title="Esse post não existe mais."
            message="Ele pode ter sido apagado por quem publicou."
            action={{
              label: "Ir para o início",
              onPress: () => router.dismissTo("/"),
            }}
          />
        </View>
      )}
      {post.isError && !naoExiste && (
        <View style={styles.margem}>
          <ErrorState
            message={falhaParaTela(post.error).mensagem}
            onRetry={() => void post.refetch()}
          />
        </View>
      )}
      {post.isSuccess && (
        <CartaoDePost
          post={post.data}
          leitor={eu.data}
          onRemovido={() => router.dismissTo("/")}
        />
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  margem: { paddingHorizontal: espacamento.margemLateral },
  espera: { marginTop: 32 },
});
