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
import { FilterChip } from "../components/FilterChip";
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
import { useTurmasDoProfessor } from "../professor/api";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { raios } from "../theme/tokens";
import { usePublicarAviso } from "./api";

// Os limites do aviso (produto.md 7), como a API conta: um emoji vale um caractere.
export const TITULO_MINIMO = 3;
export const TITULO_MAXIMO = 120;
export const TEXTO_MAXIMO = 2000;

const caracteres = (texto: string) => Array.from(texto.trim()).length;

// Novo aviso do professor (produto.md 7), pelo "+" do Mural. Sem prancheta: segue a
// identidade visual e o novo post. O professor escolhe uma ou mais turmas suas do semestre
// vigente, escreve título e texto e, se quiser, uma foto, que sobe como a do post. A
// recusa da API aparece aqui mesmo, sem perder o que foi digitado.
export function NovoAvisoScreen() {
  const { colors } = useTheme();
  const router = useRouter();
  const eu = useEu();
  const ehProfessor = eu.data?.papeis.includes("professor") ?? false;
  const turmas = useTurmasDoProfessor(ehProfessor);
  const publicar = usePublicarAviso();

  const [escolhidas, setEscolhidas] = useState<string[]>([]);
  const [titulo, setTitulo] = useState("");
  const [texto, setTexto] = useState("");
  const [derivadas, setDerivadas] = useState<Derivadas | null>(null);
  const [preparando, setPreparando] = useState(false);
  const [erroDaFoto, setErroDaFoto] = useState<string | null>(null);

  const tamanhoDoTitulo = caracteres(titulo);
  const tamanhoDoTexto = caracteres(texto);
  const tituloLongo = tamanhoDoTitulo > TITULO_MAXIMO;
  const textoLongo = tamanhoDoTexto > TEXTO_MAXIMO;
  const ocupado = preparando || publicar.isPending;
  const completo =
    escolhidas.length > 0 &&
    tamanhoDoTitulo >= TITULO_MINIMO &&
    !tituloLongo &&
    tamanhoDoTexto >= 1 &&
    !textoLongo;

  const alternar = (turmaId: string) => {
    publicar.reset();
    setEscolhidas((atuais) =>
      atuais.includes(turmaId)
        ? atuais.filter((id) => id !== turmaId)
        : [...atuais, turmaId],
    );
  };

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

  if (eu.isSuccess && !ehProfessor) {
    return (
      <Screen>
        <Cabecalho titulo="Novo aviso" voltarPara="/mural" />
        <EmptyState
          icon="newspaper"
          title="Só professor publica aviso aqui."
          message="Os avisos da faculdade e dos cursos saem pela área da instituição."
        />
      </Screen>
    );
  }

  return (
    <Screen scroll>
      <Cabecalho titulo="Novo aviso" voltarPara="/mural" />

      <View style={styles.coluna}>
        <View style={styles.secao}>
          <Text
            role="heading"
            style={[textStyles.secao, { color: colors.ink }]}
          >
            Para quais turmas
          </Text>
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            Escolha uma ou mais turmas suas deste semestre.
          </Text>
          {turmas.isPending && ehProfessor && (
            <ActivityIndicator style={styles.espera} color={colors.ink2} />
          )}
          {turmas.isError && (
            <ErrorState
              message={falhaParaTela(turmas.error).mensagem}
              onRetry={() => void turmas.refetch()}
            />
          )}
          {turmas.isSuccess && turmas.data.length === 0 && (
            <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
              Você não tem turma neste semestre.
            </Text>
          )}
          {turmas.isSuccess && turmas.data.length > 0 && (
            <View style={styles.chips}>
              {turmas.data.map((turma) => (
                <FilterChip
                  key={turma.id}
                  label={`${turma.materia} · ${turma.curso}`}
                  selected={escolhidas.includes(turma.id)}
                  disabled={ocupado}
                  onPress={() => alternar(turma.id)}
                />
              ))}
            </View>
          )}
        </View>

        <View style={styles.campo}>
          <TextField
            label="Título"
            value={titulo}
            onChangeText={(novo) => {
              publicar.reset();
              setTitulo(novo);
            }}
            placeholder="Prova remarcada para 14/08"
            error={
              tituloLongo ? "O título passou de 120 caracteres." : undefined
            }
          />
          <Text
            style={[
              textStyles.legenda,
              styles.contador,
              { color: colors.ink3 },
            ]}
          >
            {`${tamanhoDoTitulo}/${TITULO_MAXIMO}`}
          </Text>
        </View>

        <View style={styles.campo}>
          <TextField
            label="Texto"
            multiline
            value={texto}
            onChangeText={(novo) => {
              publicar.reset();
              setTexto(novo);
            }}
            placeholder="Escreva o aviso para a turma."
            error={
              textoLongo ? "O texto passou de 2.000 caracteres." : undefined
            }
          />
          <Text
            style={[
              textStyles.legenda,
              styles.contador,
              { color: colors.ink3 },
            ]}
          >
            {`${tamanhoDoTexto.toLocaleString("pt-BR")}/2.000`}
          </Text>
        </View>

        <View style={styles.secao}>
          <Text
            role="heading"
            style={[textStyles.secao, { color: colors.ink }]}
          >
            Foto
          </Text>
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            Opcional.
          </Text>
          <Pressable
            role="button"
            accessibilityLabel={derivadas ? "Trocar foto" : "Escolher foto"}
            disabled={ocupado}
            onPress={() => void trocarFoto()}
            style={({ pressed }) => [
              styles.foto,
              { backgroundColor: colors.surface, borderColor: colors.line },
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
            <Button
              label="Remover foto"
              variant="secondary"
              disabled={ocupado}
              onPress={() => {
                publicar.reset();
                setDerivadas(null);
              }}
            />
          )}
          {erroDaFoto && <AvisoDeErro mensagem={erroDaFoto} />}
        </View>

        {publicar.isError && (
          <AvisoDeErro mensagem={falhaParaTela(publicar.error).mensagem} />
        )}
        <Button
          label={publicar.isPending ? "Publicando..." : "Publicar aviso"}
          disabled={!completo || ocupado}
          onPress={() =>
            publicar.mutate(
              { turmaIds: escolhidas, titulo, texto, derivadas },
              { onSuccess: () => router.dismissTo("/mural") },
            )
          }
        />
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  coluna: { gap: 24 },
  secao: { gap: 8 },
  chips: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
  campo: { gap: 4 },
  contador: { alignSelf: "flex-end" },
  espera: { marginTop: 8 },
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
  pressionado: { opacity: 0.85 },
});
