import { View } from "react-native";
import Svg, { Path } from "react-native-svg";

import { useTheme } from "../theme/ThemeProvider";
import { logoVetor } from "./logoVetor";

// Altura mínima do logo (identidade-visual.md, Logo e imagens).
export const ALTURA_MINIMA_DO_LOGO = 22;

// O logo em vetor, nítido em qualquer densidade de tela. Em ouro sobre o fundo dos dois
// temas, como a identidade manda para maxx-ouro.
export function Logo({ largura }: { largura: number }) {
  const { colors } = useTheme();
  const altura = Math.max(
    ALTURA_MINIMA_DO_LOGO,
    Math.round((largura * logoVetor.altura) / logoVetor.largura),
  );
  const larguraFinal = (altura * logoVetor.largura) / logoVetor.altura;

  // O papel de imagem fica na View: o Svg nativo não é elemento de acessibilidade.
  return (
    <View
      testID="logo"
      accessible
      role="img"
      aria-label="Maxx"
      style={{ width: larguraFinal, height: altura }}
    >
      <Svg
        width={larguraFinal}
        height={altura}
        viewBox={`0 0 ${logoVetor.largura} ${logoVetor.altura}`}
      >
        <Path
          testID="logo-tracado"
          d={logoVetor.caminho}
          fill={colors.gold}
          fillRule="evenodd"
        />
      </Svg>
    </View>
  );
}
