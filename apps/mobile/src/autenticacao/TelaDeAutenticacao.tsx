import type { ReactNode } from "react";
import { Image, StyleSheet, Text, View } from "react-native";

import { Screen } from "../components/Screen";
import { Logo } from "../marca/Logo";
import { textStyles } from "../theme/fonts";
import { raios } from "../theme/tokens";
import { useTheme } from "../theme/ThemeProvider";

// Largura do logo nas pranchetas de login e cadastro.
const LARGURA_DO_LOGO = 245;
const SELO = 28;

interface TelaDeAutenticacaoProps {
  // O controle Entrar / Cadastrar, nas telas que o têm.
  segmentado?: ReactNode;
  children: ReactNode;
}

// A moldura comum de login, cadastro, termo, recuperação e demonstração, como nas
// pranchetas: logo e frase no alto, o conteúdo, e o selo da rede no pé. Rola quando o
// conteúdo não cabe, e o pé fica no fim da tela quando cabe.
export function TelaDeAutenticacao({
  segmentado,
  children,
}: TelaDeAutenticacaoProps) {
  const { colors } = useTheme();

  return (
    <Screen scroll>
      <View style={styles.tela}>
        <View style={styles.conteudo}>
          <View style={styles.marca}>
            <Logo largura={LARGURA_DO_LOGO} />
            <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
              A rede de conquistas da UniMax
            </Text>
          </View>
          {segmentado}
          {children}
        </View>

        <View style={styles.rodape}>
          <Image
            source={require("../../../../design/assets/unimax.png")}
            style={styles.selo}
            accessibilityIgnoresInvertColors
            alt=""
          />
          <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
            Rede UniEduk · UniMax e UniFaj
          </Text>
        </View>
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  tela: { flexGrow: 1, justifyContent: "space-between", gap: 32 },
  // Centrado no espaço acima do pé, como nas pranchetas: o logo sobe quando o
  // formulário é maior, e a tela rola quando não cabe.
  conteudo: { flexGrow: 1, justifyContent: "center", gap: 24, paddingTop: 24 },
  marca: { alignItems: "center", gap: 8 },
  rodape: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    gap: 12,
    paddingTop: 16,
  },
  selo: { width: SELO, height: SELO, borderRadius: raios.botoes },
});
