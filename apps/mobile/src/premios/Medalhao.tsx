import { useId } from "react";
import { StyleSheet, View } from "react-native";
import Svg, {
  Circle,
  Defs,
  Line,
  RadialGradient,
  Stop,
} from "react-native-svg";

import { Icon } from "../components/Icon";
import { useTheme } from "../theme/ThemeProvider";
import { tamanhos, tiers, type Raridade, type Tier } from "../theme/tokens";
import { rotulosDeRaridade, rotulosDeTier } from "./rotulos";

// Taça para troféu, fita para medalha (identidade-visual.md).
export type Glifo = "taca" | "fita";

interface MedalhaoProps {
  tier: Tier;
  glifo: Glifo;
  // Sem raridade não há ornamento nem espaço para ele: é o medalhão do selo e da trilha.
  raridade?: Raridade;
  // Diâmetro do metal; o ornamento fica fora dele.
  size?: number;
}

// As medidas do ornamento, em tokens.json, valem para o medalhão de 48 da lista.
const REFERENCIA = tamanhos.medalhaoLista;
const ORNAMENTO = {
  traco: 1.5,
  primeiroAnel: 3,
  segundoAnel: 8,
  raios: 10.5,
  tamanhoDoRaio: 2,
  quantidadeDeRaios: 32,
  // Até onde os raios chegam, com a ponta arredondada.
  folga: 13,
};
const BORDA_DO_METAL = 1.5;
const GLIFO_NO_METAL = 0.5;

// Quanto espaço o medalhão ocupa: com raridade, sempre a folga do lendário, para que uma lista
// com raridades diferentes fique alinhada.
export function larguraDoMedalhao(size: number, comRaridade: boolean) {
  return comRaridade ? size + (2 * ORNAMENTO.folga * size) / REFERENCIA : size;
}

export function Medalhao({
  tier,
  glifo,
  raridade,
  size = tamanhos.medalhaoLista,
}: MedalhaoProps) {
  const { colors } = useTheme();
  const estilo = tiers[tier];
  // O id do gradiente é global no navegador: cada medalhão precisa do seu.
  const gradiente = `metal${useId().replace(/[^a-zA-Z0-9]/g, "")}`;

  const escala = size / REFERENCIA;
  const caixa = larguraDoMedalhao(size, raridade !== undefined);
  const centro = caixa / 2;
  const raio = size / 2;
  const borda = BORDA_DO_METAL * escala;
  const tracoDoAnel = ORNAMENTO.traco * escala;
  const anel = (afastamento: number) => (
    <Circle
      testID="anel"
      cx={centro}
      cy={centro}
      r={raio + afastamento * escala}
      stroke={colors.rarityRing}
      strokeWidth={tracoDoAnel}
      fill="none"
    />
  );

  const rotulo = [
    `${glifo === "taca" ? "Troféu" : "Medalha"} ${rotulosDeTier[tier]}`,
    raridade && rotulosDeRaridade[raridade],
  ]
    .filter(Boolean)
    .join(", ");

  return (
    <View
      accessible
      role="img"
      aria-label={rotulo}
      style={{ width: caixa, height: caixa }}
    >
      <Svg width={caixa} height={caixa}>
        <Defs>
          {/* radial-gradient(circle at 50% 30%, ...): o raio vai até o canto mais distante. */}
          <RadialGradient id={gradiente} cx="50%" cy="30%" r="86%">
            <Stop offset="0%" stopColor={estilo.medalhao[0]} />
            <Stop offset="55%" stopColor={estilo.medalhao[1]} />
            <Stop offset="100%" stopColor={estilo.medalhao[2]} />
          </RadialGradient>
        </Defs>

        {raridade === "raro" && anel(ORNAMENTO.primeiroAnel)}
        {raridade === "epico" && (
          <>
            {anel(ORNAMENTO.primeiroAnel)}
            {anel(ORNAMENTO.segundoAnel)}
          </>
        )}
        {raridade === "lendario" && (
          <>
            {anel(ORNAMENTO.primeiroAnel)}
            {Array.from({ length: ORNAMENTO.quantidadeDeRaios }, (_, i) => {
              const angulo = (2 * Math.PI * i) / ORNAMENTO.quantidadeDeRaios;
              const meio = raio + ORNAMENTO.raios * escala;
              const de = meio - (ORNAMENTO.tamanhoDoRaio / 2) * escala;
              const ate = meio + (ORNAMENTO.tamanhoDoRaio / 2) * escala;
              return (
                <Line
                  key={i}
                  testID="raio"
                  x1={centro + de * Math.cos(angulo)}
                  y1={centro + de * Math.sin(angulo)}
                  x2={centro + ate * Math.cos(angulo)}
                  y2={centro + ate * Math.sin(angulo)}
                  stroke={colors.rarityRing}
                  strokeWidth={tracoDoAnel}
                  strokeLinecap="round"
                />
              );
            })}
          </>
        )}

        <Circle
          testID="metal"
          cx={centro}
          cy={centro}
          r={raio - borda / 2}
          fill={`url(#${gradiente})`}
          stroke={estilo.borda}
          strokeWidth={borda}
        />
      </Svg>
      <View style={[StyleSheet.absoluteFill, styles.centro]}>
        <Icon
          name={glifo}
          color={estilo.glifo}
          size={Math.round(size * GLIFO_NO_METAL)}
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  centro: { alignItems: "center", justifyContent: "center" },
});
