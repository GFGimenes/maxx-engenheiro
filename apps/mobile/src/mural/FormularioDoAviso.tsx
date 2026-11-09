import { useState, type ReactNode } from "react";
import {
  ActivityIndicator,
  Image,
  Pressable,
  StyleSheet,
  Text,
  View,
} from "react-native";

import { falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { Button } from "../components/Button";
import { Icon } from "../components/Icon";
import { TextField } from "../components/TextField";
import {
  FotoGrandeDemais,
  gerarDerivadas,
  type Derivadas,
} from "../fotos/derivadas";
import { escolherFoto } from "../fotos/envio";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { raios } from "../theme/tokens";
import { usePublicarAviso, type AlvoDoAviso } from "./api";

// Os limites do aviso (produto.md 7), como a API conta: um emoji vale um caractere.
export const TITULO_MINIMO = 3;
export const TITULO_MAXIMO = 120;
export const TEXTO_MAXIMO = 2000;

const caracteres = (texto: string) => Array.from(texto.trim()).length;

interface FormularioDoAvisoProps {
  // Para quem, já escolhido na seção de cima; nulo enquanto falta escolher.
  alvo: AlvoDoAviso | null;
  // A seção "para quem": turmas do professor, faculdade ou cursos da instituição.
  paraQuem: (ocupado: boolean, mudou: () => void) => ReactNode;
  placeholderDoTexto: string;
  onPublicado: () => void;
}

// O corpo do novo aviso (produto.md 7), do professor e da instituição: para quem, título e
// texto com contador, a foto opcional, que sobe como a do post, e o publicar. A recusa da
// API aparece aqui mesmo, sem perder o que foi digitado.
export function FormularioDoAviso({
  alvo,
  paraQuem,
  placeholderDoTexto,
  onPublicado,
}: FormularioDoAvisoProps) {
  const { colors } = useTheme();
  const publicar = usePublicarAviso();
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
    alvo !== null &&
    tamanhoDoTitulo >= TITULO_MINIMO &&
    !tituloLongo &&
    tamanhoDoTexto >= 1 &&
    !textoLongo;

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

  return (
    <View style={styles.coluna}>
      {paraQuem(ocupado, () => publicar.reset())}

      <View style={styles.campo}>
        <TextField
          label="Título"
          value={titulo}
          onChangeText={(novo) => {
            publicar.reset();
            setTitulo(novo);
          }}
          placeholder="Prova remarcada para 14/08"
          error={tituloLongo ? "O título passou de 120 caracteres." : undefined}
        />
        <Text
          style={[textStyles.legenda, styles.contador, { color: colors.ink3 }]}
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
          placeholder={placeholderDoTexto}
          error={textoLongo ? "O texto passou de 2.000 caracteres." : undefined}
        />
        <Text
          style={[textStyles.legenda, styles.contador, { color: colors.ink3 }]}
        >
          {`${tamanhoDoTexto.toLocaleString("pt-BR")}/2.000`}
        </Text>
      </View>

      <View style={styles.secao}>
        <Text role="heading" style={[textStyles.secao, { color: colors.ink }]}>
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
          alvo &&
          publicar.mutate(
            { alvo, titulo, texto, derivadas },
            { onSuccess: onPublicado },
          )
        }
      />
    </View>
  );
}

const styles = StyleSheet.create({
  coluna: { gap: 24 },
  secao: { gap: 8 },
  campo: { gap: 4 },
  contador: { alignSelf: "flex-end" },
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
