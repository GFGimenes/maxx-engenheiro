import { useState } from "react";
import { Image, StyleSheet, Text, View } from "react-native";

import { falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { Avatar } from "../components/Avatar";
import { Button } from "../components/Button";
import { descreverAutor, tempoDesde } from "../feed/textos";
import { Medalhao } from "../premios/Medalhao";
import { rotulosDeTier } from "../premios/rotulos";
import { periodo } from "../premiosParticipacao/textos";
import { fontFamily, textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, raios, tiers, type Tier } from "../theme/tokens";
import { useAprovar, useReprovar, type ComprovacaoNaFila } from "./api";
import { FolhaDeMotivo } from "./FolhaDeMotivo";

// Uma comprovação pendente na fila (produto.md 4.7): quem reivindicou, com curso e as
// matérias em comum, a foto, o prêmio com tier e pontos, o evento e a legenda; há quanto
// tempo, com a marca do que passou de 7 dias. Aprovar decide na hora; reprovar pede o
// motivo numa folha. Vale quem chega primeiro: se outra pessoa decidiu antes, a mensagem
// diz isso e a fila é relida. Sem prancheta: como o cartão do post, com as ações embaixo.
export function CartaoDeComprovacao({
  comprovacao,
}: {
  comprovacao: ComprovacaoNaFila;
}) {
  const { colors, name } = useTheme();
  const aprovar = useAprovar();
  const reprovar = useReprovar();
  const [reprovando, setReprovando] = useState(false);
  const tier = comprovacao.tier as Tier;
  const ocupado = aprovar.isPending || reprovar.isPending;
  const quem = [descreverAutor(comprovacao.aluno), ...comprovacao.materias]
    .filter(Boolean)
    .join(" · ");
  const evento = [
    comprovacao.evento.nome,
    periodo(comprovacao.evento.inicio, comprovacao.evento.fim),
    comprovacao.materia,
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <View
      testID={`comprovacao-${comprovacao.id}`}
      style={[styles.cartao, { borderBottomColor: colors.line }]}
    >
      <View style={styles.cabecalho}>
        <Avatar
          nome={comprovacao.aluno.nomeDeExibicao}
          foto={comprovacao.aluno.foto}
          size={44}
        />
        <View style={styles.texto}>
          <Text style={[textStyles.secao, { color: colors.ink }]}>
            {comprovacao.aluno.nomeDeExibicao}
          </Text>
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            {`${comprovacao.aluno.arroba} · ${quem}`}
          </Text>
        </View>
      </View>

      {comprovacao.foto && (
        <Image
          testID="foto-da-comprovacao"
          source={{ uri: comprovacao.foto.feed }}
          style={styles.foto}
          accessibilityIgnoresInvertColors
          accessibilityLabel={`Foto de ${comprovacao.aluno.nomeDeExibicao}`}
        />
      )}

      <View style={styles.corpo}>
        <View style={styles.premio}>
          <Medalhao tier={tier} glifo="taca" size={40} />
          <View style={styles.texto}>
            <Text style={[textStyles.secao, { color: colors.ink }]}>
              {comprovacao.nomeDoItem}
            </Text>
            <Text style={textStyles.legenda}>
              <Text style={{ color: tiers[tier].texto[name] }}>
                {rotulosDeTier[tier]}
              </Text>
              <Text
                style={{ color: colors.ink2 }}
              >{` · +${comprovacao.pontos}`}</Text>
            </Text>
            <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
              {evento}
            </Text>
            {comprovacao.evento.digitado && (
              <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
                Evento digitado pelo aluno.
              </Text>
            )}
          </View>
        </View>

        {comprovacao.legenda && (
          <Text style={[textStyles.corpo, { color: colors.ink }]}>
            {comprovacao.legenda}
          </Text>
        )}

        <View style={styles.quando}>
          <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
            {`Enviada ${tempoDesde(comprovacao.reivindicadaEm)}`}
          </Text>
          {comprovacao.passouDoPrazo && (
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

        {aprovar.isError && (
          <AvisoDeErro mensagem={falhaParaTela(aprovar.error).mensagem} />
        )}
        <View style={styles.acoes}>
          <View style={styles.metade}>
            <Button
              label={aprovar.isPending ? "Aprovando..." : "Aprovar"}
              accessibilityLabel={`Aprovar a comprovação de ${comprovacao.aluno.nomeDeExibicao}`}
              disabled={ocupado}
              onPress={() => aprovar.mutate(comprovacao.id)}
            />
          </View>
          <View style={styles.metade}>
            <Button
              label="Reprovar"
              accessibilityLabel={`Reprovar a comprovação de ${comprovacao.aluno.nomeDeExibicao}`}
              variant="secondary"
              disabled={ocupado}
              onPress={() => {
                reprovar.reset();
                setReprovando(true);
              }}
            />
          </View>
        </View>
      </View>

      <FolhaDeMotivo
        aberta={reprovando}
        titulo="Reprovar comprovação"
        explicacao={`O aluno vê o motivo. O post de ${comprovacao.aluno.nomeDeExibicao} fica como post comum, e ele pode tentar de novo com outra foto.`}
        confirmar="Reprovar"
        enviando={reprovar.isPending}
        erro={reprovar.isError ? falhaParaTela(reprovar.error).mensagem : null}
        onConfirmar={(motivo) =>
          reprovar.mutate(
            { comprovacaoId: comprovacao.id, motivo },
            { onSuccess: () => setReprovando(false) },
          )
        }
        onFechar={() => setReprovando(false)}
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
