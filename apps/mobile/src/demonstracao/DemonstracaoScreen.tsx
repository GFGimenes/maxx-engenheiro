import { useMutation } from "@tanstack/react-query";
import { Redirect, useRouter } from "expo-router";
import { useEffect, useState } from "react";
import {
  ActivityIndicator,
  Platform,
  StyleSheet,
  Text,
  View,
} from "react-native";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi, falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { TelaDeAutenticacao } from "../autenticacao/TelaDeAutenticacao";
import { erroDoNome } from "../cadastro/regras";
import { Avatar } from "../components/Avatar";
import { Button } from "../components/Button";
import { FilterChip } from "../components/FilterChip";
import { ErrorState } from "../components/StateMessage";
import { TextField } from "../components/TextField";
import { TextLink } from "../components/TextLink";
import {
  FotoGrandeDemais,
  gerarDerivadas,
  type Derivadas,
} from "../fotos/derivadas";
import { enviarFoto, escolherFoto } from "../fotos/envio";
import { useSessao } from "../sessao/SessaoProvider";
import { TextoDoTermo } from "../termo/TextoDoTermo";
import { useTermoDeUso } from "../termo/useTermoDeUso";
import { fontFamily, textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import {
  rotuloDaConta,
  type ContaDeDemonstracao,
  type PapelDeDemonstracao,
} from "./contas";
import * as dispositivo from "./contasDoDispositivo";
import { useDemonstracao } from "./useDemonstracao";

type CursoDeDemonstracao = { sigla: string; nome: string };

interface ContasDoDispositivo {
  lerContas: () => Promise<ContaDeDemonstracao[]>;
  gravarContas: (contas: ContaDeDemonstracao[]) => Promise<void>;
}

// As três entradas (passo 14): o botão, a linha de apoio e o título da tela de registro.
const ENTRADAS: Record<
  PapelDeDemonstracao,
  { botao: string; apoio: string; titulo: string }
> = {
  aluno: {
    botao: "Entrar como aluno",
    apoio: "Posta, reivindica prêmios e aparece no ranking.",
    titulo: "Aluno de teste",
  },
  professor: {
    botao: "Entrar como professor",
    apoio: "Concede medalhas e decide comprovações das turmas de um curso.",
    titulo: "Professor de teste",
  },
  coordenacao: {
    botao: "Entrar como coordenação",
    apoio: "Cuida da faculdade inteira, pela área web.",
    titulo: "Coordenação de teste",
  },
};

// A entrada da apresentação (produto.md 11): três papéis, cada um com um registro simples e
// o aceite do termo, e a volta sem senha pela chave guardada neste aparelho. Sem a flag, a
// API responde 404, e esta tela devolve ao login.
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
  const [etapa, setEtapa] = useState<"escolha" | "registro" | "termo">(
    "escolha",
  );
  const [papel, setPapel] = useState<PapelDeDemonstracao>("aluno");
  const [nome, setNome] = useState("");
  const [curso, setCurso] = useState<string | null>(null);
  const [erros, setErros] = useState<{ nome?: string; curso?: string }>({});
  const [aviso, setAviso] = useState<string | null>(null);
  // A foto opcional do registro do aluno (produto.md 11): as derivadas saem na hora da
  // escolha e sobem como foto de perfil logo depois de a conta nascer.
  const [foto, setFoto] = useState<Derivadas | null>(null);
  const [preparandoFoto, setPreparandoFoto] = useState(false);
  const [erroDaFoto, setErroDaFoto] = useState<string | null>(null);
  const comCurso = papel !== "coordenacao";

  const trocarFoto = async () => {
    setErroDaFoto(null);
    try {
      const escolhida = await escolherFoto();
      if (!escolhida) return;
      setPreparandoFoto(true);
      setFoto(await gerarDerivadas(escolhida));
    } catch (erro) {
      setErroDaFoto(
        erro instanceof FotoGrandeDemais
          ? erro.message
          : "Não deu para abrir essa foto. Escolha outra.",
      );
    } finally {
      setPreparandoFoto(false);
    }
  };

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
      // A conta foi apagada ou encerrada (purga da apresentação): some da lista deste
      // aparelho.
      if (erro instanceof FalhaDaApi && erro.codigo === "chave-invalida") {
        await guardar(contas.filter((c) => c.chave !== conta.chave));
      }
      setAviso(falhaParaTela(erro).mensagem);
    },
  });

  // Cada papel tem a sua rota; a coordenação não escolhe curso.
  const pedirRegistro = async (versaoDoTermo: number) => {
    const corpo = { nome: nome.trim(), curso: curso ?? "", versaoDoTermo };
    switch (papel) {
      case "professor":
        return client.POST("/api/v1/demo/professores", { body: corpo });
      case "coordenacao":
        return client.POST("/api/v1/demo/coordenacoes", {
          body: { nome: corpo.nome, versaoDoTermo },
        });
      default:
        return client.POST("/api/v1/demo/alunos", { body: corpo });
    }
  };

  const registrar = useMutation({
    mutationFn: async (versaoDoTermo: number) => {
      const { data, error } = await pedirRegistro(versaoDoTermo);
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      // Guardada antes de entrar: se a sessão cair, a pessoa ainda volta por aqui.
      await guardar([
        {
          chave: data.chaveDoDispositivo,
          nome: nome.trim(),
          curso: comCurso ? (curso ?? "") : "",
          papel,
        },
        ...contas,
      ]);
      await iniciar(data.sessao);
      // Já com a sessão, a foto sobe como a de qualquer perfil. Melhor esforço: a conta já
      // existe, e a foto pode ser posta depois em Editar perfil.
      if (papel === "aluno" && foto) {
        try {
          const fotoUploadId = await enviarFoto(client, "fotoDePerfil", foto);
          await client.PATCH("/api/v1/eu/perfil", {
            body: {
              nomeDeExibicao: null,
              prefixo: null,
              fotoUploadId,
              removerFoto: null,
            },
          });
        } catch {
          // Sem foto, por ora.
        }
      }
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
        setEtapa("registro");
        setErros({ curso: erro.message });
      }
    },
  });
  const falhaDoRegistro = registrar.isError
    ? falhaParaTela(registrar.error)
    : null;

  const abrir = (escolhido: PapelDeDemonstracao) => {
    setPapel(escolhido);
    setErros({});
    setAviso(null);
    setEtapa("registro");
  };

  const continuar = () => {
    const novos = {
      nome: erroDoNome(nome),
      curso: !comCurso || curso ? undefined : "Escolha um curso.",
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
            <TextLink label="Voltar" onPress={() => setEtapa("registro")} />
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

  if (etapa === "registro") {
    return (
      <TelaDeAutenticacao>
        <View style={styles.coluna}>
          <View style={styles.cabecalho}>
            <TextLink label="Voltar" onPress={() => setEtapa("escolha")} />
            <Text
              role="heading"
              style={[textStyles.titulo, { color: colors.ink }]}
            >
              {ENTRADAS[papel].titulo}
            </Text>
            {papel === "coordenacao" && (
              <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
                A coordenação tem todos os poderes da instituição sobre a base
                de teste, inclusive apagar as contas de teste.
              </Text>
            )}
          </View>

          <View style={styles.secao}>
            <TextField
              label="Nome"
              placeholder="Como você quer aparecer"
              value={nome}
              onChangeText={setNome}
              autoComplete="name"
              error={erros.nome}
            />
            {comCurso && (
              <View style={styles.cursos}>
                <Text style={[styles.rotulo, { color: colors.ink }]}>
                  Curso
                </Text>
                {papel === "professor" && (
                  <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                    As turmas deste curso no semestre ficam com você.
                  </Text>
                )}
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
            )}
            {papel === "aluno" && (
              <View style={styles.cursos}>
                <Text style={[styles.rotulo, { color: colors.ink }]}>
                  Foto (opcional)
                </Text>
                <View style={styles.foto}>
                  <Avatar
                    nome={nome.trim() || "Aluno"}
                    foto={foto?.grade.uri}
                    size={56}
                  />
                  <View style={styles.acoesDaFoto}>
                    <Button
                      label={foto ? "Trocar foto" : "Escolher foto"}
                      variant="secondary"
                      disabled={preparandoFoto}
                      onPress={() => void trocarFoto()}
                    />
                    {foto && (
                      <Button
                        label="Remover foto"
                        variant="secondary"
                        onPress={() => setFoto(null)}
                      />
                    )}
                  </View>
                </View>
                {erroDaFoto && <AvisoDeErro mensagem={erroDaFoto} />}
              </View>
            )}
            <Button label="Continuar" size="large" onPress={continuar} />
          </View>
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
            Escolha como entrar, sem senha. As contas de teste são apagadas ao
            fim da avaliação do projeto.
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
                label={rotuloDaConta(conta)}
                variant="secondary"
                disabled={voltar.isPending}
                onPress={() => voltar.mutate(conta)}
              />
            ))}
          </View>
        )}
        {aviso && <AvisoDeErro mensagem={aviso} />}

        <View style={styles.secao}>
          {(Object.keys(ENTRADAS) as PapelDeDemonstracao[]).map((opcao) => (
            <View key={opcao} style={styles.entrada}>
              <Button
                label={ENTRADAS[opcao].botao}
                variant="secondary"
                size="large"
                onPress={() => abrir(opcao)}
              />
              <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                {ENTRADAS[opcao].apoio + navegador(opcao)}
              </Text>
            </View>
          ))}
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

// A área da instituição só abre no navegador (produto.md 10.2): no aparelho, a coordenação
// já sabe disso antes de entrar.
function navegador(papel: PapelDeDemonstracao): string {
  return papel === "coordenacao" && Platform.OS !== "web"
    ? " Abre no navegador."
    : "";
}

const styles = StyleSheet.create({
  coluna: { gap: 24 },
  cabecalho: { gap: 8 },
  secao: { gap: 12 },
  entrada: { gap: 4 },
  cursos: { gap: 8 },
  // O rótulo dos campos (TextField).
  rotulo: {
    fontFamily: fontFamily({ familia: "Instrument Sans", peso: 600 }),
    fontSize: 14,
    lineHeight: 20,
  },
  chips: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
  foto: { flexDirection: "row", alignItems: "center", gap: 12 },
  acoesDaFoto: { flex: 1, gap: 8 },
  voltar: { alignItems: "center" },
});
