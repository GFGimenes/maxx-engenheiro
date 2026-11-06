import { useId, useState } from "react";
import { StyleSheet, Text, View } from "react-native";
import Svg, { Defs, LinearGradient, Rect, Stop } from "react-native-svg";

import { fontFamily } from "../theme/fonts";
import { raios, tiers, type Tier } from "../theme/tokens";
import { Medalhao, type Glifo } from "./Medalhao";

interface SeloPremioProps {
  nome: string;
  tier: Tier;
  glifo: Glifo;
}

const ALTURA = 26;
const MEDALHAO = 18;
const PARADAS = ["0%", "34%", "50%", "64%", "100%"] as const;
const ANGULO = 105;

// Os pontos da linha de um linear-gradient do CSS numa caixa w x h, porque o SVG não tem ângulo.
export function linhaDoGradiente(angulo: number, w: number, h: number) {
  const radianos = (angulo * Math.PI) / 180;
  const dx = Math.sin(radianos);
  const dy = -Math.cos(radianos);
  const meio = (Math.abs(w * dx) + Math.abs(h * dy)) / 2;
  return {
    x1: w / 2 - dx * meio,
    y1: h / 2 - dy * meio,
    x2: w / 2 + dx * meio,
    y2: h / 2 + dy * meio,
  };
}

// O selo compacto do post: pílula com o gradiente do metal, medalhão pequeno e o nome do prêmio.
export function SeloPremio({ nome, tier, glifo }: SeloPremioProps) {
  const estilo = tiers[tier];
  const [largura, setLargura] = useState(0);
  const gradiente = `pilula${useId().replace(/[^a-zA-Z0-9]/g, "")}`;

  return (
    <View
      testID="selo"
      onLayout={(event) => setLargura(event.nativeEvent.layout.width)}
      // Até medir a largura, a cor do meio do metal segura o contraste do texto.
      style={[styles.pilula, { backgroundColor: estilo.pill[1] }]}
    >
      {largura > 0 && (
        <Svg width={largura} height={ALTURA} style={StyleSheet.absoluteFill}>
          <Defs>
            <LinearGradient
              id={gradiente}
              gradientUnits="userSpaceOnUse"
              {...linhaDoGradiente(ANGULO, largura, ALTURA)}
            >
              {PARADAS.map((offset, indice) => (
                <Stop
                  key={offset}
                  offset={offset}
                  stopColor={estilo.pill[indice]}
                />
              ))}
            </LinearGradient>
          </Defs>
          <Rect
            width={largura}
            height={ALTURA}
            rx={ALTURA / 2}
            fill={`url(#${gradiente})`}
          />
        </Svg>
      )}
      <Medalhao tier={tier} glifo={glifo} size={MEDALHAO} />
      <Text
        numberOfLines={1}
        style={[styles.nome, { color: estilo.pillTexto }]}
      >
        {nome}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  pilula: {
    alignSelf: "flex-start",
    maxWidth: "100%",
    height: ALTURA,
    borderRadius: raios.pill,
    flexDirection: "row",
    alignItems: "center",
    gap: 6,
    paddingLeft: 4,
    paddingRight: 10,
  },
  nome: {
    flexShrink: 1,
    fontFamily: fontFamily({ familia: "Archivo", peso: 700 }),
    fontSize: 12,
  },
});
