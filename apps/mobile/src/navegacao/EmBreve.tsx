import { StyleSheet, View } from "react-native";

import { type IconName } from "../components/Icon";
import { Screen } from "../components/Screen";
import { EmptyState } from "../components/StateMessage";

// A aba que ainda não chegou: diz o que vem, em vez de uma tela vazia que pareça travada.
export function EmBreve({
  icone,
  titulo,
  mensagem,
}: {
  icone: IconName;
  titulo: string;
  mensagem: string;
}) {
  return (
    <Screen aboveNav>
      <View style={styles.centro}>
        <EmptyState icon={icone} title={titulo} message={mensagem} />
      </View>
    </Screen>
  );
}

export const MuralEmBreve = () => (
  <EmBreve
    icone="newspaper"
    titulo="Mural em breve."
    mensagem="Os avisos da faculdade, do curso e das turmas vão aparecer aqui."
  />
);

const styles = StyleSheet.create({
  centro: { flex: 1, justifyContent: "center" },
});
