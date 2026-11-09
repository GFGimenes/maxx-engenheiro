import { useRouter } from "expo-router";
import { useState } from "react";
import {
  ActivityIndicator,
  FlatList,
  Pressable,
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
import { SegmentedControl } from "../components/SegmentedControl";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { perfilDe } from "../feed/CartaoDePost";
import { formatarNumero } from "../feed/textos";
import { Logo } from "../marca/Logo";
import { Sininho } from "../notificacoes/Sininho";
import { usePerfil } from "../perfil/api";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, raios, tamanhos } from "../theme/tokens";
import {
  rotuloDoSemestre,
  useRanking,
  visoesPara,
  VISOES,
  type LinhaDoRanking as Linha,
  type Ranking,
  type Visao,
} from "./api";
import { LinhaDoRanking } from "./LinhaDoRanking";

type Filtro = "todos" | "seguidos";

const FILTROS = [
  { value: "todos", label: "Todos" },
  { value: "seguidos", label: "Quem eu sigo" },
] as const;

// Rankings (produto.md 5, prancheta rankings-*.png). Sem aba própria: abre pela posição no
// perfil e pelos atalhos da Busca (produto.md 10). Os chips trocam a visão; o card mostra a
// posição do próprio aluno, e a lista, o topo por cursor, com a linha dele destacada. O
// segmentado filtra quem o aluno segue sem mudar a posição de ninguém. Fica na pilha sobre
// as abas, como as outras telas internas (decisão do passo 7).
export function RankingsScreen({ inicial }: { inicial: Visao }) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const eu = useEu();
  const aluno =
    eu.data !== undefined &&
    eu.data.papeis.length === 1 &&
    eu.data.papeis[0] === "aluno";
  const perfil = usePerfil();
  const exAluno = aluno && (perfil.data?.exAluno ?? false);
  const visoes = visoesPara({ aluno, exAluno });
  const [escolhida, setEscolhida] = useState<Visao>(inicial);
  // Enquanto não se sabe quem lê, vale a escolhida; depois, só uma que a pessoa pode ver.
  const visao =
    eu.data === undefined || visoes.includes(escolhida) ? escolhida : "geral";
  const [filtro, setFiltro] = useState<Filtro>("todos");
  const ranking = useRanking(visao, filtro === "seguidos");

  const primeira = ranking.data?.pages[0];
  const linhas = ranking.data?.pages.flatMap((pagina) => pagina.itens) ?? [];
  const semestre =
    VISOES[visao].periodo === "semestre"
      ? rotuloDoSemestre(primeira?.semestre)
      : null;

  const cabecalho = (
    <View>
      <View style={styles.topo}>
        <Logo largura={110} />
        <Sininho />
      </View>
      <View style={styles.linhaDoTitulo}>
        <Pressable
          role="button"
          accessibilityLabel="Voltar"
          hitSlop={8}
          onPress={() =>
            router.canGoBack() ? router.back() : router.replace("/perfil")
          }
          style={styles.voltar}
        >
          <Icon name="voltar" color={colors.ink} size={tamanhos.iconeNav} />
        </Pressable>
        <Text
          role="heading"
          style={[textStyles.titulo, styles.titulo, { color: colors.ink }]}
        >
          Rankings
        </Text>
        {semestre && (
          <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
            {semestre}
          </Text>
        )}
      </View>
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.chips}
        style={styles.foraDaMargem}
      >
        {visoes.map((valor) => (
          <FilterChip
            key={valor}
            label={VISOES[valor].chip}
            role="radio"
            selected={visao === valor}
            onPress={() => setEscolhida(valor)}
          />
        ))}
      </ScrollView>
      {aluno && (
        <CardDaPosicao
          ranking={primeira}
          visao={visao}
          exAluno={exAluno}
          carregando={ranking.isPending}
          onVerPremios={() => router.push("/trofeus")}
        />
      )}
      {/* Na prancheta, o rótulo e o segmentado dividem a linha; a 390 px, com "Quem eu
          sigo" e as visões de nome longo, não cabem os dois. O rótulo vem por cima. */}
      <View style={styles.filtro}>
        <Text style={[textStyles.rotulo, { color: colors.ink2 }]}>
          {`Posição no ${VISOES[visao].nome}`}
        </Text>
        <SegmentedControl
          label="Quem aparece"
          options={FILTROS}
          value={filtro}
          onChange={setFiltro}
        />
      </View>
    </View>
  );

  const vazio = ranking.isPending ? (
    <ActivityIndicator style={styles.espera} color={colors.ink2} />
  ) : ranking.isError ? (
    <ErrorState
      message={falhaParaTela(ranking.error).mensagem}
      onRetry={() => void ranking.refetch()}
    />
  ) : (
    <EmptyState
      icon="taca"
      title={
        filtro === "seguidos"
          ? "Ninguém que você segue pontuou aqui."
          : "Ninguém pontuou aqui ainda."
      }
    />
  );

  return (
    <View
      testID="rankings"
      style={[
        styles.fill,
        { paddingTop: insets.top, backgroundColor: colors.bg },
      ]}
    >
      <FlatList
        style={styles.fill}
        contentContainerStyle={styles.margem}
        data={linhas}
        keyExtractor={(linha) => linha.alunoId}
        ListHeaderComponent={cabecalho}
        renderItem={({ item }: { item: Linha }) => (
          <LinhaDoRanking
            linha={item}
            propria={item.alunoId === eu.data?.id}
            onPress={() => router.push(perfilDe(item.alunoId, eu.data))}
          />
        )}
        ListEmptyComponent={<View style={styles.estado}>{vazio}</View>}
        ListFooterComponent={
          <View style={{ height: espacamento.respiroAntesDaNavegacao }}>
            {ranking.isFetchingNextPage && (
              <ActivityIndicator color={colors.ink2} />
            )}
          </View>
        }
        onEndReachedThreshold={0.5}
        onEndReached={() => {
          if (ranking.hasNextPage && !ranking.isFetchingNextPage) {
            void ranking.fetchNextPage();
          }
        }}
      />
    </View>
  );
}

