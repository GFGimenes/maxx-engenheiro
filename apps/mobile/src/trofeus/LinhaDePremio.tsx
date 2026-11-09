import { StyleSheet, Text, View } from "react-native";

import { Medalhao } from "../premios/Medalhao";
import { rotulosDeRaridade, rotulosDeTier } from "../premios/rotulos";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { tiers, type Tier } from "../theme/tokens";
import {
  detalheDaLinha,
  glifoDe,
  pontosDaLinha,
  raridadeDe,
  textoDaRaridade,
  textoDoProgresso,
  type ItemDoAluno,
} from "./montagem";

// Uma linha da lista de Troféus (prancheta trofeus-*.png): medalhão com tier e raridade,
// nome, "Tier · Raridade · x% dos alunos", o critério ou de onde veio, e os pontos. O que
// falta aparece apagado, com o valor do catálogo sem o "+". A conquista que falta na
// instância vigente ganha a linha de progresso ("2 de 3 neste bimestre"), sob o critério.
export function LinhaDePremio({ item }: { item: ItemDoAluno }) {
  const { colors, name } = useTheme();
  const tier = item.tier as Tier;
  const raridade = raridadeDe(item);
  const obtido = item.obtidos.length > 0;
  const progresso = textoDoProgresso(item);

  return (
    <View
      testID={`premio-${item.codigo}`}
      accessible
      accessibilityLabel={[
        item.nome,
        rotulosDeTier[tier],
        raridade && rotulosDeRaridade[raridade],
        obtido ? "obtido" : "não obtido",
        progresso,
      ]
        .filter(Boolean)
        .join(", ")}
      style={[styles.linha, { borderBottomColor: colors.line }]}
    >
      <View style={!obtido && styles.apagado}>
        <Medalhao tier={tier} glifo={glifoDe(item)} raridade={raridade} />
      </View>
      <View style={styles.texto}>
        <Text style={[textStyles.secao, { color: colors.ink }]}>
          {item.nome}
        </Text>
        <Text style={textStyles.legenda}>
          <Text style={{ color: tiers[tier].texto[name] }}>
            {rotulosDeTier[tier]}
          </Text>
          {raridade && item.raridade && (
            <Text style={{ color: colors.ink2 }}>
              {` · ${rotulosDeRaridade[raridade]} · ${textoDaRaridade(item.raridade.percentual)}`}
            </Text>
          )}
        </Text>
        <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
          {detalheDaLinha(item)}
        </Text>
        {progresso && (
          <Text
            testID={`progresso-${item.codigo}`}
            style={[textStyles.legenda, { color: colors.ink }]}
          >
            {progresso}
          </Text>
        )}
      </View>
      <Text
        style={[
          textStyles.pontos,
          { color: obtido ? colors.ink : colors.ink3 },
        ]}
      >
        {pontosDaLinha(item)}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  linha: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    paddingVertical: 14,
    borderBottomWidth: 1,
  },
  apagado: { opacity: 0.45 },
  texto: { flex: 1, gap: 2 },
});
