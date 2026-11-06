import { StyleSheet, Text, View } from "react-native";

import { fontFamily } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { raios, tamanhos } from "../theme/tokens";

interface AvatarProps {
  // Nome de exibição: as iniciais saem dele. Fotos entram quando existirem.
  nome: string;
  size?: number;
}

// Só com as cores do token: aluno nunca é pintado com cor de tier. O anel dourado do próprio
// aluno fica para os passos 7 e 11.
export function Avatar({ nome, size = tamanhos.avatarPost }: AvatarProps) {
  const { colors } = useTheme();

  return (
    <View
      accessible
      role="img"
      aria-label={nome}
      style={[
        styles.circulo,
        {
          width: size,
          height: size,
          backgroundColor: colors.avatarBg,
        },
      ]}
    >
      <Text
        numberOfLines={1}
        style={[
          styles.iniciais,
          { color: colors.avatarFg, fontSize: Math.round(size * 0.4) },
        ]}
      >
        {iniciais(nome)}
      </Text>
    </View>
  );
}

// Primeira letra do primeiro e do último nome: "Ana Paula Souza" vira AS.
export function iniciais(nome: string) {
  const nomes = nome.trim().split(/\s+/);
  const primeira = (palavra: string | undefined) =>
    Array.from(palavra ?? "")[0] ?? "";
  const ultimo = nomes.length > 1 ? nomes[nomes.length - 1] : undefined;
  return (primeira(nomes[0]) + primeira(ultimo)).toLocaleUpperCase("pt-BR");
}

const styles = StyleSheet.create({
  circulo: {
    borderRadius: raios.pill,
    alignItems: "center",
    justifyContent: "center",
  },
  iniciais: {
    fontFamily: fontFamily({ familia: "Archivo", peso: 700 }),
  },
});
