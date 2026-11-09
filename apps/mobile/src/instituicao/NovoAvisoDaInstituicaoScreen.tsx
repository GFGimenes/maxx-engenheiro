import { useRouter } from "expo-router";
import { useState } from "react";
import {
  ActivityIndicator,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { useCursos } from "../busca/api";
import { FilterChip } from "../components/FilterChip";
import { SegmentedControl } from "../components/SegmentedControl";
import { ErrorState } from "../components/StateMessage";
import { FormularioDoAviso } from "../mural/FormularioDoAviso";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento } from "../theme/tokens";
import { TelaDaArea } from "./TelaDaArea";

type Escopo = "faculdade" | "curso";

const ESCOPOS = [
  { value: "faculdade", label: "Faculdade" },
  { value: "curso", label: "Cursos" },
] as const;

// O aviso da instituição (produto.md 7, passo 12): para a faculdade inteira ou para um ou
// mais cursos, na mesma publicação, com título, texto e foto opcional, como o aviso do
// professor. A notificação vai pelo worker aos alunos ativos alcançados e, no da faculdade,
// também aos professores. Sem prancheta: segue a identidade visual e o novo aviso.
export function NovoAvisoDaInstituicaoScreen() {
  return (
    <TelaDaArea titulo="Novo aviso" voltarPara="/instituicao">
      <Formulario />
    </TelaDaArea>
  );
}

function Formulario() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const cursos = useCursos();
  const [escopo, setEscopo] = useState<Escopo>("faculdade");
  const [escolhidos, setEscolhidos] = useState<string[]>([]);
  const alvo =
    escopo === "faculdade"
      ? ({ escopo: "faculdade" } as const)
      : escolhidos.length > 0
        ? ({ escopo: "curso", cursoIds: escolhidos } as const)
        : null;

  return (
    <ScrollView
      contentContainerStyle={[
        styles.margem,
        { paddingBottom: espacamento.respiroAntesDaNavegacao + insets.bottom },
      ]}
    >
      <FormularioDoAviso
        alvo={alvo}
        placeholderDoTexto="Escreva o aviso para os alunos."
        onPublicado={() => router.dismissTo("/mural")}
        paraQuem={(ocupado, mudou) => (
          <View style={styles.secao}>
            <Text
              role="heading"
              style={[textStyles.secao, { color: colors.ink }]}
            >
              Para quem
            </Text>
            <SegmentedControl
              label="Para quem"
              options={ESCOPOS}
              value={escopo}
              disabled={ocupado}
              onChange={(novo) => {
                mudou();
                setEscopo(novo);
              }}
            />
            <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
              {escopo === "faculdade"
                ? "Todos os alunos e os professores veem no mural."
                : "Os alunos dos cursos escolhidos veem no mural. Escolha um ou mais."}
            </Text>
            {escopo === "curso" && cursos.isPending && (
              <ActivityIndicator color={colors.ink2} />
            )}
            {escopo === "curso" && cursos.isError && (
              <ErrorState
                message={falhaParaTela(cursos.error).mensagem}
                onRetry={() => void cursos.refetch()}
              />
            )}
            {escopo === "curso" && cursos.isSuccess && (
              <View style={styles.chips}>
                {cursos.data.map((curso) => (
                  <FilterChip
                    key={curso.id}
                    label={curso.sigla}
                    selected={escolhidos.includes(curso.id)}
                    disabled={ocupado}
                    onPress={() => {
                      mudou();
                      setEscolhidos((atuais) =>
                        atuais.includes(curso.id)
                          ? atuais.filter((id) => id !== curso.id)
                          : [...atuais, curso.id],
                      );
                    }}
                  />
                ))}
              </View>
            )}
          </View>
        )}
      />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  margem: { paddingHorizontal: espacamento.margemLateral },
  secao: { gap: 8 },
  chips: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
});
