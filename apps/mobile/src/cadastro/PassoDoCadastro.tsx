import { StyleSheet, Text, View } from "react-native";

import { TextLink } from "../components/TextLink";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";

// O cabeçalho dos passos 2 e 3: onde a pessoa está e como voltar, sem perder o que já
// digitou.
export function PassoDoCadastro({
  numero,
  titulo,
  onVoltar,
}: {
  numero: 2 | 3;
  titulo: string;
  onVoltar: () => void;
}) {
  const { colors } = useTheme();

  return (
    <View style={styles.cabecalho}>
      <View style={styles.linha}>
        <TextLink label="Voltar" onPress={onVoltar} />
        <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
          {numero} de 3
        </Text>
      </View>
      <Text role="heading" style={[textStyles.titulo, { color: colors.ink }]}>
        {titulo}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  cabecalho: { gap: 4 },
  linha: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },
});
