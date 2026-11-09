import { useState } from "react";
import { Modal, Pressable, StyleSheet, Text, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { Button } from "../components/Button";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento } from "../theme/tokens";
import { useRemoverPost } from "./api";

// O que o "..." oferece a quem pode tirar o post do ar: o autor apaga, a instituição remove
// (produto.md 2.1 e 6.1). Mais ninguém vê o menu.
export type AcaoDoPost = "apagar" | "remover";

const TEXTOS: Record<
  AcaoDoPost,
  { botao: string; pergunta: string; detalhe: string }
> = {
  apagar: {
    botao: "Apagar post",
    pergunta: "Apagar este post?",
    detalhe: "Curtidas e comentários saem junto. Não dá para desfazer.",
  },
  remover: {
    botao: "Remover post",
    pergunta: "Remover este post do feed?",
    detalhe: "Ele some para todos, e fica registrado quem removeu e quando.",
  },
};

interface MenuDoPostProps {
  postId: string;
  acao: AcaoDoPost | null;
  aberto: boolean;
  onFechar: () => void;
  onRemovido: () => void;
  // Só a instituição, sobre prêmio de participação válido (passo 12): abre a folha de motivo.
  onContestar?: () => void;
}

// Folha que sobe de baixo, como as do Instagram, com a confirmação na própria folha.
export function MenuDoPost({
  postId,
  acao,
  aberto,
  onFechar,
  onRemovido,
  onContestar,
}: MenuDoPostProps) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const remover = useRemoverPost();
  const [confirmando, setConfirmando] = useState(false);

  const fechar = () => {
    setConfirmando(false);
    remover.reset();
    onFechar();
  };

  if (!acao) return null;
  const textos = TEXTOS[acao];

  return (
    <Modal
      visible={aberto}
      transparent
      animationType="slide"
      onRequestClose={fechar}
    >
      <View style={styles.fundo}>
        <Pressable
          accessibilityLabel="Fechar"
          style={[StyleSheet.absoluteFill, { backgroundColor: colors.scrim }]}
          onPress={fechar}
        />
        <View
          role="dialog"
          aria-modal
          accessibilityLabel="Opções do post"
          style={[
            styles.folha,
            {
              backgroundColor: colors.bg,
              paddingBottom:
                espacamento.respiroAntesDaNavegacao + insets.bottom,
            },
          ]}
        >
          {confirmando ? (
            <>
              <Text
                role="heading"
                style={[textStyles.secao, { color: colors.ink }]}
              >
                {textos.pergunta}
              </Text>
              <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
                {textos.detalhe}
              </Text>
              {remover.isError && (
                <AvisoDeErro mensagem={falhaParaTela(remover.error).mensagem} />
              )}
              <Button
                label={
                  remover.isPending
                    ? "Aguarde..."
                    : acao === "apagar"
                      ? "Apagar"
                      : "Remover"
                }
                disabled={remover.isPending}
                onPress={() =>
                  remover.mutate(postId, {
                    onSuccess: () => {
                      setConfirmando(false);
                      onRemovido();
                    },
                  })
                }
              />
              <Button label="Cancelar" variant="secondary" onPress={fechar} />
            </>
          ) : (
            <>
              {onContestar && (
                <Button
                  label="Contestar prêmio"
                  variant="secondary"
                  onPress={onContestar}
                />
              )}
              <Button
                label={textos.botao}
                variant="secondary"
                onPress={() => setConfirmando(true)}
              />
              <Button label="Cancelar" variant="secondary" onPress={fechar} />
            </>
          )}
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
