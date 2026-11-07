import { useMutation } from "@tanstack/react-query";
import { useRouter } from "expo-router";
import { useState } from "react";
import { StyleSheet, Text, View } from "react-native";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi, falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { SegmentadoDeAcesso } from "../autenticacao/SegmentadoDeAcesso";
import { TelaDeAutenticacao } from "../autenticacao/TelaDeAutenticacao";
import { Button } from "../components/Button";
import { TextField } from "../components/TextField";
import { TextLink } from "../components/TextLink";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { useCadastro } from "./CadastroContext";
import { erroDaSenha } from "./regras";

// Passo 1 de 3, a prancheta do cadastro: RA, e-mail e senha. A sugestão de arroba que sai
// daqui já confere RA e e-mail na mesma linha da base, antes de pedir mais nada.
export function DadosScreen() {
  const { colors } = useTheme();
  const client = useApiClient();
  const router = useRouter();
  const { dados, comecar } = useCadastro();
  const [ra, setRa] = useState(dados?.ra ?? "");
  const [email, setEmail] = useState(dados?.email ?? "");
  const [senha, setSenha] = useState(dados?.senha ?? "");
  const [erros, setErros] = useState<{
    ra?: string;
    email?: string;
    senha?: string;
  }>({});

  const continuar = useMutation({
    mutationFn: async () => {
      const identificacao = { ra: ra.trim(), email: email.trim() };
      const { data, error } = await client.POST(
        "/api/v1/auth/cadastro/sugestao",
        { body: identificacao },
      );
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      // Voltar e avançar de novo mantém o que a pessoa já escolheu no passo 2.
      const mesmaPessoa =
        dados?.ra === identificacao.ra && dados.email === identificacao.email;
      comecar({
        ...identificacao,
        senha,
        sufixo: data.sufixo,
        prefixoSugerido: data.prefixo,
        nomeDeExibicao: mesmaPessoa ? dados.nomeDeExibicao : "",
        prefixo: mesmaPessoa ? dados.prefixo : data.prefixo,
      });
      router.push("/cadastro/arroba");
    },
  });
  const falha = continuar.isError ? falhaParaTela(continuar.error) : null;

  const enviar = () => {
    const novos = {
      ra: ra.trim() === "" ? "Informe o RA." : undefined,
      email:
        email.trim() === "" ? "Informe o e-mail institucional." : undefined,
      senha: erroDaSenha(senha),
    };
    setErros(novos);
    continuar.reset();
    if (!novos.ra && !novos.email && !novos.senha) {
      continuar.mutate();
    }
  };

  return (
    <TelaDeAutenticacao segmentado={<SegmentadoDeAcesso atual="cadastrar" />}>
      <View style={styles.formulario}>
        <TextField
          label="RA"
          value={ra}
          onChangeText={setRa}
          autoCapitalize="none"
          autoCorrect={false}
          inputMode="numeric"
          error={erros.ra}
        />
        <TextField
          label="E-mail institucional"
          placeholder="seu e-mail da faculdade"
          value={email}
          onChangeText={setEmail}
          autoCapitalize="none"
          autoCorrect={false}
          autoComplete="email"
          inputMode="email"
          error={erros.email}
        />
        <TextField
          label="Crie uma senha"
          placeholder="Mínimo de 8 caracteres"
          password
          value={senha}
          onChangeText={setSenha}
          autoComplete="new-password"
          onSubmitEditing={enviar}
          error={erros.senha}
        />

        <Button
          label={continuar.isPending ? "Conferindo..." : "Continuar"}
          size="large"
          disabled={continuar.isPending}
          onPress={enviar}
        />
        {falha && (
          <View style={styles.falha}>
            <AvisoDeErro mensagem={falha.mensagem} />
            {falha.codigo === "conta-existente" && (
              <TextLink
                label="Esqueci minha senha"
                onPress={() => router.push("/esqueci-senha")}
              />
            )}
          </View>
        )}

        <Text style={[textStyles.corpo, styles.aviso, { color: colors.ink2 }]}>
          Primeiro acesso? Aluno cria a conta em Cadastrar. Professor define a
          senha em Esqueci minha senha.
        </Text>
      </View>
    </TelaDeAutenticacao>
  );
}

const styles = StyleSheet.create({
  formulario: { gap: 16 },
  falha: { gap: 4 },
  aviso: { textAlign: "center", marginTop: 8 },
});
