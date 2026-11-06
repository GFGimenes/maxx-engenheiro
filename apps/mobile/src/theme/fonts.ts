import type { TextStyle } from "react-native";

import { tipografia, type EstiloDeTexto } from "./tokens";

// Os arquivos gerados por design/fontes/gerar.sh. A chave é o nome da família no React
// Native: cada peso é uma família, porque o Android não escolhe arquivo por `fontWeight`.
export const fontAssets = {
  "Archivo112-700": require("../../assets/fonts/Archivo112-700.ttf"),
  "Archivo112-800": require("../../assets/fonts/Archivo112-800.ttf"),
  "Archivo112-800Italic": require("../../assets/fonts/Archivo112-800Italic.ttf"),
  "InstrumentSans-400": require("../../assets/fonts/InstrumentSans-400.ttf"),
  "InstrumentSans-500": require("../../assets/fonts/InstrumentSans-500.ttf"),
  "InstrumentSans-600": require("../../assets/fonts/InstrumentSans-600.ttf"),
  "InstrumentSans-700": require("../../assets/fonts/InstrumentSans-700.ttf"),
} as const;

export type FontFamily = keyof typeof fontAssets;

export function fontFamily({
  familia,
  peso,
  italico,
}: Pick<EstiloDeTexto, "familia" | "peso" | "italico">): FontFamily {
  const prefixo = familia === "Archivo" ? "Archivo112" : "InstrumentSans";
  return `${prefixo}-${peso}${italico ? "Italic" : ""}` as FontFamily;
}

function toTextStyle(estilo: EstiloDeTexto): TextStyle {
  return {
    fontFamily: fontFamily(estilo),
    fontSize: estilo.tamanho,
    ...(estilo.alturaLinha !== undefined && { lineHeight: estilo.alturaLinha }),
    ...(estilo.caixaAlta && { textTransform: "uppercase" as const }),
    ...(estilo.espacamentoLetras !== undefined && {
      letterSpacing: estilo.espacamentoLetras * estilo.tamanho,
    }),
    ...(estilo.algarismosTabulares && {
      fontVariant: ["tabular-nums" as const],
    }),
  };
}

export const textStyles = {
  display: toTextStyle(tipografia.display),
  titulo: toTextStyle(tipografia.titulo),
  pontos: toTextStyle(tipografia.pontos),
  secao: toTextStyle(tipografia.secao),
  corpo: toTextStyle(tipografia.corpo),
  legenda: toTextStyle(tipografia.legenda),
  rotulo: toTextStyle(tipografia.rotulo),
};
