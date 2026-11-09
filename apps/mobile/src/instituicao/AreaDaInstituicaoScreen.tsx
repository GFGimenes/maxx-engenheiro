import { useRouter, type Href } from "expo-router";
import { Pressable, StyleSheet, Text, View } from "react-native";

import { Icon, type IconName } from "../components/Icon";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, tamanhos } from "../theme/tokens";
import { TelaDaArea } from "./TelaDaArea";

interface Entrada {
  titulo: string;
  descricao: string;
  icone: IconName;
  destino: Href;
}

// O que a área da instituição oferece (produto.md 10.2). Avisos e a purga chegam na etapa 9
// do passo 12.
const ENTRADAS: Entrada[] = [
  {
    titulo: "Alunos",
    descricao: "Ficha, e-mail, curso, ex-aluno, medalhas e prêmios.",
    icone: "user",
    destino: "/instituicao/alunos",
  },
  {
    titulo: "Comprovações",
    descricao: "Reivindicações com foto esperando aprovação.",
    icone: "inbox",
    destino: "/comprovacoes",
  },
  {
    titulo: "Contestações",
    descricao: "Prêmios em averiguação: confirmar ou revogar.",
    icone: "taca",
    destino: "/instituicao/contestacoes",
  },
];

// A entrada da área da instituição, só no navegador (P1 do passo 12): uma lista simples,
// como o menu de configurações do Instagram, cada linha levando a uma tela. Sem prancheta:
// segue a identidade visual.
export function AreaDaInstituicaoScreen() {
  const { colors } = useTheme();
  const router = useRouter();

  return (
    <TelaDaArea titulo="Área da instituição" voltarPara="/perfil">
      <View style={styles.lista}>
        {ENTRADAS.map((entrada) => (
          <Pressable
            key={entrada.titulo}
            role="button"
            accessibilityLabel={entrada.titulo}
            onPress={() => router.push(entrada.destino)}
            style={({ pressed }) => [
              styles.linha,
              { borderBottomColor: colors.line },
              pressed && styles.pressionado,
            ]}
          >
            <Icon
              name={entrada.icone}
              color={colors.ink}
              size={tamanhos.iconeNav}
            />
            <View style={styles.texto}>
              <Text style={[textStyles.secao, { color: colors.ink }]}>
                {entrada.titulo}
              </Text>
              <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                {entrada.descricao}
              </Text>
            </View>
            <Icon name="avancar" color={colors.ink3} size={20} />
          </Pressable>
        ))}
      </View>
    </TelaDaArea>
  );
}

const styles = StyleSheet.create({
  lista: { paddingHorizontal: espacamento.margemLateral },
  linha: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    minHeight: 64,
    borderBottomWidth: 1,
  },
  texto: { flex: 1, gap: 2 },
  pressionado: { opacity: 0.85 },
});
