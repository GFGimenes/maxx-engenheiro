import { Pressable, StyleSheet, Text, View } from "react-native";

import { Avatar } from "../components/Avatar";
import { formatarNumero } from "../feed/textos";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { interacao, raios } from "../theme/tokens";
import type { LinhaDoRanking as Linha } from "./api";

// Largura da coluna da posição: cabe "#1.234" sem empurrar o nome.
const COLUNA_DA_POSICAO = 52;

// Uma linha do ranking (prancheta rankings-*.png): posição, avatar, nome, curso e semestre,
// pontos. A do próprio aluno vem destacada, com o fundo `highlight`, a borda dourada, o anel
// dourado no avatar e o "· você". O ex-aluno, só no histórico, leva a marca. O aluno nunca é
// pintado com cor de tier: o ranking é só posição.
export function LinhaDoRanking({
  linha,
  propria,
  onPress,
}: {
  linha: Linha;
  propria: boolean;
  onPress: () => void;
}) {
  const { colors } = useTheme();
  const descricao = [
    linha.curso,
    linha.semestre ? `${linha.semestre}º semestre` : null,
    linha.exAluno ? "não pontua mais" : null,
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <Pressable
      testID={`linha-${linha.alunoId}`}
      role="link"
      accessibilityLabel={`${linha.posicao}º, ${linha.nomeDeExibicao}${propria ? ", você" : ""}, ${linha.pontos} pontos`}
      onPress={onPress}
      style={({ pressed }) => [
        styles.linha,
        propria
          ? [
              styles.propria,
              { backgroundColor: colors.highlight, borderColor: colors.gold },
            ]
          : { borderBottomColor: colors.line },
        pressed && { opacity: interacao.opacidadePressionado },
      ]}
    >
      <Text
        numberOfLines={1}
        style={[textStyles.secao, styles.posicao, { color: colors.ink }]}
      >
        {`#${formatarNumero(linha.posicao)}`}
      </Text>
      <Avatar
        nome={linha.nomeDeExibicao}
        foto={linha.foto}
        size={44}
        anel={propria}
      />
      <View style={styles.quem}>
        <View style={styles.nome}>
          <Text
            numberOfLines={1}
            style={[textStyles.secao, styles.encolhe, { color: colors.ink }]}
          >
            {propria ? `${linha.nomeDeExibicao} · você` : linha.nomeDeExibicao}
          </Text>
          {linha.exAluno && (
            <View style={[styles.marca, { backgroundColor: colors.surface2 }]}>
              <Text style={[textStyles.rotulo, { color: colors.ink2 }]}>
                Ex-aluno
              </Text>
            </View>
          )}
        </View>
        {descricao !== "" && (
          <Text
            numberOfLines={1}
            style={[textStyles.legenda, { color: colors.ink2 }]}
          >
            {descricao}
          </Text>
        )}
      </View>
      <Text numberOfLines={1} style={styles.pontos}>
        <Text style={[textStyles.secao, { color: colors.ink }]}>
          {formatarNumero(linha.pontos)}
        </Text>
        <Text style={[textStyles.legenda, { color: colors.ink2 }]}> pts</Text>
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  linha: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    paddingVertical: 12,
    borderBottomWidth: 1,
  },
  // A borda dourada tem 2 px e a linha avança 8 px sobre a margem, como na prancheta.
  propria: {
    borderWidth: 2,
    borderRadius: raios.cardMenor,
    marginHorizontal: -8,
    paddingHorizontal: 8,
  },
  posicao: { width: COLUNA_DA_POSICAO, textAlign: "center" },
  quem: { flex: 1, gap: 2 },
  nome: { flexDirection: "row", alignItems: "center", gap: 8 },
  encolhe: { flexShrink: 1 },
  marca: {
    borderRadius: raios.filtros,
    paddingHorizontal: 6,
    paddingVertical: 2,
  },
  pontos: { textAlign: "right" },
});
