import type { ReactNode } from "react";
import { Platform, StyleSheet, View, useWindowDimensions } from "react-native";

import { useTheme } from "../theme/ThemeProvider";

// Moldura de celular no desktop: a partir de 600 px de janela, coluna de 390 px
// (a largura das pranchetas) com teto de 844 px de altura, centralizada nos dois eixos.
export const FRAME_MIN_WINDOW_WIDTH = 600;
export const FRAME_WIDTH = 390;
export const FRAME_MAX_HEIGHT = 844;

export function PhoneFrame({ children }: { children: ReactNode }) {
  const { colors } = useTheme();
  const { width, height } = useWindowDimensions();

  if (Platform.OS !== "web" || width < FRAME_MIN_WINDOW_WIDTH) {
    return (
      <View
        testID="app-fundo"
        style={[styles.fill, { backgroundColor: colors.bg }]}
      >
        {children}
      </View>
    );
  }

  return (
    <View
      testID="moldura-fundo"
      style={[styles.fill, styles.center, { backgroundColor: colors.surface }]}
    >
      <View
        testID="moldura"
        style={[
          styles.frame,
          {
            height: Math.min(height, FRAME_MAX_HEIGHT),
            backgroundColor: colors.bg,
            borderColor: colors.line,
          },
        ]}
      >
        {children}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  center: { alignItems: "center", justifyContent: "center" },
  // Sem sombra: separação só pela borda de 1 px em `line`.
  frame: { width: FRAME_WIDTH, borderWidth: 1, overflow: "hidden" },
});

// A largura em que o app desenha: a da moldura no navegador de desktop (sem a borda de 1 px
// de cada lado), a da janela no resto. É o que a grade do perfil divide em três colunas,
// sem esperar o primeiro onLayout.
export function useLarguraDoApp(): number {
  const { width } = useWindowDimensions();
  return Platform.OS === "web" && width >= FRAME_MIN_WINDOW_WIDTH
    ? FRAME_WIDTH - 2
    : width;
}
