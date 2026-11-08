import { useRouter } from "expo-router";
import { useId } from "react";
import { Image, Pressable, StyleSheet, Text, View } from "react-native";
import Svg, { Defs, LinearGradient, Rect, Stop } from "react-native-svg";

import type { Post } from "../feed/api";
import { Medalhao } from "../premios/Medalhao";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { tiers, type Tier } from "../theme/tokens";

// Uma célula da grade de três colunas do perfil, sem legenda (identidade-visual.md). Com
// foto, a derivada da grade e, se houver prêmio, o medalhão pequeno no canto. Sem foto, o
// medalhão sobre o metal do tier, como na prancheta. Só com legenda (decisão D10), o começo
// dela sobre `surface`.
export function CelulaDaGrade({ post, lado }: { post: Post; lado: number }) {
  const { colors, name } = useTheme();
  const router = useRouter();
  const gradiente = `grade${useId().replace(/[^a-zA-Z0-9]/g, "")}`;
  const premio = post.premio;
  const tier = premio?.tier as Tier | undefined;
  const glifo = premio?.tipo === "medalha" ? "fita" : "taca";

  const descricao = post.foto
    ? "Post com foto"
    : premio
      ? `Post do prêmio ${premio.nome}`
      : "Post de texto";

  return (
    <Pressable
      testID={`celula-${post.id}`}
      role="link"
      accessibilityLabel={descricao}
      onPress={() => router.push(`/post/${post.id}`)}
      style={({ pressed }) => [
        { width: lado, height: lado, backgroundColor: colors.surface },
        pressed && styles.pressionado,
      ]}
    >
      {post.foto ? (
        <>
          <Image
            source={{ uri: post.foto.grade }}
            style={styles.cheio}
            accessibilityIgnoresInvertColors
          />
          {tier && (
            <View style={styles.canto}>
              <Medalhao tier={tier} glifo={glifo} size={22} />
            </View>
          )}
        </>
      ) : tier ? (
        <View style={styles.centro}>
          <Svg width={lado} height={lado} style={StyleSheet.absoluteFill}>
            <Defs>
              <LinearGradient id={gradiente} x1="0" y1="0" x2="1" y2="1">
                <Stop offset="0" stopColor={tiers[tier].fundoGrade[name][0]} />
                <Stop offset="1" stopColor={tiers[tier].fundoGrade[name][1]} />
              </LinearGradient>
            </Defs>
            <Rect width={lado} height={lado} fill={`url(#${gradiente})`} />
          </Svg>
          <Medalhao tier={tier} glifo={glifo} size={Math.round(lado * 0.45)} />
        </View>
      ) : (
        <Text
          numberOfLines={5}
          style={[textStyles.legenda, styles.legenda, { color: colors.ink }]}
        >
          {post.legenda}
        </Text>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  cheio: { width: "100%", height: "100%" },
  canto: { position: "absolute", top: 6, right: 6 },
  centro: { flex: 1, alignItems: "center", justifyContent: "center" },
  legenda: { padding: 8 },
  pressionado: { opacity: 0.85 },
});
