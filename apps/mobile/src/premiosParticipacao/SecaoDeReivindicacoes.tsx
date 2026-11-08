import { useRouter } from "expo-router";
import { Pressable, StyleSheet, Text, View } from "react-native";

import { falhaParaTela } from "../api/FalhaDaApi";
import { ErrorState } from "../components/StateMessage";
import { Medalhao } from "../premios/Medalhao";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import type { Tier } from "../theme/tokens";
import { useReivindicacoes, type Reivindicacao } from "./api";
import { MarcaDeEstado } from "./MarcaDeEstado";
import { tempoDesde } from "../feed/textos";
import { rotulosDeEstado } from "./textos";

// A seção Reivindicações da aba Troféus (D9): os prêmios de participação do aluno que ainda
// não pontuam, pendentes, em averiguação, reprovados e revogados, com o motivo nos dois
// últimos. Só o próprio aluno vê (produto.md 9.1). Sem prancheta: linhas como as do catálogo,
// com o medalhão apagado, porque nenhum desses vale pontos agora. Some quando não há nada.
export function SecaoDeReivindicacoes({ ativo }: { ativo: boolean }) {
  const { colors } = useTheme();
  const reivindicacoes = useReivindicacoes(ativo);

  if (reivindicacoes.isError) {
    return (
      <ErrorState
        message={falhaParaTela(reivindicacoes.error).mensagem}
        onRetry={() => void reivindicacoes.refetch()}
      />
    );
  }

  if (!reivindicacoes.data || reivindicacoes.data.length === 0) return null;

  return (
    <View style={styles.secao}>
      <Text role="heading" style={[textStyles.secao, { color: colors.ink }]}>
        Reivindicações
      </Text>
      {reivindicacoes.data.map((reivindicacao) => (
        <LinhaDeReivindicacao
          key={reivindicacao.premioId}
          reivindicacao={reivindicacao}
        />
      ))}
    </View>
  );
}

function LinhaDeReivindicacao({
  reivindicacao,
}: {
  reivindicacao: Reivindicacao;
}) {
  const { colors } = useTheme();
  const router = useRouter();
  const origem = [reivindicacao.evento, reivindicacao.materia]
    .filter(Boolean)
    .join(" · ");
  const postId = reivindicacao.postId;

  return (
    <Pressable
      testID={`reivindicacao-${reivindicacao.premioId}`}
      role={postId ? "link" : undefined}
      disabled={!postId}
      accessibilityLabel={[
        reivindicacao.nomeDoItem,
        origem,
        rotulosDeEstado[reivindicacao.estado],
        reivindicacao.motivo,
      ]
        .filter(Boolean)
        .join(", ")}
      onPress={() => postId && router.push(`/post/${postId}`)}
      style={({ pressed }) => [
        styles.linha,
        { borderBottomColor: colors.line },
        pressed && styles.pressionado,
      ]}
    >
      <View style={styles.apagado}>
        <Medalhao tier={reivindicacao.tier as Tier} glifo="taca" />
      </View>
      <View style={styles.texto}>
        <Text style={[textStyles.secao, { color: colors.ink }]}>
          {reivindicacao.nomeDoItem}
        </Text>
        <Text
          numberOfLines={1}
          style={[textStyles.legenda, { color: colors.ink2 }]}
        >
          {origem}
        </Text>
        <View style={styles.estado}>
          <MarcaDeEstado estado={reivindicacao.estado} />
          <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
            {tempoDesde(reivindicacao.desde)}
          </Text>
        </View>
        {reivindicacao.motivo && (
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            {`Motivo: ${reivindicacao.motivo}`}
          </Text>
        )}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  secao: { gap: 4, marginBottom: 16 },
  linha: {
    flexDirection: "row",
    gap: 12,
    paddingVertical: 12,
    borderBottomWidth: 1,
  },
  apagado: { opacity: 0.45 },
  texto: { flex: 1, gap: 4 },
  estado: { flexDirection: "row", alignItems: "center", gap: 8 },
  pressionado: { opacity: 0.85 },
});
