import { useRouter } from "expo-router";
import { useState } from "react";
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { useEu } from "../api/useEu";
import { FilterChip } from "../components/FilterChip";
import { Icon } from "../components/Icon";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { Logo } from "../marca/Logo";
import { useNovidades } from "../notificacoes/api";
import { Sininho } from "../notificacoes/Sininho";
import { fontFamily, textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, interacao, raios, tamanhos } from "../theme/tokens";
import { useLerTodos, useMural, type EscopoDoAviso } from "./api";
import { CartaoDeAviso } from "./CartaoDeAviso";

// Os chips da prancheta. "Professores" é o escopo turma: os avisos que os professores
// mandam para as turmas.
const FILTROS: { escopo: EscopoDoAviso | null; rotulo: string }[] = [
  { escopo: null, rotulo: "Todos" },
  { escopo: "faculdade", rotulo: "Faculdade" },
  { escopo: "curso", rotulo: "Curso" },
  { escopo: "turma", rotulo: "Professores" },
];

const VAZIO: Record<string, string> = {
  todos: "Nenhum aviso ainda.",
  faculdade: "Nenhum aviso da faculdade.",
  curso: "Nenhum aviso do curso.",
  turma: "Nenhum aviso dos professores.",
};

// A aba Mural (produto.md 7 e prancheta mural-*.png): o mural combinado da faculdade, do
// curso e das turmas, do mais novo ao mais antigo, somente leitura. O "+" do professor abre
// o novo aviso para as turmas dele; o sininho abre as notificações, como no feed.
export function MuralScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const eu = useEu();
  const ehProfessor = eu.data?.papeis.includes("professor") ?? false;
  const [escopo, setEscopo] = useState<EscopoDoAviso | null>(null);
  const mural = useMural(escopo);
  const lerTodos = useLerTodos();
  const [atualizando, setAtualizando] = useState(false);

  const novidades = useNovidades();
  const avisos = mural.data?.pages.flatMap((pagina) => pagina.itens) ?? [];

  // "Ler todos" marca o mural inteiro, não só o filtro do chip: segue a bolinha da aba, que
  // é do mural inteiro. Sem a resposta das novidades, vale o que a lista mostra.
  const temNaoLido =
    novidades.data?.mural ?? avisos.some((aviso) => !aviso.lido);

  const atualizar = async () => {
    setAtualizando(true);
    try {
      await mural.refetch();
    } finally {
      setAtualizando(false);
    }
  };

  const cabecalho = (
    <View>
      <View style={styles.topo}>
        <Logo largura={110} />
        <View style={styles.acoes}>
          {ehProfessor && (
            <Pressable
              role="button"
              accessibilityLabel="Novo aviso"
              hitSlop={8}
              onPress={() => router.push("/novo-aviso")}
              style={styles.botao}
            >
              <Icon name="novo" color={colors.ink} size={tamanhos.iconeNav} />
            </Pressable>
          )}
          <Sininho />
        </View>
      </View>
      <View style={styles.linhaDoTitulo}>
        <Text role="heading" style={[textStyles.titulo, { color: colors.ink }]}>
          Mural
        </Text>
        <Pressable
          role="button"
          accessibilityLabel="Ler todos os avisos"
          disabled={!temNaoLido || lerTodos.isPending}
          onPress={() => lerTodos.mutate()}
          style={({ pressed }) => [
            styles.toqueLerTodos,
            pressed && { opacity: interacao.opacidadePressionado },
          ]}
        >
          <View
            style={[
              styles.lerTodos,
              { backgroundColor: colors.surface },
              !temNaoLido && { backgroundColor: colors.surface2 },
            ]}
          >
            <Icon
              name="lerTodos"
              color={temNaoLido ? colors.ink : colors.ink3}
              size={18}
            />
            <Text
              style={[
                styles.textoLerTodos,
                { color: temNaoLido ? colors.ink : colors.ink3 },
              ]}
            >
              Ler todos
            </Text>
          </View>
        </Pressable>
      </View>
      {lerTodos.isError && (
        <Text
          role="alert"
          style={[
            textStyles.legenda,
            styles.erroLerTodos,
            { color: colors.ink },
          ]}
        >
          {falhaParaTela(lerTodos.error).mensagem}
        </Text>
      )}
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.chips}
      >
        {FILTROS.map((filtro) => (
          <FilterChip
            key={filtro.rotulo}
            role="radio"
            label={filtro.rotulo}
            selected={escopo === filtro.escopo}
            onPress={() => setEscopo(filtro.escopo)}
          />
        ))}
      </ScrollView>
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
        testID="mural"
        data={avisos}
        keyExtractor={(aviso) => aviso.id}
        renderItem={({ item }) => (
          <View style={styles.item}>
            <CartaoDeAviso
              aviso={item}
              onAbrir={() => router.push(`/aviso/${item.id}`)}
            />
          </View>
        )}
        ListHeaderComponent={cabecalho}
        ListEmptyComponent={
          mural.isPending ? (
            <ActivityIndicator style={styles.espera} color={colors.ink2} />
          ) : mural.isError ? (
            <View style={styles.estado}>
              <ErrorState
                message={falhaParaTela(mural.error).mensagem}
                onRetry={() => void mural.refetch()}
              />
            </View>
          ) : (
            <View style={styles.estado}>
              <EmptyState
                icon="newspaper"
                title={VAZIO[escopo ?? "todos"]!}
                message="Os avisos da faculdade, do curso e das turmas aparecem aqui."
              />
            </View>
          )
        }
        ListFooterComponent={
          <View style={styles.rodape}>
            {mural.isFetchingNextPage && (
              <ActivityIndicator color={colors.ink2} />
            )}
            {mural.isError && avisos.length > 0 && (
              <ErrorState
                message={falhaParaTela(mural.error).mensagem}
                onRetry={() => void mural.fetchNextPage()}
              />
            )}
          </View>
        }
        onEndReachedThreshold={0.5}
        onEndReached={() => {
          if (
            mural.hasNextPage &&
            !mural.isFetchingNextPage &&
            !mural.isError
          ) {
            void mural.fetchNextPage();
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
    minHeight: tamanhos.alvoDeToque + 16,
  },
  linhaDoTitulo: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingHorizontal: espacamento.margemLateral,
    paddingTop: 4,
  },
  acoes: { flexDirection: "row", alignItems: "center" },
  botao: {
    width: tamanhos.alvoDeToque,
    height: tamanhos.alvoDeToque,
    alignItems: "center",
    justifyContent: "center",
    marginRight: 4,
  },
  toqueLerTodos: {
    minHeight: tamanhos.alvoDeToque,
    justifyContent: "center",
  },
  lerTodos: {
    flexDirection: "row",
    alignItems: "center",
    gap: 6,
    height: tamanhos.botaoDiscreto.altura,
    paddingHorizontal: 12,
    borderRadius: raios.botoes,
  },
  textoLerTodos: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 700 }),
    fontSize: tamanhos.botaoDiscreto.texto,
  },
  erroLerTodos: {
    paddingHorizontal: espacamento.margemLateral,
    textAlign: "right",
  },
  chips: {
    gap: 8,
    paddingHorizontal: espacamento.margemLateral,
    paddingVertical: 4,
  },
  item: {
    paddingHorizontal: espacamento.margemLateral,
    paddingTop: 8,
  },
  espera: { marginTop: 48 },
  estado: { paddingHorizontal: espacamento.margemLateral, paddingTop: 32 },
  rodape: { paddingVertical: espacamento.respiroAntesDaNavegacao },
});
