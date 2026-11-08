import { Image, StyleSheet, Text, View } from "react-native";

import { fontFamily } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { raios, tamanhos } from "../theme/tokens";

interface AvatarProps {
  // Nome de exibição: as iniciais saem dele, e é o rótulo para o leitor de tela.
  nome: string;
  // A derivada da grade da foto de perfil. Sem ela, as iniciais.
  foto?: string | null;
  size?: number;
  // O anel dourado do próprio aluno, no cabeçalho do perfil dele (prancheta do perfil). É o
  // ouro da marca, não cor de tier: o aluno não tem tier.
  anel?: boolean;
}

// Espessura do anel e a folga entre ele e o avatar, como na prancheta.
const ANEL = 3;
const FOLGA = 3;

// Só com as cores do token: aluno nunca é pintado com cor de tier.
export function Avatar({
  nome,
  foto,
  size = tamanhos.avatarPost,
  anel = false,
}: AvatarProps) {
  const { colors } = useTheme();
  const interno = anel ? size - 2 * (ANEL + FOLGA) : size;

  const circulo = foto ? (
    <Image
      accessible
      role="img"
      aria-label={nome}
      source={{ uri: foto }}
      style={[
        styles.circulo,
        { width: interno, height: interno, backgroundColor: colors.avatarBg },
      ]}
    />
  ) : (
    <View
      accessible
      role="img"
      aria-label={nome}
      style={[
        styles.circulo,
        { width: interno, height: interno, backgroundColor: colors.avatarBg },
      ]}
    >
      <Text
        numberOfLines={1}
        style={[
          styles.iniciais,
          { color: colors.avatarFg, fontSize: Math.round(interno * 0.4) },
        ]}
      >
        {iniciais(nome)}
      </Text>
    </View>
  );

  if (!anel) return circulo;

  return (
    <View
      testID="anel-do-avatar"
      style={[
        styles.circulo,
        {
          width: size,
          height: size,
          borderWidth: ANEL,
          borderColor: colors.gold,
        },
      ]}
    >
      {circulo}
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
