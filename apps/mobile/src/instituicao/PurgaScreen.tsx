import { useState } from "react";
import {
  ActivityIndicator,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { Button } from "../components/Button";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { TextField } from "../components/TextField";
import { fontFamily, textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, raios } from "../theme/tokens";
import { FolhaDeAto } from "./alunos/FolhaDeAto";
import { dataCurta } from "./alunos/textos";
import { useLotes, usePurgar, type LoteDaDemonstracao } from "./api";
import { TelaDaArea } from "./TelaDaArea";

// A purga dos dados de demonstração por lote (produto.md 11, passo 12): os lotes no banco,
// do mais recente ao mais antigo, com o vigente marcado, e a purga de um deles, que pede o
// nome do lote digitado de novo (P6). Sai tudo do lote numa transação, e as fotos dele vão
// para a remoção do bucket. Sem prancheta: segue a identidade visual.
export function PurgaScreen() {
  return (
    <TelaDaArea titulo="Purgar demonstração" voltarPara="/instituicao">
      <Lotes />
    </TelaDaArea>
  );
}

function Lotes() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const lotes = useLotes();
  const purgar = usePurgar();
  const [escolhido, setEscolhido] = useState<LoteDaDemonstracao | null>(null);
  const [confirmacao, setConfirmacao] = useState("");

  const fechar = () => {
    setEscolhido(null);
    setConfirmacao("");
  };

  return (
    <ScrollView
      contentContainerStyle={[
        styles.margem,
        styles.coluna,
        { paddingBottom: espacamento.respiroAntesDaNavegacao + insets.bottom },
      ]}
    >
      <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
        Cada aluno de demonstração entra com o lote configurado no momento do
        registro. Purgar tira do banco tudo do lote: contas, posts, prêmios,
        curtidas e comentários, e manda as fotos para fora do bucket. Não dá
        para desfazer.
      </Text>

      {purgar.isSuccess && (
        <View
          testID="purga-concluida"
          style={[
            styles.aviso,
            { backgroundColor: colors.surface, borderColor: colors.line },
          ]}
        >
          <Text style={[textStyles.corpo, { color: colors.ink }]}>
            {`Lote ${purgar.data.lote} purgado: ${purgar.data.alunos} ${purgar.data.alunos === 1 ? "aluno" : "alunos"} e ${purgar.data.fotos} ${purgar.data.fotos === 1 ? "foto" : "fotos"} a remover do bucket.`}
          </Text>
        </View>
      )}

      {lotes.isPending ? (
        <ActivityIndicator style={styles.espera} color={colors.ink2} />
      ) : lotes.isError ? (
        <ErrorState
          message={falhaParaTela(lotes.error).mensagem}
          onRetry={() => void lotes.refetch()}
        />
      ) : lotes.data.length === 0 ? (
        <EmptyState
          icon="inbox"
          title="Nenhum lote de demonstração no banco."
        />
      ) : (
        lotes.data.map((lote) => (
          <View
            key={lote.lote}
            testID={`lote-${lote.lote}`}
            style={[styles.lote, { borderColor: colors.line }]}
          >
            <View style={styles.titulo}>
              <Text
                style={[textStyles.secao, styles.nome, { color: colors.ink }]}
              >
                {lote.lote}
              </Text>
              {lote.vigente && (
                <View
                  style={[styles.marca, { backgroundColor: colors.highlight }]}
                >
                  <Text style={[styles.textoDaMarca, { color: colors.ink }]}>
                    Vigente
                  </Text>
                </View>
              )}
            </View>
            <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
              {`${lote.alunos} ${lote.alunos === 1 ? "aluno" : "alunos"} · de ${dataCurta(lote.primeiroEm)} a ${dataCurta(lote.ultimoEm)}`}
            </Text>
            {lote.vigente && (
              <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
                Novos registros da demonstração entram neste lote.
              </Text>
            )}
            <Button
              label="Purgar"
              variant="secondary"
              accessibilityLabel={`Purgar o lote ${lote.lote}`}
              onPress={() => {
                purgar.reset();
                setEscolhido(lote);
              }}
            />
          </View>
        ))
      )}

      <FolhaDeAto
        aberta={escolhido !== null}
        titulo="Purgar lote"
        explicacao={
          escolhido
            ? `Saem do banco os ${escolhido.alunos} ${escolhido.alunos === 1 ? "aluno" : "alunos"} do lote ${escolhido.lote} e tudo o que fizeram. Para confirmar, digite o nome do lote.`
            : ""
        }
        confirmar="Purgar lote"
        podeConfirmar={confirmacao.trim() === escolhido?.lote}
        enviando={purgar.isPending}
        erro={purgar.isError ? falhaParaTela(purgar.error).mensagem : null}
        onConfirmar={() =>
          escolhido &&
          purgar.mutate(
            { lote: escolhido.lote, confirmacao: confirmacao.trim() },
            { onSuccess: fechar },
          )
        }
        onFechar={fechar}
      >
        <TextField
          label="Nome do lote"
          value={confirmacao}
          onChangeText={setConfirmacao}
          placeholder={escolhido?.lote}
          autoCapitalize="none"
          autoCorrect={false}
        />
      </FolhaDeAto>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  margem: { paddingHorizontal: espacamento.margemLateral },
  coluna: { gap: 16 },
  espera: { marginTop: 32 },
  aviso: { borderWidth: 1, borderRadius: raios.filtros, padding: 12 },
  lote: { borderWidth: 1, borderRadius: 16, padding: 16, gap: 8 },
  titulo: { flexDirection: "row", alignItems: "center", gap: 8 },
  nome: { flexShrink: 1 },
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
});
