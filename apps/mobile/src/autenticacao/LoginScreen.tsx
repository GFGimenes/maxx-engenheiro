import { useMutation } from "@tanstack/react-query";
import { useRouter } from "expo-router";
import { useState } from "react";
import { StyleSheet, Text, View } from "react-native";

import { useApiClient } from "../api/ApiClientProvider";
import { codigoDoErro, mensagemDaFalha, mensagemDeErro } from "../api/erros";
import { Button } from "../components/Button";
import { TextField } from "../components/TextField";
import { TextLink } from "../components/TextLink";
import { useDemonstracao } from "../demonstracao/useDemonstracao";
import { useSessao } from "../sessao/SessaoProvider";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { AvisoDeErro } from "./AvisoDeErro";
import { SegmentadoDeAcesso } from "./SegmentadoDeAcesso";
import { TelaDeAutenticacao } from "./TelaDeAutenticacao";

// Onde cada recusa aparece: a credencial errada fica no campo da senha, como no Instagram;
// o resto, abaixo do botão.
class Falha extends Error {
  constructor(
    readonly campo: "senha" | "formulario",
    mensagem: string,
  ) {
    super(mensagem);
  }
}

export function LoginScreen() {
  const { colors } = useTheme();
  const client = useApiClient();
  const { iniciar } = useSessao();
  const router = useRouter();
  const [identificador, setIdentificador] = useState("");
  const [senha, setSenha] = useState("");
  const [vazios, setVazios] = useState({ identificador: false, senha: false });
  const [falha, setFalha] = useState<Falha | null>(null);
  // Só com a flag ligada a API responde, e só então o botão aparece.
  const demonstracao = useDemonstracao();

  const entrar = useMutation({
    mutationFn: async () => {
      const { data, error } = await client.POST("/api/v1/auth/login", {
        body: { identificador: identificador.trim(), senha },
      });
      if (!data) {
        const codigo = codigoDoErro(error);
        throw new Falha(
          codigo === "credenciais-invalidas" ? "senha" : "formulario",
          mensagemDeErro(codigo),
        );
      }
      // A sessão nova leva o grupo público a mandar para o início.
      await iniciar(data);
    },
    onError: (erro) =>
      setFalha(
        erro instanceof Falha
          ? erro
          : new Falha("formulario", mensagemDaFalha(erro)),
      ),
  });

  const enviar = () => {
    const faltando = {
      identificador: identificador.trim() === "",
      senha: senha === "",
    };
    setVazios(faltando);
    setFalha(null);
    if (!faltando.identificador && !faltando.senha) {
      entrar.mutate();
    }
  };

  return (
    <TelaDeAutenticacao segmentado={<SegmentadoDeAcesso atual="entrar" />}>
      <View style={styles.formulario}>
        <TextField
          label="RA ou e-mail"
          value={identificador}
          onChangeText={setIdentificador}
          autoCapitalize="none"
          autoCorrect={false}
          autoComplete="username"
          inputMode="email"
          returnKeyType="next"
          error={vazios.identificador ? "Informe o RA ou o e-mail." : undefined}
        />
        <View>
          <TextField
            label="Senha"
            password
            value={senha}
            onChangeText={setSenha}
            autoComplete="current-password"
            returnKeyType="go"
            onSubmitEditing={enviar}
            error={
              vazios.senha
                ? "Informe a senha."
                : falha?.campo === "senha"
                  ? falha.message
                  : undefined
            }
          />
          <View style={styles.esqueci}>
            <TextLink
              label="Esqueci minha senha"
              onPress={() => router.push("/esqueci-senha")}
            />
          </View>
        </View>

        <Button
          label={entrar.isPending ? "Entrando..." : "Entrar"}
          size="large"
          disabled={entrar.isPending}
          onPress={enviar}
        />
        {falha?.campo === "formulario" && (
          <AvisoDeErro mensagem={falha.message} />
        )}

        <Text style={[textStyles.corpo, styles.aviso, { color: colors.ink2 }]}>
          Primeiro acesso? Aluno cria a conta em Cadastrar. Professor define a
          senha em Esqueci minha senha.
        </Text>

        {demonstracao.data && (
          <Button
            label="Entrar na demonstração"
            variant="secondary"
            onPress={() => router.push("/demonstracao")}
          />
        )}
      </View>
    </TelaDeAutenticacao>
  );
}

const styles = StyleSheet.create({
  formulario: { gap: 16 },
  esqueci: { alignItems: "flex-end" },
  aviso: { textAlign: "center", marginTop: 8 },
});
