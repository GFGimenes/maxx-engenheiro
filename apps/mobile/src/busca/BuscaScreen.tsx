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
import { Avatar } from "../components/Avatar";
import { Button } from "../components/Button";
import { FilterChip } from "../components/FilterChip";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { TextField } from "../components/TextField";
import { perfilDe } from "../feed/CartaoDePost";
import { usePerfil } from "../perfil/api";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento } from "../theme/tokens";
import {
  temCriterio,
  useBuscaDeAlunos,
  useCursos,
  type AlunoEncontrado,
} from "./api";

// A Busca (produto.md 6.4): alunos por nome de exibição e por arroba, com filtros de curso
// e de semestre, como a busca do Instagram, e os atalhos para os rankings (passo 11).
// Sem prancheta: segue a identidade visual.
export function BuscaScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const eu = useEu();
  // Os atalhos dos rankings (produto.md 6.4): o geral para todos; o do curso só para o aluno,
  // e o ex-aluno vai direto ao histórico do curso (pergunta 6 do passo 11).
  const aluno =
    eu.data !== undefined &&
    eu.data.papeis.length === 1 &&
    eu.data.papeis[0] === "aluno";
  const exAluno = (usePerfil().data?.exAluno ?? false) && aluno;
  const cursos = useCursos();
  const [termo, setTermo] = useState("");
  const [curso, setCurso] = useState<string | null>(null);
  const [semestre, setSemestre] = useState<number | null>(null);
  // Os semestres do filtro vão do 1º ao último do curso escolhido (Medicina tem 12); sem
  // curso, até o mais longo deles.
  const duracaoDe = (sigla: string | null) =>
    sigla
      ? (cursos.data?.find((item) => item.sigla === sigla)
          ?.duracaoEmSemestres ?? 0)
      : Math.max(
          0,
          ...(cursos.data ?? []).map((item) => item.duracaoEmSemestres),
        );
  const semestres = Array.from(
    { length: duracaoDe(curso) },
    (_, indice) => indice + 1,
  );
  const filtro = { termo, curso, semestre };
  const busca = useBuscaDeAlunos(filtro);
  const alunos = busca.data?.pages.flatMap((pagina) => pagina.itens) ?? [];

  const linha = ({ item }: { item: AlunoEncontrado }) => (
    <Pressable
      testID={`aluno-${item.id}`}
      role="link"
      accessibilityLabel={`${item.nomeDeExibicao}, ${item.arroba}`}
      onPress={() => router.push(perfilDe(item.id, eu.data))}
      style={({ pressed }) => [styles.linha, pressed && styles.pressionado]}
    >
      <Avatar nome={item.nomeDeExibicao} foto={item.foto} size={44} />
      <View style={styles.texto}>
        <Text style={[textStyles.secao, { color: colors.ink }]}>
          {item.nomeDeExibicao}
        </Text>
        <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
          {[
            item.arroba,
            item.curso,
            item.semestre ? `${item.semestre}º semestre` : null,
            item.exAluno ? "Ex-aluno" : null,
          ]
            .filter(Boolean)
            .join(" · ")}
        </Text>
      </View>
      {item.euSigo && (
        <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
          Seguindo
        </Text>
      )}
    </Pressable>
  );

  const vazio = !temCriterio(filtro) ? (
    <EmptyState
      icon="search"
      title="Busque alunos."
      message="Pelo nome ou pelo arroba, com ao menos 2 letras, ou escolha um curso ou semestre."
    />
  ) : busca.isPending ? (
    <ActivityIndicator style={styles.espera} color={colors.ink2} />
  ) : busca.isError ? (
    <ErrorState
      message={falhaParaTela(busca.error).mensagem}
      onRetry={() => void busca.refetch()}
    />
  ) : (
    <EmptyState
      icon="search"
      title="Nenhum aluno encontrado."
      message="Tente outro nome ou outro filtro."
    />
  );

  return (
    <View
      style={[
        styles.fill,
        { paddingTop: insets.top, backgroundColor: colors.bg },
      ]}
    >
      <View style={[styles.margem, styles.topo]}>
        <TextField
          label="Buscar"
          value={termo}
          onChangeText={setTermo}
          placeholder="Nome ou arroba"
          autoCapitalize="none"
          autoCorrect={false}
          returnKeyType="search"
        />
        <View style={styles.atalhos}>
          <View style={styles.atalho}>
            <Button
              label="Ranking geral"
              variant="secondary"
              onPress={() => router.push("/rankings?visao=geral")}
            />
          </View>
          {aluno && (
            <View style={styles.atalho}>
              <Button
                label="Ranking do curso"
                variant="secondary"
                onPress={() =>
                  router.push(
                    `/rankings?visao=${exAluno ? "historico-curso" : "curso"}`,
                  )
                }
              />
            </View>
          )}
        </View>
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.chips}
        >
          {(cursos.data ?? []).map((item) => (
            <FilterChip
              key={item.sigla}
              label={item.sigla}
              role="radio"
              selected={curso === item.sigla}
              onPress={() => {
                const novo = curso === item.sigla ? null : item.sigla;
                setCurso(novo);
                // O semestre marcado que o curso novo não tem sai do filtro.
                if (semestre !== null && semestre > duracaoDe(novo))
                  setSemestre(null);
              }}
            />
          ))}
        </ScrollView>
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.chips}
        >
          {semestres.map((numero) => (
            <FilterChip
              key={numero}
              label={`${numero}º semestre`}
              role="radio"
              selected={semestre === numero}
              onPress={() =>
                setSemestre((atual) => (atual === numero ? null : numero))
              }
            />
          ))}
        </ScrollView>
      </View>
      <FlatList
        style={styles.fill}
        contentContainerStyle={styles.margem}
        keyboardShouldPersistTaps="handled"
        data={temCriterio(filtro) ? alunos : []}
        keyExtractor={(aluno) => aluno.id}
        renderItem={linha}
        ListEmptyComponent={<View style={styles.estado}>{vazio}</View>}
        ListFooterComponent={
          <View style={{ height: espacamento.respiroAntesDaNavegacao }}>
            {busca.isFetchingNextPage && (
              <ActivityIndicator color={colors.ink2} />
            )}
          </View>
        }
        onEndReachedThreshold={0.5}
        onEndReached={() => {
          if (busca.hasNextPage && !busca.isFetchingNextPage) {
            void busca.fetchNextPage();
          }
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  margem: { paddingHorizontal: espacamento.margemLateral },
  topo: { gap: 4, paddingTop: 8, paddingBottom: 8 },
  chips: { gap: 8 },
  atalhos: { flexDirection: "row", gap: 8, paddingTop: 4 },
  atalho: { flex: 1 },
  linha: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    paddingVertical: 10,
  },
  texto: { flex: 1, gap: 2 },
  pressionado: { opacity: 0.85 },
  espera: { marginTop: 32 },
  estado: { paddingTop: 32 },
});
