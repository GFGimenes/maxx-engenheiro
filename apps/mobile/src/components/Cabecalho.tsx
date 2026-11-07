import { useRouter, type Href } from "expo-router";
import { Pressable, StyleSheet, Text, View } from "react-native";

import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { tamanhos } from "../theme/tokens";
import { Icon } from "./Icon";

interface CabecalhoProps {
  titulo: string;
  // Para onde volta quando não há tela anterior (a página foi aberta direto pelo link).
  voltarPara: Href;
}

// O topo de uma tela interna, como o do Instagram: voltar à esquerda e o título. Sem
// sombra: a separação é a linha de 1 px em `line`.
export function Cabecalho({ titulo, voltarPara }: CabecalhoProps) {
  const { colors } = useTheme();
  const router = useRouter();

  return (
    <View style={[styles.barra, { borderBottomColor: colors.line }]}>
      <Pressable
        role="button"
        accessibilityLabel="Voltar"
        hitSlop={8}
        style={({ pressed }) => [styles.voltar, pressed && styles.pressionado]}
        onPress={() =>
          router.canGoBack() ? router.back() : router.replace(voltarPara)
        }
      >
        <Icon name="voltar" color={colors.ink} size={tamanhos.iconeNav} />
      </Pressable>
      <Text
        role="heading"
        numberOfLines={1}
        style={[textStyles.secao, styles.titulo, { color: colors.ink }]}
      >
        {titulo}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  barra: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
    minHeight: tamanhos.alvoDeToque + 8,
    borderBottomWidth: 1,
    marginBottom: 8,
  },
  voltar: {
    width: tamanhos.alvoDeToque,
    height: tamanhos.alvoDeToque,
    alignItems: "center",
    justifyContent: "center",
    marginLeft: -12,
  },
  pressionado: { opacity: 0.85 },
  titulo: { flex: 1 },
});
