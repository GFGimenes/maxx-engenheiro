import { useEffect, useState } from "react";
import {
  ActivityIndicator,
  StyleSheet,
  Switch,
  Text,
  View,
} from "react-native";

import { falhaParaTela } from "../api/FalhaDaApi";
import { useEu } from "../api/useEu";
import { Screen } from "../components/Screen";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { Logo } from "../marca/Logo";
import { useVerTrofeus } from "../notificacoes/api";
import { TrilhaFamilia } from "../premios/TrilhaFamilia";
import { SecaoDeReivindicacoes } from "../premiosParticipacao/SecaoDeReivindicacoes";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { cores } from "../theme/tokens";
import { AbasSublinhadas } from "./AbasSublinhadas";
import { LinhaDePremio } from "./LinhaDePremio";
import {
  formatarPontos,
  itensDaAba,
  trilhasDaAba,
  type AbaDeTrofeus,
} from "./montagem";
import { usePremios } from "./usePremios";

const POLEGAR = cores.claro.bg;

const ABAS = [
  { valor: "trofeus", rotulo: "Troféus" },
  { valor: "medalhas", rotulo: "Medalhas" },
] as const;

// A tela de Troféus (prancheta trofeus-*.png): o catálogo do próprio aluno, o que falta, o
// critério e a raridade por escassez. Abrir a aba apaga a bolinha de prêmio novo (passo 10,
// pergunta 5), sem ler o sininho. A posição no ranking chega no passo 11.
export function TrofeusScreen() {
  const { colors } = useTheme();
  const eu = useEu();
  const ehAluno = eu.data?.papeis.includes("aluno") ?? false;
  const premios = usePremios(ehAluno);
  const { mutate: verTrofeus } = useVerTrofeus();
  useEffect(() => {
    if (ehAluno) {
      verTrofeus();
    }
  }, [ehAluno, verTrofeus]);
  const [aba, setAba] = useState<AbaDeTrofeus>("trofeus");
  const [mostrarNaoObtidos, setMostrarNaoObtidos] = useState(true);

  const itens = premios.data
    ? itensDaAba(premios.data.itens, aba, mostrarNaoObtidos)
    : [];
  const trilhas = premios.data ? trilhasDaAba(premios.data, aba) : [];

  return (
    <Screen scroll aboveNav>
      <View style={styles.topo}>
        <Logo largura={96} />
      </View>
      <View style={styles.titulo}>
        <Text role="heading" style={[textStyles.titulo, { color: colors.ink }]}>
          Troféus e medalhas
        </Text>
        {premios.data && (
          <Text style={[textStyles.secao, { color: colors.goldText }]}>
            {formatarPontos(premios.data.pontos)}
          </Text>
        )}
      </View>

      {(eu.isPending || (ehAluno && premios.isPending)) && (
        <ActivityIndicator style={styles.espera} color={colors.ink2} />
      )}
      {eu.isError && <ErrorState onRetry={() => void eu.refetch()} />}
      {eu.isSuccess && !ehAluno && (
        <EmptyState
          icon="taca"
          title="Troféus e medalhas são dos alunos."
          message="Quem dá aula concede medalhas em Minhas turmas, no início."
        />
      )}
      {premios.isError && (
        <ErrorState
          message={falhaParaTela(premios.error).mensagem}
          onRetry={() => void premios.refetch()}
        />
      )}
      {premios.isSuccess && (
        <>
          <AbasSublinhadas abas={ABAS} ativa={aba} onSelect={setAba} />
          <View style={styles.alternar}>
            <Text
              style={[textStyles.secao, styles.flex, { color: colors.ink }]}
            >
              Mostrar não obtidos
            </Text>
            <Switch
              accessibilityLabel="Mostrar não obtidos"
              value={mostrarNaoObtidos}
              onValueChange={setMostrarNaoObtidos}
              trackColor={{ false: colors.surface2, true: colors.primaryBg }}
              // Polegar branco nos dois temas, como na prancheta. Na web, o ligado tem
              // cor própria, e precisa ser dito também.
              thumbColor={POLEGAR}
              {...{ activeThumbColor: POLEGAR }}
            />
          </View>

          {aba === "trofeus" && <SecaoDeReivindicacoes ativo={ehAluno} />}

          {trilhas.map((trilha) => (
            <View key={trilha.codigo} style={styles.trilha}>
              <TrilhaFamilia
                titulo={trilha.titulo}
                resumo={trilha.resumo}
                itens={trilha.passos}
              />
            </View>
          ))}

          {itens.length === 0 ? (
            <EmptyState
              icon={aba === "medalhas" ? "fita" : "taca"}
              title={
                aba === "medalhas"
                  ? "Nenhuma medalha ainda."
                  : "Nenhum troféu ainda."
              }
              message="Ligue Mostrar não obtidos para ver o que falta e o critério de cada um."
            />
          ) : (
            itens.map((item) => <LinhaDePremio key={item.codigo} item={item} />)
          )}
        </>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  topo: { paddingTop: 16, paddingBottom: 8 },
  titulo: {
    flexDirection: "row",
    alignItems: "baseline",
    justifyContent: "space-between",
    gap: 8,
    paddingVertical: 8,
  },
  espera: { marginTop: 32 },
  alternar: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    minHeight: 56,
  },
  trilha: { marginBottom: 8 },
  flex: { flex: 1 },
});
