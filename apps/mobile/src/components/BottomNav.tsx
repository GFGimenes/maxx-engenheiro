import type { ReactNode } from "react";
import { Pressable, StyleSheet, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { useTheme } from "../theme/ThemeProvider";
import { interacao, raios, tamanhos } from "../theme/tokens";
import { Avatar } from "./Avatar";
import { focusRingStyle, noOutline, useFocusRing } from "./focusRing";
import { Icon, type IconName } from "./Icon";

export type Aba = "home" | "busca" | "mural" | "trofeus" | "perfil";

const ABAS_COM_ICONE: {
  aba: Exclude<Aba, "perfil">;
  rotulo: string;
  icone: IconName;
}[] = [
  { aba: "home", rotulo: "Home", icone: "home" },
  { aba: "busca", rotulo: "Busca", icone: "search" },
  { aba: "mural", rotulo: "Mural", icone: "newspaper" },
  { aba: "trofeus", rotulo: "Troféus", icone: "taca" },
];

interface BottomNavProps {
  active: Aba;
  onSelect: (aba: Aba) => void;
  // Nome do aluno, para o avatar da aba Perfil.
  nome: string;
  // Aviso não lido no Mural; prêmio novo em Troféus.
  novidades?: Partial<Record<"mural" | "trofeus", boolean>>;
}

const RESPIRO_NO_TOPO = 8;
// O que a aba precisa acima da área segura. Com os 34 do iPhone de referência, dá os 84.
const ALTURA_SEM_AREA_SEGURA = 50;

export function BottomNav({
  active,
  onSelect,
  nome,
  novidades = {},
}: BottomNavProps) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();

  return (
    <View
      role="tablist"
      testID="navegacao"
      style={[
        styles.barra,
        {
          // 84 de mínimo; cresce só se a área segura do aparelho exigir.
          height: Math.max(
            tamanhos.navegacaoInferior,
            ALTURA_SEM_AREA_SEGURA + insets.bottom,
          ),
          backgroundColor: colors.bg,
          borderTopColor: colors.line,
        },
      ]}
    >
      {ABAS_COM_ICONE.map(({ aba, rotulo, icone }) => {
        const ativa = aba === active;
        return (
          <Tab
            key={aba}
            rotulo={rotulo}
            ativa={ativa}
            novidade={
              (aba === "mural" || aba === "trofeus") && !!novidades[aba]
            }
            onPress={() => onSelect(aba)}
          >
            <Icon
              name={icone}
              size={tamanhos.iconeNav}
              color={ativa ? colors.ink : colors.ink2}
              filled={ativa}
              detailColor={colors.navHighlight}
            />
          </Tab>
        );
      })}
      <Tab
        rotulo="Perfil"
        ativa={active === "perfil"}
        novidade={false}
        onPress={() => onSelect("perfil")}
      >
        <Avatar nome={nome} size={tamanhos.avatarNav} />
      </Tab>
    </View>
  );
}

function Tab({
  rotulo,
  ativa,
  novidade,
  onPress,
  children,
}: {
  rotulo: string;
  ativa: boolean;
  novidade: boolean;
  onPress: () => void;
  children: ReactNode;
}) {
  const { colors } = useTheme();
  const { focusVisible, focusProps } = useFocusRing();

  return (
    <Pressable
      role="tab"
      aria-selected={ativa}
      // A bolinha sozinha não diz nada a quem não vê.
      aria-label={novidade ? `${rotulo}, com novidade` : rotulo}
      onPress={onPress}
      {...focusProps}
      style={[styles.aba, noOutline]}
    >
      {({ pressed }) => (
        <View
          testID={`realce-${rotulo}`}
          style={[
            styles.realce,
            ativa && { backgroundColor: colors.navHighlight },
            pressed && { opacity: interacao.opacidadePressionado },
            focusVisible && focusRingStyle(colors.focus),
          ]}
        >
          <View>
            {children}
            {novidade && (
              <View
                testID={`novidade-${rotulo}`}
                style={[
                  styles.bolinha,
                  { backgroundColor: colors.gold, borderColor: colors.bg },
                ]}
              />
            )}
          </View>
        </View>
      )}
    </Pressable>
  );
}

const { diametro, halo } = tamanhos.bolinhaNovidade;

const styles = StyleSheet.create({
  barra: {
    flexDirection: "row",
    borderTopWidth: 1,
    paddingTop: RESPIRO_NO_TOPO,
  },
  aba: { flex: 1, alignItems: "center" },
  realce: {
    width: tamanhos.realceAbaAtiva,
    height: tamanhos.realceAbaAtiva,
    borderRadius: raios.realceNav,
    alignItems: "center",
    justifyContent: "center",
  },
  // A borda é o halo, na cor do fundo: separa o ouro do que estiver atrás.
  bolinha: {
    position: "absolute",
    top: -halo - 1,
    right: -halo - 1,
    width: diametro + 2 * halo,
    height: diametro + 2 * halo,
    borderRadius: raios.pill,
    borderWidth: halo,
  },
});
