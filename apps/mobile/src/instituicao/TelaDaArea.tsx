import type { Href } from "expo-router";
import type { ReactNode } from "react";
import { ActivityIndicator, Platform, StyleSheet, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { useEu } from "../api/useEu";
import { Cabecalho } from "../components/Cabecalho";
import { EmptyState } from "../components/StateMessage";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento } from "../theme/tokens";

// Quem vê a área da instituição: a conta com o papel de instituição, e só no navegador
// (produto.md 10.2, P1 do passo 12). No aparelho, e para os outros papéis, a tela explica.
// A API confere o papel de novo em cada pedido.
export function useEhDaInstituicao() {
  const eu = useEu();
  return {
    carregando: eu.isPending,
    instituicao: eu.data?.papeis.includes("instituicao") ?? false,
  };
}

export const naWeb = () => Platform.OS === "web";

// A moldura das telas da área: cabeçalho com voltar e o conteúdo, ou a explicação de quem
// não entra. Fica dentro da moldura de celular do app, como as outras telas.
export function TelaDaArea({
  titulo,
  voltarPara,
  children,
}: {
  titulo: string;
  voltarPara: Href;
  children: ReactNode;
}) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const { carregando, instituicao } = useEhDaInstituicao();

  let conteudo = children;
  if (carregando) {
    conteudo = <ActivityIndicator style={styles.espera} color={colors.ink2} />;
  } else if (!instituicao) {
    conteudo = (
      <View style={styles.margem}>
        <EmptyState
          icon="inbox"
          title="Esta área é da instituição."
          message="Quem decide contestações, concede prêmios e publica avisos da faculdade é a conta da UniMax."
        />
      </View>
    );
  } else if (!naWeb()) {
    conteudo = (
      <View style={styles.margem}>
        <EmptyState
          icon="inbox"
          title="A área da instituição abre no navegador."
          message="Entre no Maxx pelo computador para usar as filas e os atos da instituição."
        />
      </View>
    );
  }

  return (
    <View
      style={[
        styles.fill,
        { paddingTop: insets.top, backgroundColor: colors.bg },
      ]}
    >
      <View style={styles.margem}>
        <Cabecalho titulo={titulo} voltarPara={voltarPara} />
      </View>
      {conteudo}
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  margem: { paddingHorizontal: espacamento.margemLateral },
  espera: { marginTop: 32 },
});
