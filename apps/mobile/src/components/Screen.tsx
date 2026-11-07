import type { ReactNode } from "react";
import { ScrollView, StyleSheet, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { espacamento } from "../theme/tokens";

interface ScreenProps {
  children: ReactNode;
  // Conteúdo maior que a tela. Sem isso, a tela ocupa a altura disponível, sem rolar.
  scroll?: boolean;
  // Acima da navegação inferior, que já cuida da área segura de baixo.
  aboveNav?: boolean;
}

// O espaço de toda tela: margem lateral de 16, área segura do aparelho e 24 de respiro no fim.
export function Screen({
  children,
  scroll = false,
  aboveNav = false,
}: ScreenProps) {
  const insets = useSafeAreaInsets();
  const padding = {
    paddingTop: insets.top,
    paddingBottom:
      espacamento.respiroAntesDaNavegacao + (aboveNav ? 0 : insets.bottom),
    paddingLeft: espacamento.margemLateral + insets.left,
    paddingRight: espacamento.margemLateral + insets.right,
  };

  if (scroll) {
    return (
      <ScrollView
        testID="tela"
        style={styles.fill}
        contentContainerStyle={[padding, styles.cheio]}
      >
        {children}
      </ScrollView>
    );
  }

  return (
    <View testID="tela" style={[styles.fill, padding]}>
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  // O conteúdo ocupa ao menos a altura da tela: um pé de página fica no fim quando cabe.
  cheio: { flexGrow: 1 },
});
