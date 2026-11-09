import { useState } from "react";
import { Image, StyleSheet, Text, View } from "react-native";

import { falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { FolhaDeMotivo } from "../comprovacoes/FolhaDeMotivo";
import { Avatar } from "../components/Avatar";
import { Button } from "../components/Button";
import { descreverAutor, tempoDesde } from "../feed/textos";
import { Medalhao } from "../premios/Medalhao";
import { rotulosDeTier } from "../premios/rotulos";
import { periodo } from "../premiosParticipacao/textos";
import { fontFamily, textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, raios, tiers, type Tier } from "../theme/tokens";
import { useConfirmar, useRevogar, type ContestacaoNaFila } from "./api";

// Uma contestação aberta na fila da instituição (produto.md 4.7): o aluno, a foto quando o
// prêmio veio de reivindicação, o prêmio com tier e pontos, o evento, o motivo e quem
// contestou, há quanto tempo e a marca de 7 dias. Confirmar torna o prêmio definitivo na
// hora; revogar pede o motivo, que o aluno vê. Como o cartão da comprovação, com as ações
// embaixo. Sem prancheta: segue a identidade visual.
export function CartaoDeContestacao({
  contestacao,
}: {
  contestacao: ContestacaoNaFila;
}) {
  const { colors, name } = useTheme();
  const confirmar = useConfirmar();
  const revogar = useRevogar();
  const [revogando, setRevogando] = useState(false);
  const tier = contestacao.tier as Tier;
  const ocupado = confirmar.isPending || revogar.isPending;
  const nome = contestacao.aluno.nomeDeExibicao;
  const evento = [
    contestacao.evento.nome,
    periodo(contestacao.evento.inicio, contestacao.evento.fim),
    contestacao.materia,
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <View
      testID={`contestacao-${contestacao.id}`}
      style={[styles.cartao, { borderBottomColor: colors.line }]}
    >
      <View style={styles.cabecalho}>
        <Avatar nome={nome} foto={contestacao.aluno.foto} size={44} />
        <View style={styles.texto}>
          <Text style={[textStyles.secao, { color: colors.ink }]}>{nome}</Text>
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            {`${contestacao.aluno.arroba} · ${descreverAutor(contestacao.aluno)}`}
          </Text>
        </View>
      </View>

      {contestacao.foto ? (
        <Image
          testID="foto-da-contestacao"
          source={{ uri: contestacao.foto.feed }}
          style={styles.foto}
          accessibilityIgnoresInvertColors
          accessibilityLabel={`Foto de ${nome}`}
        />
      ) : null}

      <View style={styles.corpo}>
        <View style={styles.premio}>
          <Medalhao tier={tier} glifo="taca" size={40} />
          <View style={styles.texto}>
            <Text style={[textStyles.secao, { color: colors.ink }]}>
              {contestacao.nomeDoItem}
            </Text>
            <Text style={textStyles.legenda}>
              <Text style={{ color: tiers[tier].texto[name] }}>
                {rotulosDeTier[tier]}
              </Text>
              <Text
                style={{ color: colors.ink2 }}
              >{` · +${contestacao.pontos}`}</Text>
            </Text>
            <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
              {evento}
            </Text>
            {!contestacao.foto && (
              <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
                Concedido direto pela instituição, sem comprovação.
              </Text>
            )}
          </View>
        </View>

        <View
          style={[
            styles.motivo,
            { backgroundColor: colors.surface, borderColor: colors.line },
          ]}
        >
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            {`Contestado por ${contestacao.contestadaPor}`}
          </Text>
          <Text style={[textStyles.corpo, { color: colors.ink }]}>
            {contestacao.motivo}
          </Text>
        </View>

        <View style={styles.quando}>
          <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
            {`Contestado ${tempoDesde(contestacao.contestadaEm)}`}
          </Text>
          {contestacao.passouDoPrazo && (
            <View
              testID="marca-mais-de-7-dias"
              style={[styles.marca, { backgroundColor: colors.highlight }]}
            >
              <Text style={[styles.textoDaMarca, { color: colors.ink }]}>
                Mais de 7 dias
              </Text>
            </View>
          )}
        </View>

        {confirmar.isError && (
          <AvisoDeErro mensagem={falhaParaTela(confirmar.error).mensagem} />
        )}
        <View style={styles.acoes}>
          <View style={styles.metade}>
            <Button
              label={confirmar.isPending ? "Confirmando..." : "Confirmar"}
              accessibilityLabel={`Confirmar o prêmio de ${nome}`}
              disabled={ocupado}
              onPress={() => confirmar.mutate(contestacao.id)}
            />
          </View>
          <View style={styles.metade}>
            <Button
              label="Revogar"
              accessibilityLabel={`Revogar o prêmio de ${nome}`}
              variant="secondary"
              disabled={ocupado}
              onPress={() => {
                revogar.reset();
                setRevogando(true);
              }}
            />
          </View>
        </View>
      </View>

      <FolhaDeMotivo
        aberta={revogando}
        titulo="Revogar prêmio"
        explicacao={`${nome} vê o motivo. O prêmio sai da pontuação e fica no histórico, e o post dele fica como post comum.`}
        confirmar="Revogar"
        enviando={revogar.isPending}
        erro={revogar.isError ? falhaParaTela(revogar.error).mensagem : null}
        onConfirmar={(motivo) =>
          revogar.mutate(
            { contestacaoId: contestacao.id, motivo },
            { onSuccess: () => setRevogando(false) },
          )
        }
        onFechar={() => setRevogando(false)}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  cartao: { borderBottomWidth: 1, paddingBottom: 16 },
  cabecalho: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    paddingHorizontal: espacamento.margemLateral,
    paddingVertical: 12,
  },
  texto: { flex: 1, gap: 2 },
  foto: { width: "100%", aspectRatio: 1 },
  corpo: {
    paddingHorizontal: espacamento.margemLateral,
    paddingTop: 12,
    gap: 10,
  },
  premio: { flexDirection: "row", gap: 12, alignItems: "flex-start" },
  motivo: {
    borderWidth: 1,
    borderRadius: raios.filtros,
    padding: 12,
    gap: 4,
  },
  quando: { flexDirection: "row", alignItems: "center", gap: 8 },
  marca: {
    height: 26,
    borderRadius: raios.filtros,
    paddingHorizontal: 10,
    justifyContent: "center",
  },
  textoDaMarca: {
    fontFamily: fontFamily({ familia: "Archivo", peso: 700 }),
    fontSize: 12,
  },
  acoes: { flexDirection: "row", gap: 8 },
  metade: { flex: 1 },
});
