import { useRouter } from "expo-router";
import { useState } from "react";
import {
  ActivityIndicator,
  Image,
  Pressable,
  StyleSheet,
  Text,
  View,
} from "react-native";

import { falhaParaTela } from "../api/FalhaDaApi";
import { useEu } from "../api/useEu";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { Button } from "../components/Button";
import { Cabecalho } from "../components/Cabecalho";
import { Icon } from "../components/Icon";
import { Screen } from "../components/Screen";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { TextField } from "../components/TextField";
import {
  FotoGrandeDemais,
  gerarDerivadas,
  type Derivadas,
} from "../fotos/derivadas";
import { escolherFoto } from "../fotos/envio";
import { Medalhao } from "../premios/Medalhao";
import { rotulosDeTier } from "../premios/rotulos";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { raios, tiers } from "../theme/tokens";
import {
  usePremiosParaPostar,
  usePublicarPost,
  type PremioParaPostar,
} from "./api";

export const LIMITE_DA_LEGENDA = 1000;

// Caracteres como a API conta: um emoji vale um.
const caracteres = (texto: string) => Array.from(texto).length;

// Novo post (produto.md 6.1): foto, legenda e prêmio, todos opcionais, mas ao menos um. Sem
// prancheta: segue a identidade visual e o "novo post" do Instagram, com a foto quadrada em
// cima.
export function NovoPostScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const eu = useEu();
  const ehAluno = eu.data?.papeis.includes("aluno") ?? false;
  const premios = usePremiosParaPostar(ehAluno);
  const publicar = usePublicarPost();

  const [derivadas, setDerivadas] = useState<Derivadas | null>(null);
  const [preparando, setPreparando] = useState(false);
  const [erroDaFoto, setErroDaFoto] = useState<string | null>(null);
  const [legenda, setLegenda] = useState("");
  const [medalhaId, setMedalhaId] = useState<string | null>(null);

  const tamanhoDaLegenda = caracteres(legenda);
  const legendaLonga = tamanhoDaLegenda > LIMITE_DA_LEGENDA;
  const vazio = legenda.trim() === "" && !derivadas && !medalhaId;
  const ocupado = preparando || publicar.isPending;

  const trocarFoto = async () => {
    setErroDaFoto(null);
    publicar.reset();
    try {
      const foto = await escolherFoto();
      if (!foto) return;
      setPreparando(true);
      setDerivadas(await gerarDerivadas(foto));
    } catch (erro) {
      setErroDaFoto(
        erro instanceof FotoGrandeDemais
          ? erro.message
          : "Não deu para abrir essa foto. Escolha outra.",
      );
    } finally {
      setPreparando(false);
    }
  };

  const enviar = () =>
    publicar.mutate(
      { legenda, derivadas, medalhaId },
      { onSuccess: () => router.dismissTo("/") },
    );

  if (eu.isSuccess && !ehAluno) {
    return (
      <Screen>
        <Cabecalho titulo="Novo post" voltarPara="/" />
        <EmptyState
          icon="imagem"
          title="Só aluno publica no feed."
          message="Professores e a instituição acompanham os posts dos alunos."
        />
      </Screen>
    );
  }

  return (
    <Screen scroll>
      <Cabecalho titulo="Novo post" voltarPara="/" />

      <View style={styles.coluna}>
        <Pressable
          role="button"
          accessibilityLabel={derivadas ? "Trocar foto" : "Escolher foto"}
          disabled={ocupado}
          onPress={() => void trocarFoto()}
          style={({ pressed }) => [
            styles.foto,
            {
              backgroundColor: colors.surface,
              borderColor: colors.line,
            },
            pressed && styles.pressionado,
          ]}
        >
          {derivadas ? (
            <Image
              testID="previa-da-foto"
              source={{ uri: derivadas.feed.uri }}
              style={styles.imagem}
              accessibilityIgnoresInvertColors
            />
          ) : preparando ? (
            <ActivityIndicator color={colors.ink2} />
          ) : (
            <View style={styles.convite}>
              <Icon name="imagem" color={colors.ink2} size={32} />
              <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
                Escolher foto
              </Text>
            </View>
          )}
        </Pressable>
        {derivadas && (
          <View style={styles.acoesDaFoto}>
            <View style={styles.metade}>
              <Button
                label="Trocar foto"
                variant="secondary"
                disabled={ocupado}
                onPress={() => void trocarFoto()}
              />
            </View>
            <View style={styles.metade}>
              <Button
                label="Remover foto"
                variant="secondary"
                disabled={ocupado}
                onPress={() => {
                  publicar.reset();
                  setDerivadas(null);
                }}
              />
            </View>
          </View>
        )}
        {erroDaFoto && <AvisoDeErro mensagem={erroDaFoto} />}

        <View style={styles.legenda}>
          <TextField
            label="Legenda"
            multiline
            value={legenda}
            onChangeText={(texto) => {
              publicar.reset();
              setLegenda(texto);
            }}
            placeholder="Conte o que aconteceu."
            error={
              legendaLonga ? "A legenda passou de 1.000 caracteres." : undefined
            }
          />
          <Text
            style={[
              textStyles.legenda,
              styles.contador,
              { color: colors.ink3 },
            ]}
          >
            {`${tamanhoDaLegenda.toLocaleString("pt-BR")}/1.000`}
          </Text>
        </View>

        <View style={styles.secao}>
          <Text
            role="heading"
            style={[textStyles.secao, { color: colors.ink }]}
          >
            Prêmio
          </Text>
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            Opcional. Cada prêmio rende um post.
          </Text>
          {premios.isPending && ehAluno && (
            <ActivityIndicator style={styles.espera} color={colors.ink2} />
          )}
          {premios.isError && (
            <ErrorState
              message={falhaParaTela(premios.error).mensagem}
              onRetry={() => void premios.refetch()}
            />
          )}
          {premios.isSuccess && premios.opcoes.length === 0 && (
            <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
              Seus prêmios que ainda não têm post aparecem aqui.
            </Text>
          )}
          {premios.opcoes.map((premio) => (
            <OpcaoDePremio
              key={premio.id}
              premio={premio}
              marcado={medalhaId === premio.id}
              onPress={() => {
                publicar.reset();
                setMedalhaId((atual) =>
                  atual === premio.id ? null : premio.id,
                );
              }}
            />
          ))}
        </View>

        {publicar.isError && (
          <AvisoDeErro mensagem={falhaParaTela(publicar.error).mensagem} />
        )}
        <Button
          label={publicar.isPending ? "Publicando..." : "Publicar"}
          disabled={vazio || legendaLonga || ocupado}
          onPress={enviar}
        />
      </View>
    </Screen>
  );
}

