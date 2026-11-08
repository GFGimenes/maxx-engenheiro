import { useLocalSearchParams, useRouter } from "expo-router";
import { useState } from "react";
import {
  ActivityIndicator,
  FlatList,
  KeyboardAvoidingView,
  Pressable,
  Platform,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { useEu } from "../api/useEu";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { Avatar } from "../components/Avatar";
import { Button } from "../components/Button";
import { Cabecalho } from "../components/Cabecalho";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { TextField } from "../components/TextField";
import { TextLink } from "../components/TextLink";
import {
  useComentar,
  useComentarios,
  useRemoverComentario,
  type Comentario,
} from "../feed/api";
import { perfilDe } from "../feed/CartaoDePost";
import { tempoDesde, usuarioDoArroba } from "../feed/textos";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento } from "../theme/tokens";

export const LIMITE_DO_COMENTARIO = 500;

const caracteres = (texto: string) => Array.from(texto).length;

// Os comentários de um post (produto.md 6.1), do mais antigo ao mais novo, como no
// Instagram, com o campo de comentar embaixo. Apagam o autor do comentário e a instituição;
// o dono do post não apaga comentário de terceiro. Sem prancheta: segue a identidade visual.
export function ComentariosScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const eu = useEu();
  const comentarios = useComentarios(id);
  const comentar = useComentar(id);
  const remover = useRemoverComentario(id);
  const [texto, setTexto] = useState("");

  const papeis = eu.data?.papeis ?? [];
  const interage = papeis.includes("aluno") || papeis.includes("professor");
  const instituicao = papeis.includes("instituicao");
  const lista = comentarios.data?.pages.flatMap((pagina) => pagina.itens) ?? [];
  const longo = caracteres(texto) > LIMITE_DO_COMENTARIO;
  const naoExiste =
    comentarios.isError &&
    falhaParaTela(comentarios.error).codigo === "post-nao-encontrado";

  const enviar = () =>
    comentar.mutate(texto, {
      onSuccess: () => {
        setTexto("");
        void comentarios.refetch();
      },
    });

  const linha = ({ item }: { item: Comentario }) => {
    const proprio = item.autor.id === eu.data?.id;
    const acao = proprio ? "Apagar" : instituicao ? "Remover" : null;
    return (
      <View testID={`comentario-${item.id}`} style={styles.linha}>
        <Pressable
          role="link"
          accessibilityLabel={`Perfil de ${item.autor.nomeDeExibicao}`}
          onPress={() => router.push(perfilDe(item.autor.id, eu.data))}
        >
          <Avatar
            nome={item.autor.nomeDeExibicao}
            foto={item.autor.foto}
            size={32}
          />
        </Pressable>
        <View style={styles.conteudo}>
          <Text style={[textStyles.corpo, { color: colors.ink }]}>
            <Text style={textStyles.secao}>
              {usuarioDoArroba(item.autor.arroba)}
            </Text>
            {` ${item.texto}`}
          </Text>
          <View style={styles.rodapeDaLinha}>
            <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
              {tempoDesde(item.criadoEm)}
            </Text>
            {acao && (
              <TextLink
                label={acao}
                disabled={remover.isPending}
                onPress={() =>
                  remover.mutate(item.id, {
                    onSuccess: () => void comentarios.refetch(),
                  })
                }
              />
            )}
          </View>
        </View>
      </View>
    );
  };

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === "ios" ? "padding" : undefined}
      style={[
        styles.fill,
        { backgroundColor: colors.bg, paddingTop: insets.top },
      ]}
    >
      <View style={styles.margem}>
        <Cabecalho titulo="Comentários" voltarPara={`/post/${id}`} />
      </View>
      <FlatList
        style={styles.fill}
        contentContainerStyle={styles.margem}
        data={lista}
        keyExtractor={(comentario) => comentario.id}
        renderItem={linha}
        ListEmptyComponent={
          comentarios.isPending ? (
            <ActivityIndicator style={styles.espera} color={colors.ink2} />
          ) : naoExiste ? (
            <EmptyState icon="message" title="Esse post não existe mais." />
          ) : comentarios.isError ? (
            <ErrorState
              message={falhaParaTela(comentarios.error).mensagem}
              onRetry={() => void comentarios.refetch()}
            />
          ) : (
            <EmptyState
              icon="message"
              title="Nenhum comentário ainda."
              message="Comece a conversa."
            />
          )
        }
        ListFooterComponent={
          comentarios.isFetchingNextPage ? (
            <ActivityIndicator color={colors.ink2} />
          ) : undefined
        }
        onEndReachedThreshold={0.5}
        onEndReached={() => {
          if (comentarios.hasNextPage && !comentarios.isFetchingNextPage) {
            void comentarios.fetchNextPage();
          }
        }}
      />
      {remover.isError && (
        <View style={styles.margem}>
          <AvisoDeErro mensagem={falhaParaTela(remover.error).mensagem} />
        </View>
      )}
      {interage && !naoExiste && (
        <View
          style={[
            styles.campo,
            {
              borderTopColor: colors.line,
              paddingBottom: espacamento.base * 3 + insets.bottom,
            },
          ]}
        >
          <TextField
            label="Comentário"
            multiline
            value={texto}
            onChangeText={(novo) => {
              comentar.reset();
              setTexto(novo);
            }}
            placeholder="Adicione um comentário."
            error={longo ? "O comentário passou de 500 caracteres." : undefined}
          />
          {comentar.isError && (
            <AvisoDeErro mensagem={falhaParaTela(comentar.error).mensagem} />
          )}
          <Button
            label={comentar.isPending ? "Publicando..." : "Publicar"}
            disabled={texto.trim() === "" || longo || comentar.isPending}
            onPress={enviar}
          />
        </View>
      )}
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  margem: { paddingHorizontal: espacamento.margemLateral },
  espera: { marginTop: 32 },
  linha: { flexDirection: "row", gap: 12, paddingVertical: 10 },
  conteudo: { flex: 1, gap: 2 },
  rodapeDaLinha: { flexDirection: "row", alignItems: "center", gap: 16 },
  campo: {
    borderTopWidth: 1,
    paddingTop: 12,
    paddingHorizontal: espacamento.margemLateral,
    gap: 8,
  },
});
