import { useRouter } from "expo-router";
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
import { useEu } from "../api/useEu";
import { Button } from "../components/Button";
import { EmptyState, ErrorState } from "../components/StateMessage";
import { TextField } from "../components/TextField";
import { semAConta, type ContaDeDemonstracao } from "../demonstracao/contas";
import * as dispositivo from "../demonstracao/contasDoDispositivo";
import { useSessao } from "../sessao/SessaoProvider";
import { fontFamily, textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, raios } from "../theme/tokens";
import { FolhaDeAto } from "./alunos/FolhaDeAto";
import { dataCurta } from "./alunos/textos";
import { useLotes, usePurgar, type LoteDaDemonstracao } from "./api";
import { TelaDaArea } from "./TelaDaArea";

interface ContasDoDispositivo {
  lerContas: () => Promise<ContaDeDemonstracao[]>;
  gravarContas: (contas: ContaDeDemonstracao[]) => Promise<void>;
}

// A purga dos dados de demonstração por lote (produto.md 11, passo 12): os lotes no banco,
// do mais recente ao mais antigo, com o vigente marcado, e a purga de um deles, que pede o
// nome do lote digitado de novo (P6). Desde o passo 14, os alunos do lote são apagados e os
// professores e as coordenações, encerrados; a coordenação de demonstração pode purgar o
// próprio lote, e a tela avisa antes e leva ao login depois (P2). Sem prancheta: segue a
// identidade visual.
export function PurgaScreen({
  contasDoDispositivo = dispositivo,
}: {
  contasDoDispositivo?: ContasDoDispositivo;
}) {
  return (
    <TelaDaArea titulo="Purgar demonstração" voltarPara="/instituicao">
      <Lotes contasDoDispositivo={contasDoDispositivo} />
    </TelaDaArea>
  );
}

function Lotes({
  contasDoDispositivo,
}: {
  contasDoDispositivo: ContasDoDispositivo;
}) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { sair } = useSessao();
  const eu = useEu();
  const lotes = useLotes();
  const purgar = usePurgar();
  const [escolhido, setEscolhido] = useState<LoteDaDemonstracao | null>(null);
  const [confirmacao, setConfirmacao] = useState("");
  const [saindo, setSaindo] = useState(false);

  const fechar = () => {
    setEscolhido(null);
    setConfirmacao("");
  };

  // A conta de quem purgou foi encerrada junto: some da lembrança do aparelho, a sessão
  // acaba e a pessoa volta ao login.
  const voltarAoLogin = async () => {
    setSaindo(true);
    try {
      if (eu.data) {
        await contasDoDispositivo.gravarContas(
          semAConta(await contasDoDispositivo.lerContas(), eu.data.id),
        );
      }
    } finally {
      await sair();
      router.replace("/login");
    }
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
        Cada conta de demonstração entra com o lote configurado no momento do
        registro. Purgar apaga os alunos do lote, com posts, prêmios, curtidas e
        comentários, e manda as fotos para fora do bucket. Professores e
        coordenações do lote são encerrados: o que fizeram fica, com o nome
        deles. Não dá para desfazer.
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
            {resultado(purgar.data)}
          </Text>
          {purgar.data.contaPropriaEncerrada && (
            <Button
              label={saindo ? "Saindo..." : "Voltar ao login"}
              disabled={saindo}
              onPress={() => void voltarAoLogin()}
            />
          )}
        </View>
      )}

      {purgar.data?.contaPropriaEncerrada ? null : lotes.isPending ? (
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
              {lote.vigente && <Marca texto="Vigente" />}
              {lote.deQuemPede && <Marca texto="Seu lote" />}
            </View>
            <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
              {`${contas(lote)} · de ${dataCurta(lote.primeiroEm)} a ${dataCurta(lote.ultimoEm)}`}
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
        explicacao={escolhido ? explicacao(escolhido) : ""}
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

function Marca({ texto }: { texto: string }) {
  const { colors } = useTheme();
  return (
    <View style={[styles.marca, { backgroundColor: colors.highlight }]}>
      <Text style={[styles.textoDaMarca, { color: colors.ink }]}>{texto}</Text>
    </View>
  );
}

type Purgado = {
  lote: string;
  alunos: number;
  fotos: number;
  professores: number;
  coordenacoes: number;
  contaPropriaEncerrada: boolean;
};

function contar(n: number, singular: string, plural: string): string {
  return `${n} ${n === 1 ? singular : plural}`;
}

// "a", "a e b", "a, b e c".
function emLista(partes: string[]): string {
  return partes.length < 2
    ? (partes[0] ?? "")
    : `${partes.slice(0, -1).join(", ")} e ${partes.at(-1)}`;
}

function equipe(professores: number, coordenacoes: number): string[] {
  return [
    ...(professores > 0
      ? [contar(professores, "professor", "professores")]
      : []),
    ...(coordenacoes > 0
      ? [contar(coordenacoes, "coordenação", "coordenações")]
      : []),
  ];
}

// Concordância com a equipe: "encerrado", "encerrada", "encerrados", "encerradas".
function encerrados(professores: number, coordenacoes: number): string {
  if (professores + coordenacoes === 1) {
    return professores === 1 ? "encerrado" : "encerrada";
  }
  return professores > 0 ? "encerrados" : "encerradas";
}

// As contas ativas do lote, por papel. Os alunos aparecem sempre; a equipe, quando há.
function contas(lote: LoteDaDemonstracao): string {
  return emLista([
    contar(lote.alunos, "aluno", "alunos"),
    ...equipe(lote.professores, lote.coordenacoes),
  ]);
}

function explicacao(lote: LoteDaDemonstracao): string {
  const daEquipe = equipe(lote.professores, lote.coordenacoes);
  return [
    lote.alunos > 0
      ? lote.alunos === 1
        ? `Sai do banco o aluno do lote ${lote.lote} e tudo o que fez.`
        : `Saem do banco os ${lote.alunos} alunos do lote ${lote.lote} e tudo o que fizeram.`
      : null,
    daEquipe.length > 0
      ? `${emLista(daEquipe)} do lote ${lote.lote} ${lote.professores + lote.coordenacoes === 1 ? "é" : "são"} ${encerrados(lote.professores, lote.coordenacoes)}; o que fizeram fica.`
      : null,
    lote.deQuemPede ? "Sua conta é deste lote e será encerrada junto." : null,
    "Para confirmar, digite o nome do lote.",
  ]
    .filter(Boolean)
    .join(" ");
}

function resultado(purga: Purgado): string {
  const daEquipe = equipe(purga.professores, purga.coordenacoes);
  return [
    `Lote ${purga.lote} purgado: ${contar(purga.alunos, "aluno", "alunos")} e ${contar(purga.fotos, "foto", "fotos")} a remover do bucket.`,
    daEquipe.length > 0
      ? `${emLista(daEquipe)} ${encerrados(purga.professores, purga.coordenacoes)}.`
      : null,
    purga.contaPropriaEncerrada
      ? "Sua conta era deste lote e foi encerrada."
      : null,
  ]
    .filter(Boolean)
    .join(" ");
}

const styles = StyleSheet.create({
  margem: { paddingHorizontal: espacamento.margemLateral },
  coluna: { gap: 16 },
  espera: { marginTop: 32 },
  aviso: { borderWidth: 1, borderRadius: raios.filtros, padding: 12, gap: 12 },
  lote: { borderWidth: 1, borderRadius: 16, padding: 16, gap: 8 },
  titulo: {
    flexDirection: "row",
    alignItems: "center",
    flexWrap: "wrap",
    gap: 8,
  },
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
