import { useMutation } from "@tanstack/react-query";
import { Redirect, useRouter } from "expo-router";
import { useEffect, useState } from "react";
import { ActivityIndicator, StyleSheet, Text, View } from "react-native";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi, falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { TelaDeAutenticacao } from "../autenticacao/TelaDeAutenticacao";
import { erroDoNome } from "../cadastro/regras";
import { Button } from "../components/Button";
import { FilterChip } from "../components/FilterChip";
import { ErrorState } from "../components/StateMessage";
import { TextField } from "../components/TextField";
import { TextLink } from "../components/TextLink";
import { useSessao } from "../sessao/SessaoProvider";
import { TextoDoTermo } from "../termo/TextoDoTermo";
import { useTermoDeUso } from "../termo/useTermoDeUso";
import { fontFamily, textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import type { ContaDeDemonstracao } from "./contas";
import * as dispositivo from "./contasDoDispositivo";
import { useDemonstracao } from "./useDemonstracao";

type CursoDeDemonstracao = { sigla: string; nome: string };

interface ContasDoDispositivo {
  lerContas: () => Promise<ContaDeDemonstracao[]>;
  gravarContas: (contas: ContaDeDemonstracao[]) => Promise<void>;
}

// A entrada da apresentação (produto.md 11): registro simples, com nome, curso e o aceite
// do termo, e a volta sem senha pela chave guardada neste aparelho. Sem a flag, a API
// responde 404, e esta tela devolve ao login.
export function DemonstracaoScreen({
  contasDoDispositivo = dispositivo,
}: {
  contasDoDispositivo?: ContasDoDispositivo;
}) {
  const demonstracao = useDemonstracao();

  if (demonstracao.isPending) {
    return <Carregando />;
  }
  if (!demonstracao.data) {
    return <Redirect href="/login" />;
  }
  return (
    <Entrada
      cursos={demonstracao.data}
      contasDoDispositivo={contasDoDispositivo}
    />
  );
}

function Carregando() {
  const { colors } = useTheme();
  return (
    <TelaDeAutenticacao>
      <ActivityIndicator color={colors.ink2} />
    </TelaDeAutenticacao>
  );
}

function Entrada({
  cursos,
  contasDoDispositivo,
}: {
  cursos: CursoDeDemonstracao[];
  contasDoDispositivo: ContasDoDispositivo;
}) {
  const { colors } = useTheme();
  const client = useApiClient();
  const router = useRouter();
  const { iniciar } = useSessao();
  const termo = useTermoDeUso();
  const [contas, setContas] = useState<ContaDeDemonstracao[]>([]);
  const [etapa, setEtapa] = useState<"escolha" | "termo">("escolha");
  const [nome, setNome] = useState("");
  const [curso, setCurso] = useState<string | null>(null);
  const [erros, setErros] = useState<{ nome?: string; curso?: string }>({});
  const [aviso, setAviso] = useState<string | null>(null);

  useEffect(() => {
    void contasDoDispositivo.lerContas().then(setContas);
  }, [contasDoDispositivo]);

  const guardar = async (novas: ContaDeDemonstracao[]) => {
    setContas(novas);
    await contasDoDispositivo.gravarContas(novas);
  };

  const voltar = useMutation({
    mutationFn: async (conta: ContaDeDemonstracao) => {
      const { data, error } = await client.POST("/api/v1/demo/entrar", {
        body: { chaveDoDispositivo: conta.chave },
      });
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      await iniciar(data);
    },
    onMutate: () => setAviso(null),
    onError: async (erro, conta) => {
      // A conta foi apagada (purga da apresentação): some da lista deste aparelho.
      if (erro instanceof FalhaDaApi && erro.codigo === "chave-invalida") {
        await guardar(contas.filter((c) => c.chave !== conta.chave));
      }
      setAviso(falhaParaTela(erro).mensagem);
    },
  });

  const registrar = useMutation({
    mutationFn: async (versaoDoTermo: number) => {
      const { data, error } = await client.POST("/api/v1/demo/alunos", {
        body: { nome: nome.trim(), curso: curso ?? "", versaoDoTermo },
      });
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      // Guardada antes de entrar: se a sessão cair, a pessoa ainda volta por aqui.
      await guardar([
        {
          chave: data.chaveDoDispositivo,
          nome: nome.trim(),
          curso: curso ?? "",
        },
        ...contas,
      ]);
      await iniciar(data.sessao);
    },
    onError: (erro) => {
      if (!(erro instanceof FalhaDaApi)) {
        return;
      }
      if (erro.codigo === "termo-desatualizado") {
        void termo.refetch();
      }
      // Curso que saiu do semestre vigente: volta para escolher outro.
      if (erro.codigo === "curso-indisponivel") {
        setCurso(null);
        setEtapa("escolha");
        setErros({ curso: erro.message });
      }
    },
  });
  const falhaDoRegistro = registrar.isError
    ? falhaParaTela(registrar.error)
    : null;

  const continuar = () => {
    const novos = {
      nome: erroDoNome(nome),
      curso: curso ? undefined : "Escolha um curso.",
    };
    setErros(novos);
    if (!novos.nome && !novos.curso) {
      registrar.reset();
      setEtapa("termo");
    }
  };

  if (etapa === "termo") {
    return (
      <TelaDeAutenticacao>
        <View style={styles.coluna}>
          <View style={styles.cabecalho}>
            <TextLink label="Voltar" onPress={() => setEtapa("escolha")} />
            <Text
              role="heading"
              style={[textStyles.titulo, { color: colors.ink }]}
            >
              Termo de uso
            </Text>
          </View>
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
                label={registrar.isPending ? "Entrando..." : "Aceitar e entrar"}
                size="large"
                disabled={registrar.isPending || termo.isFetching}
                onPress={() => registrar.mutate(termo.data.versao)}
              />
            </>
          )}
          {falhaDoRegistro && (
            <AvisoDeErro mensagem={falhaDoRegistro.mensagem} />
          )}
          {falhaDoRegistro?.codigo === "registro-simultaneo" &&
            termo.isSuccess && (
              <Button
                label="Tentar de novo"
                variant="secondary"
                onPress={() => registrar.mutate(termo.data.versao)}
              />
            )}
        </View>
      </TelaDeAutenticacao>
    );
  }

  return (
    <TelaDeAutenticacao>
      <View style={styles.coluna}>
        <View style={styles.cabecalho}>
          <Text
            role="heading"
            style={[textStyles.titulo, { color: colors.ink }]}
          >
            Demonstração
          </Text>
          <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
            Entre como aluno de teste, sem senha. As contas de teste são
            apagadas ao fim da avaliação do projeto.
          </Text>
        </View>

        {contas.length > 0 && (
          <View style={styles.secao}>
            <Text style={[textStyles.secao, { color: colors.ink }]}>
              Neste aparelho
            </Text>
            {contas.map((conta) => (
              <Button
                key={conta.chave}
                label={`Entrar como ${conta.nome} · ${conta.curso}`}
                variant="secondary"
                disabled={voltar.isPending}
                onPress={() => voltar.mutate(conta)}
              />
            ))}
          </View>
        )}
        {aviso && <AvisoDeErro mensagem={aviso} />}

        <View style={styles.secao}>
          {contas.length > 0 && (
            <Text style={[textStyles.secao, { color: colors.ink }]}>
              Criar outra conta de teste
            </Text>
          )}
          <TextField
            label="Nome"
            placeholder="Como você quer aparecer"
            value={nome}
            onChangeText={setNome}
            autoComplete="name"
            error={erros.nome}
          />
          <View style={styles.cursos}>
            <Text style={[styles.rotulo, { color: colors.ink }]}>Curso</Text>
            <View role="radiogroup" aria-label="Curso" style={styles.chips}>
              {cursos.map((opcao) => (
                <FilterChip
                  key={opcao.sigla}
                  role="radio"
                  label={opcao.nome}
                  selected={curso === opcao.sigla}
                  onPress={() => setCurso(opcao.sigla)}
                />
              ))}
            </View>
            {erros.curso && <AvisoDeErro mensagem={erros.curso} />}
          </View>
          <Button label="Continuar" size="large" onPress={continuar} />
        </View>

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
  coluna: { gap: 24 },
  cabecalho: { gap: 8 },
  secao: { gap: 12 },
  cursos: { gap: 8 },
  // O rótulo dos campos (TextField).
  rotulo: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 600 }),
    fontSize: 14,
    lineHeight: 20,
  },
  chips: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
  voltar: { alignItems: "center" },
});
