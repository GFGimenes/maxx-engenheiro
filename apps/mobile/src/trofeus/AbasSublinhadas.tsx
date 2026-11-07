import { Pressable, StyleSheet, Text, View } from "react-native";

import {
  noOutline,
  focusRingStyle,
  useFocusRing,
} from "../components/focusRing";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { tamanhos } from "../theme/tokens";

interface AbasSublinhadasProps<T extends string> {
  abas: readonly { valor: T; rotulo: string }[];
  ativa: T;
  onSelect: (valor: T) => void;
}

// As abas de texto com traço embaixo da ativa, como as do perfil do Instagram e da
// prancheta de Troféus. A linha de 1 px em `line` separa do conteúdo.
export function AbasSublinhadas<T extends string>({
  abas,
  ativa,
  onSelect,
}: AbasSublinhadasProps<T>) {
  const { colors } = useTheme();

  return (
    <View
      role="tablist"
      style={[styles.barra, { borderBottomColor: colors.line }]}
    >
      {abas.map((aba) => (
        <Aba
          key={aba.valor}
          rotulo={aba.rotulo}
          ativa={aba.valor === ativa}
          onPress={() => onSelect(aba.valor)}
        />
      ))}
    </View>
  );
}

function Aba({
  rotulo,
  ativa,
  onPress,
}: {
  rotulo: string;
  ativa: boolean;
  onPress: () => void;
}) {
  const { colors } = useTheme();
  const { focusVisible, focusProps } = useFocusRing();

  return (
    <Pressable
      role="tab"
      aria-selected={ativa}
      onPress={onPress}
      {...focusProps}
      style={[
        styles.aba,
        noOutline,
        focusVisible && focusRingStyle(colors.focus),
      ]}
    >
      <Text
        style={[textStyles.secao, { color: ativa ? colors.ink : colors.ink2 }]}
      >
        {rotulo}
      </Text>
      <View
        style={[
          styles.traco,
          { backgroundColor: ativa ? colors.ink : "transparent" },
        ]}
      />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  barra: { flexDirection: "row", borderBottomWidth: 1 },
  aba: {
    flex: 1,
    alignItems: "center",
    justifyContent: "flex-end",
    minHeight: tamanhos.alvoDeToque,
    paddingTop: 8,
    gap: 8,
  },
  traco: {
    height: 2,
    alignSelf: "stretch",
    marginHorizontal: 24,
    borderRadius: 1,
  },
});
