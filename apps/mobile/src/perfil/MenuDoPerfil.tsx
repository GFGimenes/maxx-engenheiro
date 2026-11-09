import { useRouter } from "expo-router";
import { Modal, Platform, Pressable, StyleSheet, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { Button } from "../components/Button";
import { useSessao } from "../sessao/SessaoProvider";
import { ThemePreferenceControl } from "../theme/ThemePreferenceControl";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento } from "../theme/tokens";

interface MenuDoPerfilProps {
  aberto: boolean;
  professor: boolean;
  // A área da instituição abre só no navegador (P1 do passo 12).
  instituicao?: boolean;
  onFechar: () => void;
}

// O menu do próprio perfil (o "≡" da prancheta): o lugar definitivo do tema (decisoes,
// Design system: o controle saiu da tela de status para cá no passo 7), as turmas do
// professor, as comprovações dos alunos dele (passo 8, D8), a área da instituição no
// navegador (passo 12) e a saída. Folha que sobe de
// baixo, como as do Instagram.
export function MenuDoPerfil({
  aberto,
  professor,
  instituicao = false,
  onFechar,
}: MenuDoPerfilProps) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { sair } = useSessao();

  return (
    <Modal
      visible={aberto}
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
          role="dialog"
          aria-modal
          accessibilityLabel="Menu"
          style={[
            styles.folha,
            {
              backgroundColor: colors.bg,
              paddingBottom:
                espacamento.respiroAntesDaNavegacao + insets.bottom,
            },
          ]}
        >
          <ThemePreferenceControl />
          {instituicao && Platform.OS === "web" && (
            <Button
              label="Área da instituição"
              variant="secondary"
              onPress={() => {
                onFechar();
                router.push("/instituicao");
              }}
            />
          )}
          {professor && (
            <Button
              label="Minhas turmas"
              variant="secondary"
              onPress={() => {
                onFechar();
                router.push("/turmas");
              }}
            />
          )}
          {professor && (
            <Button
              label="Comprovações"
              variant="secondary"
              onPress={() => {
                onFechar();
                router.push("/comprovacoes");
              }}
            />
          )}
          <Button
            label="Sair"
            variant="secondary"
            onPress={() => {
              onFechar();
              void sair();
            }}
          />
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
