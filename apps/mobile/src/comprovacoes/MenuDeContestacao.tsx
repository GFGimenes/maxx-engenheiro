import { useState } from "react";
import { Modal, Pressable, StyleSheet, Text, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { Button } from "../components/Button";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento } from "../theme/tokens";
import { useContestar } from "./api";
import { FolhaDeMotivo } from "./FolhaDeMotivo";

interface MenuDeContestacaoProps {
  premioId: string;
  nomeDoAluno: string;
  aberto: boolean;
  // Direto na folha de motivo: a instituição já escolheu contestar no menu do post.
  direto?: boolean;
  onFechar: () => void;
}

// O "..." do post para o professor, sobre um prêmio de participação válido (produto.md 4.7,
// D8): contestar, sempre com motivo, dentro dos 15 dias da aprovação. O escopo de turma e a
// janela são conferidos pela API; a recusa aparece dentro da folha. Contestado, o prêmio vai
// a averiguação, e o post relido some com ele para quem não é o aluno nem a instituição.
export function MenuDeContestacao({
  premioId,
  nomeDoAluno,
  aberto,
  direto = false,
  onFechar,
}: MenuDeContestacaoProps) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const contestar = useContestar();
  const [escrevendo, setEscrevendo] = useState(false);

  const fechar = () => {
    setEscrevendo(false);
    contestar.reset();
    onFechar();
  };

  return (
    <>
      <Modal
        visible={aberto && !escrevendo && !direto}
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
            <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
              Contestar leva o prêmio para averiguação, e ele para de pontuar
              até a instituição decidir.
            </Text>
            <Button
              label="Contestar prêmio"
              variant="secondary"
              onPress={() => setEscrevendo(true)}
            />
            <Button label="Cancelar" variant="secondary" onPress={fechar} />
          </View>
        </View>
      </Modal>
      <FolhaDeMotivo
        aberta={aberto && (escrevendo || direto)}
        titulo="Contestar prêmio"
        explicacao={`A instituição decide. ${nomeDoAluno} vê que o prêmio está em averiguação.`}
        confirmar="Contestar"
        enviando={contestar.isPending}
        erro={
          contestar.isError ? falhaParaTela(contestar.error).mensagem : null
        }
        onConfirmar={(motivo) =>
          contestar.mutate({ premioId, motivo }, { onSuccess: fechar })
        }
        onFechar={fechar}
      />
    </>
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
