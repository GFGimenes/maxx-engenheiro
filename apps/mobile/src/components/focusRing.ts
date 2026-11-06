import { useState } from "react";
import { Platform, type ViewStyle } from "react-native";

import { interacao } from "../theme/tokens";

// O anel de foco dos controles: aparece para quem navega por teclado. Na web, um clique de
// mouse também foca o elemento, e por isso vale a regra do navegador (:focus-visible).
export function useFocusRing() {
  const [focusVisible, setFocusVisible] = useState(false);

  return {
    focusVisible,
    focusProps: {
      onFocus: (event: { target?: unknown }) => {
        const alvo = event.target as Element | null | undefined;
        setFocusVisible(
          Platform.OS !== "web" || (alvo?.matches(":focus-visible") ?? true),
        );
      },
      onBlur: () => setFocusVisible(false),
    },
  };
}

export function focusRingStyle(color: string): ViewStyle {
  return {
    outlineColor: color,
    outlineStyle: "solid",
    outlineWidth: interacao.anelDeFoco.espessura,
    outlineOffset: interacao.anelDeFoco.afastamento,
  };
}

// Tira o contorno padrão do navegador, que o anel substitui. Com largura 0 ele continua lá: o
// estilo "auto" do navegador ignora a largura. O tipo do React Native não conhece "none".
export const noOutline = Platform.select<ViewStyle>({
  web: { outlineStyle: "none" as ViewStyle["outlineStyle"] },
  default: {},
});
