import type { ReactNode } from "react";
import { Modal, Pressable, StyleSheet, Text, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { AvisoDeErro } from "../../autenticacao/AvisoDeErro";
import { Button } from "../../components/Button";
import { textStyles } from "../../theme/fonts";
import { useTheme } from "../../theme/ThemeProvider";
import { espacamento } from "../../theme/tokens";

interface FolhaDeAtoProps {
  aberta: boolean;
  titulo: string;
  explicacao: string;
  confirmar: string;
  podeConfirmar?: boolean;
  enviando: boolean;
  // A mensagem amigável da recusa, dentro da folha.
  erro: string | null;
  onConfirmar: () => void;
  onFechar: () => void;
  children?: ReactNode;
}

// A folha que sobe de baixo para cada ato da instituição sobre o aluno (passo 12): o que o
// ato faz, os campos dele quando há, a recusa da API dentro da própria folha e o botão de
// confirmar. Como a folha de motivo e as do Instagram. Sem prancheta.
export function FolhaDeAto({
  aberta,
  titulo,
  explicacao,
  confirmar,
  podeConfirmar = true,
  enviando,
  erro,
  onConfirmar,
  onFechar,
  children,
}: FolhaDeAtoProps) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();

  return (
    <Modal
      visible={aberta}
      transparent
      animationType="slide"
      onRequestClose={onFechar}
    >
      <View style={styles.fundo}>
        <Pressable
          accessibilityLabel="Fechar"
          style={[StyleSheet.absoluteFill, { backgroundColor: colors.scrim }]}
          onPress={onFechar}
        />
        <View
          testID="folha-de-ato"
          role="dialog"
          aria-modal
          accessibilityLabel={titulo}
          style={[
            styles.folha,
            {
              backgroundColor: colors.bg,
              paddingBottom:
                espacamento.respiroAntesDaNavegacao + insets.bottom,
            },
          ]}
        >
          <Text
            role="heading"
            style={[textStyles.secao, { color: colors.ink }]}
          >
            {titulo}
          </Text>
          <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
            {explicacao}
          </Text>
          {children}
          {erro && <AvisoDeErro mensagem={erro} />}
          <Button
            label={enviando ? "Aguarde..." : confirmar}
            disabled={!podeConfirmar || enviando}
            onPress={onConfirmar}
          />
          <Button label="Cancelar" variant="secondary" onPress={onFechar} />
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  fundo: { flex: 1, justifyContent: "flex-end" },
  folha: {
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    paddingTop: 24,
    paddingHorizontal: espacamento.margemLateral,
    gap: 12,
  },
});
