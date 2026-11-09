import { useRouter } from "expo-router";
import { Platform, StyleSheet, Text, View } from "react-native";

import { Avatar } from "../components/Avatar";
import { Button } from "../components/Button";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";

// A aba Perfil da conta só de instituição (P1 do passo 12). A instituição não tem perfil
// social (produto.md 2.1): não posta, não é seguida, não aparece em ranking. No lugar do
// perfil, quem é a conta e a entrada da área da instituição, que abre só no navegador.
export function ContaDaInstituicao({
  nome,
  arroba,
}: {
  nome: string;
  arroba: string;
}) {
  const { colors } = useTheme();
  const router = useRouter();
  const naWeb = Platform.OS === "web";

  return (
    <View style={styles.conta}>
      <Avatar nome={nome} size={84} />
      <View style={styles.texto}>
        <Text role="heading" style={[textStyles.secao, { color: colors.ink }]}>
          {nome}
        </Text>
        <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
          {arroba}
        </Text>
      </View>
      <Text style={[textStyles.corpo, styles.centro, { color: colors.ink2 }]}>
        {naWeb
          ? "A instituição não tem perfil social. A voz dela é o mural, e os atos ficam na área da instituição."
          : "A instituição não tem perfil social. A área da instituição abre no navegador."}
      </Text>
      {naWeb && (
        <View style={styles.botao}>
          <Button
            label="Área da instituição"
            onPress={() => router.push("/instituicao")}
          />
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  conta: { alignItems: "center", gap: 16, paddingTop: 32 },
  texto: { alignItems: "center", gap: 2 },
  centro: { textAlign: "center" },
  botao: { alignSelf: "stretch" },
});
