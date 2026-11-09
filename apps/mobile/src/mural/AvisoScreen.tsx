import { useLocalSearchParams, useRouter } from "expo-router";
import { useEffect } from "react";
import {
  ActivityIndicator,
  Image,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { Cabecalho } from "../components/Cabecalho";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { fontFamily, textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, raios } from "../theme/tokens";
import { useAviso, useLerAviso } from "./api";
import { autorDoAviso, dataDoAviso, rotuloDoEscopo } from "./textos";

// O aviso inteiro, com a imagem (produto.md 7), aberto pelo "Abrir" do mural e pelo link
// /aviso/:id. Somente leitura: sem comentário, sem curtida. Abrir registra a leitura, uma
// vez por visita: a API ignora a segunda.
export function AvisoScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const aviso = useAviso(id);
  const ler = useLerAviso();
  const naoExiste =
    aviso.isError &&
    falhaParaTela(aviso.error).codigo === "aviso-nao-encontrado";

  const carregado = aviso.isSuccess;
  const jaLido = aviso.data?.lido ?? true;
  const { mutate } = ler;
  useEffect(() => {
    if (carregado && !jaLido) {
      mutate(id);
    }
  }, [carregado, jaLido, id, mutate]);

  return (
    <ScrollView
      style={{ backgroundColor: colors.bg }}
      contentContainerStyle={{
        paddingTop: insets.top,
        paddingBottom: espacamento.respiroAntesDaNavegacao + insets.bottom,
      }}
    >
      <View style={styles.margem}>
        <Cabecalho titulo="Aviso" voltarPara="/mural" />
      </View>
      {aviso.isPending && (
        <ActivityIndicator style={styles.espera} color={colors.ink2} />
      )}
      {naoExiste && (
        <View style={styles.margem}>
          <EmptyState
            icon="newspaper"
            title="Esse aviso não está no seu mural."
            message="Os avisos da faculdade, do curso e das suas turmas ficam no Mural."
            action={{
              label: "Ir para o Mural",
              onPress: () => router.dismissTo("/mural"),
            }}
          />
        </View>
      )}
      {aviso.isError && !naoExiste && (
        <View style={styles.margem}>
          <ErrorState
            message={falhaParaTela(aviso.error).mensagem}
            onRetry={() => void aviso.refetch()}
          />
        </View>
      )}
      {aviso.isSuccess && (
        <View style={styles.margem}>
          <View style={styles.topo}>
            <Text
              numberOfLines={1}
              style={[textStyles.rotulo, styles.rotulo, { color: colors.ink2 }]}
            >
              {rotuloDoEscopo(aviso.data)}
            </Text>
            <Text style={[styles.data, { color: colors.ink2 }]}>
              {dataDoAviso(aviso.data.publicadoEm)}
            </Text>
          </View>
          <Text
            role="heading"
            style={[textStyles.titulo, styles.titulo, { color: colors.ink }]}
          >
            {aviso.data.titulo}
          </Text>
          <Text
            style={[textStyles.corpo, styles.autor, { color: colors.ink2 }]}
          >
            {autorDoAviso(aviso.data.autor)}
          </Text>
          {aviso.data.foto && (
            <Image
              testID="foto-do-aviso"
              source={{ uri: aviso.data.foto.feed }}
              accessibilityLabel={`Imagem do aviso ${aviso.data.titulo}`}
              accessibilityIgnoresInvertColors
              resizeMode="cover"
              style={[styles.foto, { backgroundColor: colors.surface }]}
            />
          )}
          <Text style={[textStyles.corpo, styles.texto, { color: colors.ink }]}>
            {aviso.data.texto}
          </Text>
        </View>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  margem: { paddingHorizontal: espacamento.margemLateral },
  espera: { marginTop: 32 },
  topo: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: 12,
    marginTop: 8,
  },
  rotulo: { flexShrink: 1 },
  data: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 700 }),
    fontSize: 12,
  },
  titulo: { marginTop: 8 },
  autor: { marginTop: 4 },
  foto: {
    marginTop: 16,
    width: "100%",
    aspectRatio: 1,
    borderRadius: raios.cardMaior,
  },
  texto: { marginTop: 16 },
});
