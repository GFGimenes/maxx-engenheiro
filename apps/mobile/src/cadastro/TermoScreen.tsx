import { useMutation } from "@tanstack/react-query";
import { Redirect, useRouter } from "expo-router";
import { ActivityIndicator, StyleSheet, View } from "react-native";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi, falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { TelaDeAutenticacao } from "../autenticacao/TelaDeAutenticacao";
import { Button } from "../components/Button";
import { ErrorState } from "../components/StateMessage";
import { useSessao } from "../sessao/SessaoProvider";
import { TextoDoTermo } from "../termo/TextoDoTermo";
import { useTermoDeUso } from "../termo/useTermoDeUso";
import { useTheme } from "../theme/ThemeProvider";
import { useCadastro, type DadosDoCadastro } from "./CadastroContext";
import { PassoDoCadastro } from "./PassoDoCadastro";

// Passo 3 de 3: o termo de uso vigente e o aceite. A conta nasce aqui, com a versão aceita
// (produto.md 2.6).
export function TermoScreen() {
  const { dados } = useCadastro();
  if (!dados) {
    return <Redirect href="/cadastro" />;
  }
  return <Aceite dados={dados} />;
}

function Aceite({ dados }: { dados: DadosDoCadastro }) {
  const { colors } = useTheme();
  const client = useApiClient();
  const router = useRouter();
  const { iniciar } = useSessao();
  const termo = useTermoDeUso();

  const criar = useMutation({
    mutationFn: async (versaoDoTermo: number) => {
      const { data, error } = await client.POST("/api/v1/auth/cadastro", {
        body: {
          ra: dados.ra,
          email: dados.email,
          nomeDeExibicao: dados.nomeDeExibicao,
          prefixo: dados.prefixo,
          senha: dados.senha,
          versaoDoTermo,
        },
      });
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      // A sessão nova leva o grupo público a mandar para o início.
      await iniciar(data);
    },
    onError: (erro) => {
      // O termo mudou enquanto a pessoa lia: mostra o novo, e o aceite é pedido de novo.
      if (erro instanceof FalhaDaApi && erro.codigo === "termo-desatualizado") {
        void termo.refetch();
      }
    },
  });
  const falha = criar.isError ? falhaParaTela(criar.error) : null;

  return (
    <TelaDeAutenticacao>
      <View style={styles.coluna}>
        <PassoDoCadastro
          numero={3}
          titulo="Termo de uso"
          onVoltar={() => router.back()}
        />

        {termo.isPending && <ActivityIndicator color={colors.ink2} />}
        {termo.isError && (
          <ErrorState
            title="Não deu para carregar o termo agora."
            onRetry={() => void termo.refetch()}
          />
        )}
        {termo.isSuccess && (
          <>
            <TextoDoTermo texto={termo.data.texto} />
            <Button
              label={
                criar.isPending ? "Criando a conta..." : "Aceitar e criar conta"
              }
              size="large"
              disabled={criar.isPending || termo.isFetching}
              onPress={() => criar.mutate(termo.data.versao)}
            />
          </>
        )}
        {falha && <AvisoDeErro mensagem={falha.mensagem} />}
        {falha?.codigo === "prefixo-indisponivel" && (
          <Button
            label="Escolher outro arroba"
            variant="secondary"
            onPress={() => router.back()}
          />
        )}
      </View>
    </TelaDeAutenticacao>
  );
}

const styles = StyleSheet.create({
  coluna: { gap: 16 },
});
