import { useMutation } from "@tanstack/react-query";
import { useRouter } from "expo-router";
import { useState } from "react";
import { StyleSheet, Text, View } from "react-native";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi, falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { TelaDeAutenticacao } from "../autenticacao/TelaDeAutenticacao";
import { erroDaSenha } from "../cadastro/regras";
import { Button } from "../components/Button";
import { TextField } from "../components/TextField";
import { useSessao } from "../sessao/SessaoProvider";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";

// A senha nova, a partir do link do e-mail. O token chega do fragmento da URL, que a rota
// lê e apaga; sem ele (link incompleto, ou o app nativo, que não recebe o link), a tela
// oferece pedir outro.
export function RedefinirSenhaScreen({ token }: { token: string | null }) {
  const { colors } = useTheme();
  const client = useApiClient();
  const router = useRouter();
  const { estado, sair } = useSessao();
  const [senha, setSenha] = useState("");
  const [erroSenha, setErroSenha] = useState<string>();

  const salvar = useMutation({
    mutationFn: async () => {
      const { error, response } = await client.POST(
        "/api/v1/auth/recuperacao/confirmar",
        { body: { token: token ?? "", senha } },
      );
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
      // A senha nova derrubou todas as sessões da conta. Se havia uma aberta aqui, ela
      // acaba também, e a pessoa entra de novo com a senha nova.
      if (estado === "autenticado") {
        await sair();
      }
    },
    onError: (erro) => {
      // A política é conferida antes do token: o link continua valendo.
      if (
        erro instanceof FalhaDaApi &&
        erro.codigo === "senha-fora-da-politica"
      ) {
        setErroSenha(erro.message);
      }
    },
  });
  const falha = salvar.isError ? falhaParaTela(salvar.error) : null;
  const linkInvalido = !token || falha?.codigo === "recuperacao-invalida";

  const enviar = () => {
    const erro = erroDaSenha(senha);
    setErroSenha(erro);
    if (!erro) {
      salvar.mutate();
    }
  };

  const pedirOutro = () => router.replace("/esqueci-senha");

  return (
    <TelaDeAutenticacao>
      <View style={styles.coluna}>
        <Text role="heading" style={[textStyles.titulo, { color: colors.ink }]}>
          Senha nova
        </Text>

        {salvar.isSuccess ? (
          <>
            <View role="status" aria-live="polite" style={styles.texto}>
              <Text style={[textStyles.secao, { color: colors.ink }]}>
                Senha nova salva.
              </Text>
              <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
                Entre com ela. As sessões abertas em outros aparelhos foram
                encerradas.
              </Text>
            </View>
            <Button
              label="Entrar"
              size="large"
              onPress={() => router.replace("/login")}
            />
          </>
        ) : linkInvalido ? (
          <>
            <View role="alert" style={styles.texto}>
              <Text style={[textStyles.secao, { color: colors.ink }]}>
                {token
                  ? "Esse link não vale mais."
                  : "Esse link está incompleto."}
              </Text>
              <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
                O link vale por 30 minutos e uma vez só. Abra o link mais
                recente do e-mail, ou peça um novo.
              </Text>
            </View>
            <Button label="Pedir link novo" size="large" onPress={pedirOutro} />
          </>
        ) : (
          <>
            <TextField
              label="Senha nova"
              placeholder="Mínimo de 8 caracteres"
              password
              value={senha}
              onChangeText={setSenha}
              autoComplete="new-password"
              onSubmitEditing={enviar}
              error={erroSenha}
            />
            <Button
              label={salvar.isPending ? "Salvando..." : "Salvar senha"}
              size="large"
              disabled={salvar.isPending}
              onPress={enviar}
            />
            {falha && falha.codigo !== "senha-fora-da-politica" && (
              <AvisoDeErro mensagem={falha.mensagem} />
            )}
          </>
        )}
      </View>
    </TelaDeAutenticacao>
  );
}

const styles = StyleSheet.create({
  coluna: { gap: 16 },
  texto: { gap: 4 },
});
