import { useState } from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";

import { Icon } from "../components/Icon";
import { fontFamily, textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { interacao, raios, tamanhos } from "../theme/tokens";
import type { Aviso } from "./api";
import { autorDoAviso, dataDoAviso, rotuloDoEscopo } from "./textos";

interface CartaoDeAvisoProps {
  aviso: Aviso;
  onAbrir: () => void;
}

// O cartão do mural (prancheta mural-*.png). O não lido vem aberto, com a bolinha dourada e
// o título em negrito, e diz "não lido" ao leitor de tela: o ouro sozinho não tem contraste
// no claro (identidade-visual.md). O lido vem recolhido, só o título e a seta; tocar abre
// o cartão ali mesmo, sem registrar leitura. "Abrir" leva ao aviso inteiro, com a imagem, e
// é ele que registra a leitura (passo 10, pergunta 2).
export function CartaoDeAviso({ aviso, onAbrir }: CartaoDeAvisoProps) {
  const { colors } = useTheme();
  const [expandido, setExpandido] = useState(false);
  const aberto = !aviso.lido || expandido;

  if (!aberto) {
    return (
      <Pressable
        role="button"
        aria-expanded={false}
        accessibilityLabel={`${aviso.titulo}. Mostrar aviso`}
        onPress={() => setExpandido(true)}
        style={({ pressed }) => [
          styles.cartao,
          styles.recolhido,
          { backgroundColor: colors.surface },
          pressed && { opacity: interacao.opacidadePressionado },
        ]}
      >
        <Text
          numberOfLines={2}
          style={[styles.tituloRecolhido, { color: colors.ink }]}
        >
          {aviso.titulo}
        </Text>
        <Icon name="abrir" color={colors.ink2} size={22} />
      </Pressable>
    );
  }

  return (
    <View
      testID={`aviso-${aviso.id}`}
      style={[styles.cartao, { backgroundColor: colors.surface }]}
    >
      <View style={styles.topo}>
        <View style={styles.escopo}>
          {!aviso.lido && (
            <View
              testID="bolinha-nao-lido"
              style={[styles.bolinha, { backgroundColor: colors.gold }]}
            />
          )}
          <Text
            numberOfLines={1}
            style={[textStyles.rotulo, styles.rotulo, { color: colors.ink2 }]}
          >
            {rotuloDoEscopo(aviso)}
          </Text>
        </View>
        <Text style={[styles.data, { color: colors.ink2 }]}>
          {dataDoAviso(aviso.publicadoEm)}
        </Text>
      </View>
      <Text
        role="heading"
        accessibilityLabel={
          aviso.lido ? aviso.titulo : `${aviso.titulo}, não lido`
        }
        style={[styles.titulo, { color: colors.ink }]}
      >
        {aviso.titulo}
      </Text>
      <Text
        numberOfLines={4}
        style={[textStyles.corpo, styles.texto, { color: colors.ink }]}
      >
        {aviso.texto}
      </Text>
      <View style={styles.rodape}>
        <Text
          numberOfLines={1}
          style={[textStyles.corpo, styles.autor, { color: colors.ink2 }]}
        >
          {autorDoAviso(aviso.autor)}
        </Text>
        <Pressable
          role="link"
          accessibilityLabel={`Abrir o aviso ${aviso.titulo}`}
          hitSlop={8}
          onPress={onAbrir}
          style={({ pressed }) => [
            styles.abrir,
            pressed && { opacity: interacao.opacidadePressionado },
          ]}
        >
          <Text style={[styles.textoAbrir, { color: colors.link }]}>Abrir</Text>
          <Icon name="avancar" color={colors.link} size={18} />
        </Pressable>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  cartao: {
    borderRadius: raios.cardMaior,
    padding: 16,
  },
  recolhido: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    minHeight: tamanhos.alvoDeToque + 12,
  },
  tituloRecolhido: {
    flex: 1,
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 700 }),
    fontSize: 16,
    lineHeight: 20,
  },
  topo: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: 12,
  },
  escopo: { flexDirection: "row", alignItems: "center", gap: 8, flex: 1 },
  bolinha: {
    width: tamanhos.bolinhaNovidade.diametro,
    height: tamanhos.bolinhaNovidade.diametro,
    borderRadius: tamanhos.bolinhaNovidade.diametro / 2,
  },
  rotulo: { flexShrink: 1 },
  data: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 700 }),
    fontSize: 12,
  },
  titulo: {
    marginTop: 8,
    fontFamily: fontFamily({ familia: "Archivo", peso: 800 }),
    fontSize: 17,
    lineHeight: 22,
  },
  texto: { marginTop: 8 },
  rodape: {
    marginTop: 12,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: 12,
  },
  autor: { flexShrink: 1 },
  abrir: { flexDirection: "row", alignItems: "center", gap: 2 },
  textoAbrir: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 700 }),
    fontSize: 14,
  },
});
