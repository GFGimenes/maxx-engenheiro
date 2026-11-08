import { useRouter } from "expo-router";
import { useState } from "react";
import { ActivityIndicator, Image, StyleSheet, Text, View } from "react-native";

import { falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { useDisponibilidadeDoMeuPrefixo, useEditarPerfil } from "../busca/api";
import { erroDoNome } from "../cadastro/regras";
import { Avatar } from "../components/Avatar";
import { Button } from "../components/Button";
import { Cabecalho } from "../components/Cabecalho";
import { Screen } from "../components/Screen";
import { ErrorState } from "../components/StateMessage";
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
import { usePerfil, type Perfil } from "./api";

// Editar perfil (produto.md 6.5): foto, nome de exibição e a parte do arroba antes do hífen.
// Sem prancheta: segue a identidade visual e o "editar perfil" do Instagram.
export function EditarPerfilScreen() {
  const { colors } = useTheme();
  const perfil = usePerfil();

  return (
    <Screen scroll>
      <Cabecalho titulo="Editar perfil" voltarPara="/perfil" />
      {perfil.isPending && (
        <ActivityIndicator style={styles.espera} color={colors.ink2} />
      )}
      {perfil.isError && (
        <ErrorState
          message={falhaParaTela(perfil.error).mensagem}
          onRetry={() => void perfil.refetch()}
        />
      )}
      {perfil.isSuccess && <Formulario perfil={perfil.data} />}
    </Screen>
  );
}

// O arroba "@anaSouza-CC" em prefixo e sufixo: só o prefixo muda aqui.
function partesDoArroba(arroba: string) {
  const [prefixo = "", sufixo = ""] = arroba.replace(/^@/, "").split("-");
  return { prefixo, sufixo };
}

function Formulario({ perfil }: { perfil: Perfil }) {
  const { colors } = useTheme();
  const router = useRouter();
  const editar = useEditarPerfil();
  const atual = partesDoArroba(perfil.arroba);
  const [nome, setNome] = useState(perfil.nomeDeExibicao);
  const [prefixo, setPrefixo] = useState(atual.prefixo);
  // undefined: a foto como está; null: remover; Derivadas: a nova.
  const [foto, setFoto] = useState<Derivadas | null | undefined>(undefined);
  const [preparando, setPreparando] = useState(false);
  const [erroDaFoto, setErroDaFoto] = useState<string | null>(null);
  const disponibilidade = useDisponibilidadeDoMeuPrefixo(
    prefixo,
    atual.prefixo,
  );

  const erroNome = erroDoNome(nome);
  const erroPrefixo =
    disponibilidade.estado === "invalido"
      ? disponibilidade.mensagem
      : disponibilidade.estado === "indisponivel"
        ? "Esse arroba já é de outra pessoa."
        : undefined;
  const nomeMudou = nome.trim() !== perfil.nomeDeExibicao;
  const prefixoMudou = prefixo !== atual.prefixo;
  const mudou = nomeMudou || prefixoMudou || foto !== undefined;
  const fotoAtual = foto === undefined ? perfil.foto?.feed : foto?.feed.uri;

  const trocarFoto = async () => {
    setErroDaFoto(null);
    editar.reset();
    try {
      const escolhida = await escolherFoto();
      if (!escolhida) return;
      setPreparando(true);
      setFoto(await gerarDerivadas(escolhida));
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

  const salvar = () =>
    editar.mutate(
      {
        nomeDeExibicao: nomeMudou ? nome.trim() : undefined,
        prefixo: prefixoMudou ? prefixo : undefined,
        foto,
      },
      {
        onSuccess: () =>
          router.canGoBack() ? router.back() : router.replace("/perfil"),
      },
    );

  return (
    <View style={styles.coluna}>
      <View style={styles.foto}>
        {fotoAtual ? (
          <Image
            testID="foto-de-perfil"
            source={{ uri: fotoAtual }}
            style={[styles.circulo, { backgroundColor: colors.avatarBg }]}
            accessibilityIgnoresInvertColors
            accessibilityLabel="Foto de perfil"
          />
        ) : (
          <Avatar nome={nome.trim() || perfil.nomeDeExibicao} size={96} />
        )}
        {preparando && <ActivityIndicator color={colors.ink2} />}
        <View style={styles.acoesDaFoto}>
          <View style={styles.metade}>
            <Button
              label={fotoAtual ? "Trocar foto" : "Escolher foto"}
              variant="secondary"
              disabled={preparando || editar.isPending}
              onPress={() => void trocarFoto()}
            />
          </View>
          {fotoAtual && (
            <View style={styles.metade}>
              <Button
                label="Remover foto"
                variant="secondary"
                disabled={preparando || editar.isPending}
                onPress={() => {
                  editar.reset();
                  setFoto(perfil.foto ? null : undefined);
                }}
              />
            </View>
          )}
        </View>
        {erroDaFoto && <AvisoDeErro mensagem={erroDaFoto} />}
      </View>

      <TextField
        label="Nome de exibição"
        value={nome}
        onChangeText={(texto) => {
          editar.reset();
          setNome(texto);
        }}
        error={erroNome}
      />

      <View style={styles.arroba}>
        <TextField
          label="Arroba"
          value={prefixo}
          onChangeText={(texto) => {
            editar.reset();
            setPrefixo(texto.trim());
          }}
          autoCapitalize="none"
          autoCorrect={false}
          error={erroPrefixo}
        />
        <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
          {`Seu arroba: @${prefixo || "…"}-${atual.sufixo}. O sufixo vem do curso e não muda aqui.`}
        </Text>
        {disponibilidade.estado === "verificando" &&
          prefixoMudou &&
          !erroPrefixo && (
            <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
              Conferindo se está livre…
            </Text>
          )}
      </View>

      {editar.isError && (
        <AvisoDeErro mensagem={falhaParaTela(editar.error).mensagem} />
      )}
      <Button
        label={editar.isPending ? "Salvando..." : "Salvar"}
        disabled={
          !mudou ||
          !!erroNome ||
          !!erroPrefixo ||
          preparando ||
          editar.isPending
        }
        onPress={salvar}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  espera: { marginTop: 32 },
  coluna: { gap: 20, paddingTop: 8 },
  foto: { alignItems: "center", gap: 12 },
  circulo: { width: 96, height: 96, borderRadius: raios.pill },
  acoesDaFoto: { flexDirection: "row", gap: 8, alignSelf: "stretch" },
  metade: { flex: 1 },
  arroba: { gap: 6 },
});
