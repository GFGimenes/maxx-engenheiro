import { useState } from "react";
import { Modal, Pressable, StyleSheet, Text, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { Button } from "../components/Button";
import { TextField } from "../components/TextField";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento } from "../theme/tokens";

export const LIMITE_DO_MOTIVO = 500;

// Caracteres como a API conta: um emoji vale um.
const caracteres = (texto: string) => Array.from(texto).length;

interface FolhaDeMotivoProps {
  aberta: boolean;
  titulo: string;
  explicacao: string;
  confirmar: string;
  enviando: boolean;
  // A mensagem amigável da recusa, dentro da folha.
  erro: string | null;
  onConfirmar: (motivo: string) => void;
  onFechar: () => void;
}

// Folha que sobe de baixo, como as do Instagram, para o que exige motivo (produto.md 4.7):
// reprovar comprovação e contestar prêmio. Motivo de 1 a 500 caracteres (D10), conferido
// aqui para o botão e de novo pela API. Recusa e falta de conexão ficam dentro da folha.
export function FolhaDeMotivo({
  aberta,
  titulo,
  explicacao,
  confirmar,
  enviando,
  erro,
  onConfirmar,
  onFechar,
}: FolhaDeMotivoProps) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const [motivo, setMotivo] = useState("");
  const tamanho = caracteres(motivo.trim());
  const longo = tamanho > LIMITE_DO_MOTIVO;

  const fechar = () => {
    setMotivo("");
    onFechar();
  };

  return (
    <Modal
      visible={aberta}
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
          testID="folha-de-motivo"
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
          <View style={styles.campo}>
            <TextField
              label="Motivo"
              multiline
              value={motivo}
              onChangeText={setMotivo}
              placeholder="Explique em poucas palavras."
              error={longo ? "O motivo passou de 500 caracteres." : undefined}
            />
            <Text
              style={[
                textStyles.legenda,
                styles.contador,
                { color: colors.ink3 },
              ]}
            >
              {`${tamanho}/500`}
            </Text>
          </View>
          {erro && <AvisoDeErro mensagem={erro} />}
          <Button
            label={enviando ? "Aguarde..." : confirmar}
            disabled={tamanho === 0 || longo || enviando}
            onPress={() => onConfirmar(motivo.trim())}
          />
          <Button label="Cancelar" variant="secondary" onPress={fechar} />
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
  campo: { gap: 4 },
  contador: { alignSelf: "flex-end" },
});
