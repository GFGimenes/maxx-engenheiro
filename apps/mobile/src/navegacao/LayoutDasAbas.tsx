import { Slot, usePathname, useRouter, type Href } from "expo-router";
import { StyleSheet, View } from "react-native";

import { useEu } from "../api/useEu";
import { BottomNav, type Aba } from "../components/BottomNav";
import { useNovidades } from "../notificacoes/api";
import { useTheme } from "../theme/ThemeProvider";

const ROTAS: Record<Aba, Href> = {
  home: "/",
  busca: "/busca",
  mural: "/mural",
  trofeus: "/trofeus",
  perfil: "/perfil",
};

export function abaDaRota(caminho: string): Aba {
  const encontrada = (Object.entries(ROTAS) as [Aba, string][]).find(
    ([aba, rota]) => aba !== "home" && caminho.startsWith(rota),
  );
  return encontrada?.[0] ?? "home";
}

// As cinco abas, na ordem do design: Home, Busca, Mural, Troféus, Perfil (produto.md 10).
// Troféus desde o passo 6, Busca e Perfil desde o passo 7, Mural desde o passo 10.
// A bolinha de novidade (aviso não lido no Mural, prêmio novo em Troféus) vem das
// novidades, perguntadas ao focar e a cada 60 s, desde o passo 10.
export function LayoutDasAbas() {
  const { colors } = useTheme();
  const router = useRouter();
  const caminho = usePathname();
  const eu = useEu();
  const novidades = useNovidades();

  return (
    <View style={[styles.fill, { backgroundColor: colors.bg }]}>
      <View style={styles.fill}>
        <Slot />
      </View>
      <BottomNav
        active={abaDaRota(caminho)}
        nome={eu.data?.nomeDeExibicao ?? ""}
        novidades={{
          mural: novidades.data?.mural ?? false,
          trofeus: novidades.data?.trofeus ?? false,
        }}
        onSelect={(aba) => router.navigate(ROTAS[aba])}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
});
