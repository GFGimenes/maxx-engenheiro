import { useMutation } from "@tanstack/react-query";
import { useRouter } from "expo-router";
import { useState } from "react";
import { StyleSheet, Text, View } from "react-native";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi, falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { TelaDeAutenticacao } from "../autenticacao/TelaDeAutenticacao";
import { Button } from "../components/Button";
import { TextField } from "../components/TextField";
import { TextLink } from "../components/TextLink";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";

// Pedir o link de senha nova, que também é o primeiro acesso de professor e instituição
// (fluxos/06-recuperacao-de-senha.md). A resposta é a mesma exista a conta ou não.
export function EsqueciSenhaScreen() {
  const { colors } = useTheme();
  const client = useApiClient();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [vazio, setVazio] = useState(false);

  const pedir = useMutation({
    mutationFn: async () => {
      const { error, response } = await client.POST(
        "/api/v1/auth/recuperacao",
        { body: { email: email.trim() } },
      );
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
  });
  const falha = pedir.isError ? falhaParaTela(pedir.error) : null;

  const enviar = () => {
    const semEmail = email.trim() === "";
    setVazio(semEmail);
    if (!semEmail) {
      pedir.mutate();
    }
  };

  return (
    <TelaDeAutenticacao>
      <View style={styles.coluna}>
        <View style={styles.cabecalho}>
          <Text
            role="heading"
            style={[textStyles.titulo, { color: colors.ink }]}
          >
            Esqueci minha senha
          </Text>
          <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
            Informe o seu e-mail institucional e mandamos um link para criar uma
            senha nova. No primeiro acesso de professor, é por aqui também.
          </Text>
        </View>

        {pedir.isSuccess ? (
          <View role="status" aria-live="polite" style={styles.enviado}>
            <Text style={[textStyles.secao, { color: colors.ink }]}>
              Confira o seu e-mail.
            </Text>
            <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
              Se esse e-mail tiver conta no Maxx, o link chega em instantes. Ele
              vale por 30 minutos.
            </Text>
          </View>
        ) : (
          <>
            <TextField
              label="E-mail institucional"
              placeholder="seu e-mail da faculdade"
              value={email}
              onChangeText={setEmail}
              autoCapitalize="none"
              autoCorrect={false}
              autoComplete="email"
              inputMode="email"
              onSubmitEditing={enviar}
              error={vazio ? "Informe o e-mail institucional." : undefined}
            />
            <Button
              label={pedir.isPending ? "Enviando..." : "Enviar link"}
              size="large"
              disabled={pedir.isPending}
              onPress={enviar}
            />
            {falha && <AvisoDeErro mensagem={falha.mensagem} />}
          </>
        )}

        <View style={styles.voltar}>
          <TextLink
            label="Voltar para o login"
            onPress={() => router.replace("/login")}
          />
        </View>
      </View>
    </TelaDeAutenticacao>
  );
}

const styles = StyleSheet.create({
  coluna: { gap: 16 },
  cabecalho: { gap: 8 },
  enviado: { gap: 4 },
  voltar: { alignItems: "center" },
});
