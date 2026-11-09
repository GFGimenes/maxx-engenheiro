import { useRouter } from "expo-router";
import { Pressable, StyleSheet, View } from "react-native";

import { Icon } from "../components/Icon";
import { useTheme } from "../theme/ThemeProvider";
import { raios, tamanhos } from "../theme/tokens";
import { useNovidades } from "./api";

// O sininho do cabeçalho do feed e do Mural (produto.md 8; prancheta feed-*.png e
// mural-*.png), com a bolinha dourada de notificação não lida no canto, como a das abas. A
// novidade também vai no rótulo: a bolinha sozinha não diz nada a quem não vê.
export function Sininho() {
  const { colors } = useTheme();
  const router = useRouter();
  const novidades = useNovidades();
  const temNovidade = novidades.data?.sininho ?? false;

  return (
    <Pressable
      role="button"
      accessibilityLabel={
        temNovidade ? "Notificações, com novidade" : "Notificações"
      }
      hitSlop={8}
      onPress={() => router.push("/notificacoes")}
      style={({ pressed }) => [styles.botao, pressed && styles.pressionado]}
    >
      <View>
        <Icon name="sino" color={colors.ink} size={tamanhos.iconeNav} />
        {temNovidade && (
          <View
            testID="novidade-sininho"
            style={[
              styles.bolinha,
              { backgroundColor: colors.gold, borderColor: colors.bg },
            ]}
          />
        )}
      </View>
    </Pressable>
  );
}

const { diametro, halo } = tamanhos.bolinhaNovidade;

const styles = StyleSheet.create({
  botao: {
    width: tamanhos.alvoDeToque,
    height: tamanhos.alvoDeToque,
    alignItems: "center",
    justifyContent: "center",
    marginRight: -10,
  },
  pressionado: { opacity: 0.85 },
  // A borda é o halo, na cor do fundo, como na navegação.
  bolinha: {
    position: "absolute",
    top: -halo - 1,
    right: -halo - 1,
    width: diametro + 2 * halo,
    height: diametro + 2 * halo,
    borderRadius: raios.pill,
    borderWidth: halo,
  },
});