// O card "Sua posição" (prancheta): sobre o `hero`, marinho nos dois temas, a posição em ouro
// e os pontos da visão. O ouro aqui é sobre fundo escuro, nunca sobre o claro. Sem pontos na
// visão, "Sem posição ainda"; o ex-aluno, no semestre, não aparece (produto.md 2.2).
function CardDaPosicao({
  ranking,
  visao,
  exAluno,
  carregando,
  onVerPremios,
}: {
  ranking: Ranking | undefined;
  visao: Visao;
  exAluno: boolean;
  carregando: boolean;
  onVerPremios: () => void;
}) {
  const { name, colors } = useTheme();
  // No claro o hero é marinho, e o texto vai em branco (o `bg` do claro); no escuro o hero é
  // um marinho mais claro que o fundo, e o texto é o `ink` do escuro.
  const texto = name === "claro" ? colors.bg : colors.ink;
  const link = name === "claro" ? colors.bg : colors.link;
  const eu = ranking?.eu;
  const rotulo = `Sua posição · ${VISOES[visao].nome}`;

  return (
    <View
      testID="card-da-posicao"
      style={[styles.card, { backgroundColor: colors.hero }]}
    >
      {carregando ? (
        <ActivityIndicator color={texto} style={styles.fill} />
      ) : eu ? (
        <>
          <Text
            numberOfLines={1}
            adjustsFontSizeToFit
            style={[textStyles.display, styles.posicao, { color: colors.gold }]}
          >
            {`#${formatarNumero(eu.posicao)}`}
          </Text>
          <View style={styles.dadosDoCard}>
            <Text style={[textStyles.rotulo, { color: texto }]}>{rotulo}</Text>
            <Text style={[textStyles.titulo, { color: texto }]}>
              {`${formatarNumero(eu.pontos)} pts`}
            </Text>
          </View>
        </>
      ) : (
        <View style={styles.dadosDoCard}>
          <Text style={[textStyles.rotulo, { color: texto }]}>{rotulo}</Text>
          <Text style={[textStyles.secao, { color: texto }]}>
            {exAluno && VISOES[visao].periodo === "semestre"
              ? "Ex-aluno aparece só no histórico."
              : "Sem posição ainda"}
          </Text>
          {!(exAluno && VISOES[visao].periodo === "semestre") && (
            <Text style={[textStyles.legenda, { color: texto }]}>
              Quando você pontuar, sua posição aparece aqui.
            </Text>
          )}
        </View>
      )}
      <Pressable
        role="link"
        accessibilityLabel="Ver prêmios"
        hitSlop={8}
        onPress={onVerPremios}
      >
        <Text style={[textStyles.secao, styles.sublinhado, { color: link }]}>
          Ver prêmios
        </Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  margem: { paddingHorizontal: espacamento.margemLateral },
  foraDaMargem: { marginHorizontal: -espacamento.margemLateral },
  topo: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingVertical: 8,
  },
  linhaDoTitulo: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
    paddingBottom: 8,
  },
  voltar: {
    width: tamanhos.alvoDeToque,
    height: tamanhos.alvoDeToque,
    alignItems: "center",
    justifyContent: "center",
    marginLeft: -12,
  },
  titulo: { flex: 1 },
  chips: { gap: 8, paddingHorizontal: espacamento.margemLateral },
  card: {
    flexDirection: "row",
    alignItems: "center",
    gap: 16,
    minHeight: 104,
    marginTop: 12,
    padding: 16,
    borderRadius: raios.cardMaior,
  },
  posicao: { maxWidth: 120 },
  dadosDoCard: { flex: 1, gap: 4 },
  sublinhado: { textDecorationLine: "underline" },
  filtro: { gap: 8, paddingTop: 24, paddingBottom: 8 },
  espera: { marginTop: 32 },
  estado: { paddingTop: 24 },
});
