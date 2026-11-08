import { StyleSheet, Text, View } from "react-native";

import { fontFamily } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { raios } from "../theme/tokens";
import { rotulosDeEstado } from "./textos";

// A marca do prêmio de participação que ainda não pontua (produto.md 4.7): "Pendente" para o
// reivindicado, que todos veem, e "Em averiguação", que só o aluno e a instituição recebem.
// Sem prancheta: chip neutro, na altura do selo, com raio 8 como os chips, sem metal e sem
// tier, porque nenhum dos dois estados vale pontos agora.
export function MarcaDeEstado({ estado }: { estado: string }) {
  const { colors } = useTheme();
  const rotulo = rotulosDeEstado[estado];
  if (!rotulo) return null;

  return (
    <View
      testID={`marca-${estado}`}
      style={[styles.marca, { backgroundColor: colors.surface2 }]}
    >
      <Text style={[styles.texto, { color: colors.ink2 }]}>{rotulo}</Text>
    </View>
  );
}

// Os estados do post que levam marca.
export const temMarca = (estado: string | null | undefined) =>
  estado === "reivindicado" || estado === "emAveriguacao";

const styles = StyleSheet.create({
  marca: {
    alignSelf: "flex-start",
    height: 26,
    borderRadius: raios.filtros,
    paddingHorizontal: 10,
    justifyContent: "center",
  },
  texto: {
    fontFamily: fontFamily({ familia: "Archivo", peso: 700 }),
    fontSize: 12,
  },
});
