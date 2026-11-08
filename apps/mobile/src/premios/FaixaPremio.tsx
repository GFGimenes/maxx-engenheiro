import { StyleSheet, Text, View } from "react-native";

import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { raios, tiers, type Raridade, type Tier } from "../theme/tokens";
import { Medalhao, type Glifo } from "./Medalhao";
import { rotulosDeRaridade, rotulosDeTier } from "./rotulos";

interface FaixaPremioProps {
  nome: string;
  tier: Tier;
  // Nula enquanto o worker ainda não calculou a escassez do item: sai o ornamento e o texto.
  raridade?: Raridade | null;
  glifo: Glifo;
  pontos: number;
  // Sob os pontos, como "foto validada".
  nota?: string;
  // Acima do nome, como "Último troféu" no perfil.
  rotulo?: string;
  // Sob o tier, numa linha só: de onde veio o prêmio, como "Banco de Dados · 4º bimestre".
  detalhe?: string;
}

// A faixa do prêmio: medalhão, nome, tier e raridade, pontos à direita. O tier também vai em
// texto, e a raridade depois dele, porque os ornamentos sozinhos não dizem o nome.
export function FaixaPremio({
  nome,
  tier,
  raridade,
  glifo,
  pontos,
  nota,
  rotulo,
  detalhe,
}: FaixaPremioProps) {
  const { name, colors } = useTheme();

  return (
    <View
      testID="faixa"
      style={[styles.faixa, { backgroundColor: colors.surface }]}
    >
      <Medalhao tier={tier} glifo={glifo} raridade={raridade ?? undefined} />

      <View style={styles.textos}>
        {rotulo && (
          <Text style={[textStyles.rotulo, { color: colors.ink2 }]}>
            {rotulo}
          </Text>
        )}
        <Text style={[textStyles.secao, { color: colors.ink }]}>{nome}</Text>
        <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
          <Text style={[styles.tier, { color: tiers[tier].texto[name] }]}>
            {rotulosDeTier[tier]}
          </Text>
          {raridade ? ` · ${rotulosDeRaridade[raridade]}` : ""}
        </Text>
        {detalhe && (
          <Text
            numberOfLines={1}
            style={[textStyles.legenda, { color: colors.ink2 }]}
          >
            {detalhe}
          </Text>
        )}
      </View>

      <View style={styles.pontos}>
        <Text style={[textStyles.pontos, { color: colors.ink }]}>
          {`+${pontos}`}
        </Text>
        {nota && (
          <Text
            style={[textStyles.legenda, styles.nota, { color: colors.ink2 }]}
          >
            {nota}
          </Text>
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  faixa: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
    borderRadius: raios.cardMaior,
    paddingVertical: 4,
    paddingLeft: 4,
    paddingRight: 12,
  },
  textos: { flex: 1, gap: 2 },
  tier: { fontFamily: textStyles.secao.fontFamily },
  // Uma nota longa ("Banco de Dados · 4º bimestre") quebra na própria coluna, sem apertar o
  // nome do prêmio.
  pontos: { alignItems: "flex-end", gap: 2, maxWidth: "38%" },
  nota: { textAlign: "right" },
});
