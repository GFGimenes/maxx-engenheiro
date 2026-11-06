import { ScrollView, StyleSheet, Text, View } from "react-native";
import Svg, { Circle, Line } from "react-native-svg";

import { Icon } from "../components/Icon";
import { fontFamily, textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { raios, type Tier } from "../theme/tokens";
import { Medalhao, type Glifo } from "./Medalhao";

export interface ItemDaTrilha {
  // Chave estável do item, como o código do catálogo.
  id: string;
  rotulo: string;
  tier: Tier;
  glifo: Glifo;
  obtido: boolean;
  // Até duas linhas sob o rótulo, como "+5 pts" e "1º sem.".
  detalhes?: readonly string[];
}

interface TrilhaFamiliaProps {
  titulo: string;
  // À direita do título, como "3 de 4 · 115 pts".
  resumo?: string;
  // De 2 a 5, na ordem da família. O primeiro não obtido é o próximo, com cadeado.
  itens: readonly ItemDaTrilha[];
}

const LARGURA_DO_PASSO = 82;
const DIAMETRO = 36;
const TRACO_DA_LINHA = 2;
const PROXIMO_TRACO = 2;
const TRACEJADO = "4 4";
const CADEADO = 16;

// A trilha da família: liga os obtidos e mostra o resto tracejado. Com mais passos do que
// cabe na tela, rola na horizontal.
export function TrilhaFamilia({ titulo, resumo, itens }: TrilhaFamiliaProps) {
  const { colors } = useTheme();
  const proximo = itens.findIndex((item) => !item.obtido);

  return (
    <View
      testID="trilha"
      style={[styles.cartao, { backgroundColor: colors.surface }]}
    >
      <View style={styles.cabecalho}>
        <Text style={[styles.titulo, textStyles.secao, { color: colors.ink }]}>
          {titulo}
        </Text>
        {resumo && (
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            {resumo}
          </Text>
        )}
      </View>

      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.passos}
      >
        {itens.map((item, indice) => {
          const anterior = itens[indice - 1];
          const seguinte = itens[indice + 1];
          return (
            <View key={item.id} testID="passo" style={styles.passo}>
              <View style={styles.trilho}>
                {anterior && (
                  <Ligacao
                    lado="esquerda"
                    continua={anterior.obtido && item.obtido}
                  />
                )}
                {seguinte && (
                  <Ligacao
                    lado="direita"
                    continua={item.obtido && seguinte.obtido}
                  />
                )}
                {item.obtido ? (
                  <Medalhao
                    tier={item.tier}
                    glifo={item.glifo}
                    size={DIAMETRO}
                  />
                ) : (
                  <NaoObtido comCadeado={indice === proximo} />
                )}
              </View>

              <Text
                numberOfLines={1}
                style={[styles.rotulo, { color: colors.ink }]}
              >
                {item.rotulo}
              </Text>
              {item.detalhes?.map((detalhe) => (
                <Text
                  key={detalhe}
                  numberOfLines={1}
                  style={[
                    styles.detalhe,
                    textStyles.legenda,
                    { color: colors.ink2 },
                  ]}
                >
                  {detalhe}
                </Text>
              ))}
            </View>
          );
        })}
      </ScrollView>
    </View>
  );
}

function NaoObtido({ comCadeado }: { comCadeado: boolean }) {
  const { colors } = useTheme();

  return (
    <View
      accessible
      role="img"
      aria-label={comCadeado ? "Próximo, bloqueado" : "Bloqueado"}
      style={styles.circulo}
    >
      <Svg width={DIAMETRO} height={DIAMETRO} style={StyleSheet.absoluteFill}>
        <Circle
          testID="tracejado"
          cx={DIAMETRO / 2}
          cy={DIAMETRO / 2}
          r={(DIAMETRO - PROXIMO_TRACO) / 2}
          stroke={colors.ink2}
          strokeWidth={PROXIMO_TRACO}
          strokeDasharray={TRACEJADO}
          fill="none"
        />
      </Svg>
      {comCadeado && <Icon name="lock" color={colors.ink2} size={CADEADO} />}
    </View>
  );
}

// A linha entre dois passos, do centro do passo até a borda do círculo.
function Ligacao({
  lado,
  continua,
}: {
  lado: "esquerda" | "direita";
  continua: boolean;
}) {
  const { colors } = useTheme();
  const largura = (LARGURA_DO_PASSO - DIAMETRO) / 2;

  return (
    <Svg
      width={largura}
      height={TRACO_DA_LINHA}
      style={[styles.ligacao, lado === "esquerda" ? { left: 0 } : { right: 0 }]}
    >
      <Line
        testID={continua ? "ligacao-continua" : "ligacao-tracejada"}
        x1={0}
        y1={TRACO_DA_LINHA / 2}
        x2={largura}
        y2={TRACO_DA_LINHA / 2}
        stroke={continua ? colors.ink : colors.ink2}
        strokeWidth={TRACO_DA_LINHA}
        strokeDasharray={continua ? undefined : TRACEJADO}
      />
    </Svg>
  );
}

const styles = StyleSheet.create({
  cartao: {
    borderRadius: raios.cardMaior,
    paddingVertical: 16,
    gap: 12,
  },
  cabecalho: {
    flexDirection: "row",
    alignItems: "flex-start",
    gap: 12,
    paddingHorizontal: 16,
  },
  titulo: { flex: 1 },
  passos: { flexGrow: 1, justifyContent: "center", paddingHorizontal: 0 },
  passo: { width: LARGURA_DO_PASSO, alignItems: "center" },
  trilho: {
    width: LARGURA_DO_PASSO,
    height: DIAMETRO,
    alignItems: "center",
    justifyContent: "center",
    marginBottom: 8,
  },
  ligacao: { position: "absolute", top: (DIAMETRO - TRACO_DA_LINHA) / 2 },
  circulo: {
    width: DIAMETRO,
    height: DIAMETRO,
    alignItems: "center",
    justifyContent: "center",
  },
  rotulo: {
    fontFamily: fontFamily({ familia: "Archivo", peso: 700 }),
    fontSize: 12,
    lineHeight: 16,
    textAlign: "center",
  },
  detalhe: { textAlign: "center" },
});
