import { StyleSheet, Text, View } from "react-native";

import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";

// O texto do termo como vem da API: parágrafos separados por linha em branco, e o que
// começa por "## " é título de seção. Nada além disso, de propósito.
export function TextoDoTermo({ texto }: { texto: string }) {
  const { colors } = useTheme();
  const blocos = texto
    .split(/\n\s*\n/)
    .map((bloco) => bloco.trim())
    .filter(Boolean);

  return (
    <View testID="texto-do-termo" style={styles.coluna}>
      {blocos.map((bloco, i) =>
        bloco.startsWith("## ") ? (
          <Text
            key={i}
            role="heading"
            style={[textStyles.secao, styles.titulo, { color: colors.ink }]}
          >
            {bloco.slice(3)}
          </Text>
        ) : (
          <Text key={i} style={[textStyles.corpo, { color: colors.ink }]}>
            {bloco}
          </Text>
        ),
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  coluna: { gap: 8 },
  titulo: { marginTop: 8 },
});