function OpcaoDePremio({
  premio,
  marcado,
  onPress,
}: {
  premio: PremioParaPostar;
  marcado: boolean;
  onPress: () => void;
}) {
  const { colors, name } = useTheme();
  const detalhe = [
    premio.materia,
    premio.bimestre ? `${premio.bimestre}º bimestre` : null,
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <Pressable
      role="checkbox"
      aria-checked={marcado}
      accessibilityLabel={`${premio.nome}, ${rotulosDeTier[premio.tier]}, ${premio.pontos} pontos${detalhe ? `, ${detalhe}` : ""}`}
      onPress={onPress}
      style={({ pressed }) => [
        styles.premio,
        {
          borderColor: marcado ? colors.ink : colors.line,
          backgroundColor: marcado ? colors.surface : colors.bg,
        },
        pressed && styles.pressionado,
      ]}
    >
      <Medalhao tier={premio.tier} glifo="fita" size={40} />
      <View style={styles.textoDoPremio}>
        <Text style={[textStyles.secao, { color: colors.ink }]}>
          {premio.nome}
        </Text>
        <Text style={textStyles.legenda}>
          <Text style={{ color: tiers[premio.tier].texto[name] }}>
            {rotulosDeTier[premio.tier]}
          </Text>
          <Text style={{ color: colors.ink2 }}>{` · +${premio.pontos}`}</Text>
        </Text>
        {detalhe !== "" && (
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            {detalhe}
          </Text>
        )}
      </View>
      {marcado && <Icon name="check" color={colors.ink} size={20} />}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  coluna: { gap: 16 },
  foto: {
    width: "100%",
    aspectRatio: 1,
    borderRadius: raios.cardMenor,
    borderWidth: 1,
    overflow: "hidden",
    alignItems: "center",
    justifyContent: "center",
  },
  imagem: { width: "100%", height: "100%" },
  convite: { alignItems: "center", gap: 8 },
  acoesDaFoto: { flexDirection: "row", gap: 8 },
  metade: { flex: 1 },
  legenda: { gap: 4 },
  contador: { alignSelf: "flex-end" },
  secao: { gap: 8 },
  espera: { marginTop: 8 },
  premio: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    padding: 12,
    borderWidth: 1,
    borderRadius: raios.cardMenor,
  },
  textoDoPremio: { flex: 1, gap: 2 },
  pressionado: { opacity: 0.85 },
});
