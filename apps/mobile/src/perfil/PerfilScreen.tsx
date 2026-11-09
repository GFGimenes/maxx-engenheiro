import { useRouter } from "expo-router";
import { useState } from "react";
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { useEu } from "../api/useEu";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { Avatar } from "../components/Avatar";
import { Button } from "../components/Button";
import { Cabecalho } from "../components/Cabecalho";
import { Icon, type IconName } from "../components/Icon";
import { EmptyState, ErrorState } from "../components/StateMessage";
import type { Post } from "../feed/api";
import { formatarNumero } from "../feed/textos";
import { useLarguraDoApp } from "../components/PhoneFrame";
import { Logo } from "../marca/Logo";
import { ModalDeConcessao } from "../professor/ModalDeConcessao";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { espacamento, interacao, raios, tamanhos } from "../theme/tokens";
import { LinhaDePremio } from "../trofeus/LinhaDePremio";
import {
  usePerfil,
  usePostsDoPerfil,
  usePremiosDoPerfil,
  useSeguir,
  useTurmasEmComum,
  type Perfil,
} from "./api";
import { CelulaDaGrade } from "./CelulaDaGrade";
import { MenuDoPerfil } from "./MenuDoPerfil";

type Aba = "posts" | "trofeus" | "medalhas";

const ABAS: { aba: Aba; icone: IconName; rotulo: string }[] = [
  { aba: "posts", icone: "grade", rotulo: "Posts" },
  { aba: "trofeus", icone: "taca", rotulo: "Troféus" },
  { aba: "medalhas", icone: "fita", rotulo: "Medalhas" },
];

// Espaço entre as células da grade, como na prancheta.
const VAO = 2;

// O perfil (produto.md 6.5 e prancheta do perfil): o próprio, na aba Perfil, ou o de outra
// pessoa, por /usuarios/:id. Do aluno: avatar (com o anel dourado no próprio), pontos,
// seguidores e seguindo, nome, curso e semestre, e as abas por ícone com a grade de três
// colunas, os troféus e as medalhas, com a posição no histórico geral (passo 11). Do
// professor: as turmas do semestre e as medalhas concedidas (produto.md 2.1).
export function PerfilScreen({ usuarioId }: { usuarioId?: string }) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const eu = useEu();
  const perfil = usePerfil(usuarioId);
  const [aba, setAba] = useState<Aba>("posts");
  const [menuAberto, setMenuAberto] = useState(false);

  const dados = perfil.data;
  const proprio = !usuarioId || usuarioId === eu.data?.id;
  const ehAluno = dados?.papel === "aluno";
  const posts = usePostsDoPerfil(ehAluno ? dados?.id : undefined);
  const premios = usePremiosDoPerfil(dados?.id, ehAluno && aba !== "posts");

  const lado = Math.floor((useLarguraDoApp() - 2 * VAO) / 3);
  const listaDePosts =
    posts.data?.pages.flatMap((pagina) => pagina.itens) ?? [];
  const itensDePremio = (premios.data?.itens ?? []).filter((item) =>
    aba === "medalhas" ? item.tipo === "medalha" : item.tipo !== "medalha",
  );

  const topo = proprio ? (
    <View style={[styles.margem, styles.topo]}>
      <Logo largura={110} />
      <Pressable
        role="button"
        accessibilityLabel="Menu"
        hitSlop={8}
        onPress={() => setMenuAberto(true)}
        style={styles.botaoDeIcone}
      >
        <Icon name="menu" color={colors.ink} size={tamanhos.iconeNav} />
      </Pressable>
    </View>
  ) : (
    <View style={styles.margem}>
      <Cabecalho titulo={dados?.arroba ?? "Perfil"} voltarPara="/" />
    </View>
  );

  // Em todos os estados da tela: o "≡" aparece antes de o perfil chegar.
  const menu = (
    <MenuDoPerfil
      aberto={menuAberto}
      professor={eu.data?.papeis.includes("professor") ?? false}
      onFechar={() => setMenuAberto(false)}
    />
  );

  if (!dados) {
    return (
      <View
        style={[
          styles.fill,
          { paddingTop: insets.top, backgroundColor: colors.bg },
        ]}
      >
        {topo}
        <View style={styles.margem}>
          {perfil.isPending ? (
            <ActivityIndicator style={styles.espera} color={colors.ink2} />
          ) : falhaParaTela(perfil.error).codigo ===
            "usuario-nao-encontrado" ? (
            <EmptyState icon="user" title="Esse perfil não existe." />
          ) : (
            <ErrorState
              message={falhaParaTela(perfil.error).mensagem}
              onRetry={() => void perfil.refetch()}
            />
          )}
        </View>
        {menu}
      </View>
    );
  }

  const cabecalho = (
    <View>
      {topo}
      <DadosDoPerfil perfil={dados} proprio={proprio} />
      {ehAluno && (
        <View
          style={[styles.abas, { borderBottomColor: colors.line }]}
          role="tablist"
        >
          {ABAS.map(({ aba: valor, icone, rotulo }) => {
            const ativa = aba === valor;
            return (
              <Pressable
                key={valor}
                role="tab"
                aria-selected={ativa}
                accessibilityLabel={rotulo}
                onPress={() => setAba(valor)}
                style={[styles.aba, ativa && { borderBottomColor: colors.ink }]}
              >
                <Icon
                  name={icone}
                  color={ativa ? colors.ink : colors.ink2}
                  size={24}
                />
              </Pressable>
            );
          })}
        </View>
      )}
    </View>
  );

  const rodape = (
    <View style={{ height: espacamento.respiroAntesDaNavegacao }} />
  );

  if (!ehAluno) {
    return (
      <View style={[styles.fill, { backgroundColor: colors.bg }]}>
        <FlatList
          style={{ backgroundColor: colors.bg }}
          contentContainerStyle={{ paddingTop: insets.top }}
          data={dados.professor?.turmas ?? []}
          keyExtractor={(turma) =>
            `${turma.materia}-${turma.curso}-${turma.semestre}`
          }
          ListHeaderComponent={
            <View>
              {cabecalho}
              <Text
                role="heading"
                style={[
                  textStyles.secao,
                  styles.margem,
                  styles.tituloDaSecao,
                  { color: colors.ink },
                ]}
              >
                Turmas neste semestre
              </Text>
            </View>
          }
          renderItem={({ item: turma }) => (
            <View
              style={[
                styles.margem,
                styles.turma,
                { borderBottomColor: colors.line },
              ]}
            >
              <Text style={[textStyles.secao, { color: colors.ink }]}>
                {turma.materia}
              </Text>
              <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                {`${turma.curso} · ${turma.semestre}`}
              </Text>
            </View>
          )}
          ListEmptyComponent={
            <Text
              style={[textStyles.corpo, styles.margem, { color: colors.ink2 }]}
            >
              Nenhuma turma no semestre vigente.
            </Text>
          }
          ListFooterComponent={rodape}
        />
        {menu}
      </View>
    );
  }

  return (
    <View testID="perfil" style={[styles.fill, { backgroundColor: colors.bg }]}>
      {aba === "posts" ? (
        <FlatList
          key="posts"
          testID="grade"
          data={listaDePosts}
          keyExtractor={(post) => post.id}
          numColumns={3}
          columnWrapperStyle={{ gap: VAO }}
          contentContainerStyle={{ paddingTop: insets.top, gap: VAO }}
          renderItem={({ item }: { item: Post }) => (
            <CelulaDaGrade post={item} lado={lado} />
          )}
          ListHeaderComponent={cabecalho}
          ListEmptyComponent={
            posts.isPending ? (
              <ActivityIndicator style={styles.espera} color={colors.ink2} />
            ) : posts.isError ? (
              <View style={styles.margem}>
                <ErrorState
                  message={falhaParaTela(posts.error).mensagem}
                  onRetry={() => void posts.refetch()}
                />
              </View>
            ) : (
              <View style={styles.margem}>
                <EmptyState
                  icon="imagem"
                  title={
                    proprio ? "Você ainda não publicou." : "Nenhum post ainda."
                  }
                />
              </View>
            )
          }
          ListFooterComponent={
            <View>
              {posts.isFetchingNextPage && (
                <ActivityIndicator color={colors.ink2} />
              )}
              {rodape}
            </View>
          }
          onEndReachedThreshold={0.5}
          onEndReached={() => {
            if (posts.hasNextPage && !posts.isFetchingNextPage) {
              void posts.fetchNextPage();
            }
          }}
        />
      ) : (
        <FlatList
          key={aba}
          data={itensDePremio}
          keyExtractor={(item) => item.codigo}
          contentContainerStyle={{ paddingTop: insets.top }}
          renderItem={({ item }) => (
            <View style={styles.margem}>
              <LinhaDePremio item={item} />
            </View>
          )}
          ListHeaderComponent={cabecalho}
          ListEmptyComponent={
            premios.isPending ? (
              <ActivityIndicator style={styles.espera} color={colors.ink2} />
            ) : premios.isError ? (
              <View style={styles.margem}>
                <ErrorState
                  message={falhaParaTela(premios.error).mensagem}
                  onRetry={() => void premios.refetch()}
                />
              </View>
            ) : (
              <View style={styles.margem}>
                <EmptyState
                  icon={aba === "medalhas" ? "fita" : "taca"}
                  title={
                    aba === "medalhas"
                      ? "Nenhuma medalha ainda."
                      : "Nenhum troféu ainda."
                  }
                />
              </View>
            )
          }
          ListFooterComponent={rodape}
        />
      )}
      {menu}
    </View>
  );
}

// O cabeçalho do perfil: avatar, números, nome, linha de curso, botões.
function DadosDoPerfil({
  perfil,
  proprio,
}: {
  perfil: Perfil;
  proprio: boolean;
}) {
  const { colors } = useTheme();
  const router = useRouter();
  const eu = useEu();
  const seguir = useSeguir(perfil.id);
  const ehAluno = perfil.papel === "aluno";
  const papeis = eu.data?.papeis ?? [];
  const leitorProfessor = papeis.includes("professor");
  const turmas = useTurmasEmComum(
    perfil.id,
    !proprio && ehAluno && leitorProfessor,
  );
  const [concederEm, setConcederEm] = useState<string | null>(null);
  const [escolhendoTurma, setEscolhendoTurma] = useState(false);
  const [aviso, setAviso] = useState<string | null>(null);

  // Quem segue (produto.md 6.3): aluno segue aluno; professor, só os alunos dele. Quem já
  // segue sempre pode deixar de seguir.
  const podeSeguir =
    !proprio &&
    ehAluno &&
    (perfil.euSigo ||
      papeis.includes("aluno") ||
      (leitorProfessor && turmas.length > 0));

  const descricao = [
    perfil.curso,
    perfil.semestre ? `${perfil.semestre}º semestre` : null,
    perfil.exAluno ? "Ex-aluno" : null,
    perfil.ra ? `RA ${perfil.ra}` : null,
    perfil.posicao ? `#${formatarNumero(perfil.posicao)}` : null,
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <View style={[styles.margem, styles.cabecalho]}>
      <View style={styles.linhaDoTopo}>
        <Avatar
          nome={perfil.nomeDeExibicao}
          foto={perfil.foto?.grade}
          size={84}
          anel={proprio && ehAluno}
        />
        {ehAluno && (
          <Pontos
            pontos={perfil.pontos ?? 0}
            posicao={perfil.posicao}
            onPress={
              proprio
                ? () => router.push("/rankings?visao=historico")
                : undefined
            }
          />
        )}
        {ehAluno && <Contagem numero={perfil.seguidores} rotulo="seguidores" />}
        <Contagem numero={perfil.seguindo} rotulo="seguindo" />
      </View>

      <View style={styles.quem}>
        <Text style={[textStyles.titulo, { color: colors.ink }]}>
          {perfil.nomeDeExibicao}
        </Text>
        <Text
          testID="arroba"
          style={[textStyles.legenda, { color: colors.ink2 }]}
        >
          {perfil.arroba}
        </Text>
        {descricao !== "" && (
          <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
            {descricao}
          </Text>
        )}
        {perfil.professor && (
          <Text style={[textStyles.corpo, { color: colors.ink2 }]}>
            {`Professor · ${formatarNumero(perfil.professor.medalhasConcedidas)} ${
              perfil.professor.medalhasConcedidas === 1
                ? "medalha concedida"
                : "medalhas concedidas"
            }`}
          </Text>
        )}
      </View>

      <View style={styles.botoes}>
        {proprio && (
          <View style={styles.botao}>
            <Button
              label="Editar perfil"
              variant="secondary"
              onPress={() => router.push("/editar-perfil")}
            />
          </View>
        )}
        {podeSeguir && (
          <View style={styles.botao}>
            <Button
              label={
                seguir.isPending
                  ? "Aguarde..."
                  : perfil.euSigo
                    ? "Seguindo"
                    : "Seguir"
              }
              variant={perfil.euSigo ? "secondary" : "primary"}
              disabled={seguir.isPending}
              accessibilityLabel={
                perfil.euSigo
                  ? `Deixar de seguir ${perfil.nomeDeExibicao}`
                  : `Seguir ${perfil.nomeDeExibicao}`
              }
              onPress={() => {
                setAviso(null);
                seguir.mutate(!perfil.euSigo, {
                  onError: (erro) => setAviso(falhaParaTela(erro).mensagem),
                });
              }}
            />
          </View>
        )}
        {turmas.length > 0 && (
          <View style={styles.botao}>
            <Button
              label="Conceder medalha"
              variant="secondary"
              onPress={() =>
                turmas.length === 1
                  ? setConcederEm(turmas[0]!.id)
                  : setEscolhendoTurma(true)
              }
            />
          </View>
        )}
      </View>

      {escolhendoTurma &&
        turmas.map((turma) => (
          <Button
            key={turma.id}
            label={`Em ${turma.materia}`}
            variant="secondary"
            onPress={() => {
              setEscolhendoTurma(false);
              setConcederEm(turma.id);
            }}
          />
        ))}
      {aviso && <AvisoDeErro mensagem={aviso} />}

      {concederEm && (
        <ModalDeConcessao
          turmaId={concederEm}
          aluno={{ id: perfil.id, nomeDeExibicao: perfil.nomeDeExibicao }}
          onFechar={() => setConcederEm(null)}
          onConcedida={(mensagem) => {
            setConcederEm(null);
            setAviso(mensagem);
          }}
        />
      )}
    </View>
  );
}

// Os pontos acumulados e a posição no histórico geral, coerente com eles (pergunta 4 do
// passo 11): "15.020 / pts · #29 ›". No próprio perfil, tocar abre o ranking nesse chip
// (produto.md 6.5); no de outra pessoa, só mostra. Sem posição ainda, só os pontos.
function Pontos({
  pontos,
  posicao,
  onPress,
}: {
  pontos: number;
  posicao: number | null;
  onPress?: () => void;
}) {
  const { colors } = useTheme();
  const legenda = posicao ? `pts · #${formatarNumero(posicao)}` : "pts";
  const conteudo = (
    <>
      <Text style={[textStyles.titulo, { color: colors.ink }]}>
        {formatarNumero(pontos)}
      </Text>
      <View style={styles.legendaDosPontos}>
        <Text
          style={[textStyles.legenda, styles.forte, { color: colors.goldText }]}
        >
          {legenda}
        </Text>
        {onPress && <Icon name="avancar" color={colors.goldText} size={14} />}
      </View>
    </>
  );

  return onPress ? (
    <Pressable
      testID="pontos-do-perfil"
      role="link"
      accessibilityLabel={
        posicao
          ? `${pontos} pontos, posição ${posicao}. Abrir rankings`
          : `${pontos} pontos. Abrir rankings`
      }
      onPress={onPress}
      style={({ pressed }) => [
        styles.pontos,
        { backgroundColor: colors.surface },
        pressed && { opacity: interacao.opacidadePressionado },
      ]}
    >
      {conteudo}
    </Pressable>
  ) : (
    <View
      testID="pontos-do-perfil"
      style={[styles.pontos, { backgroundColor: colors.surface }]}
    >
      {conteudo}
    </View>
  );
}

function Contagem({ numero, rotulo }: { numero: number; rotulo: string }) {
  const { colors } = useTheme();
  return (
    <View
      style={styles.contagem}
      accessible
      accessibilityLabel={`${numero} ${rotulo}`}
    >
      <Text style={[textStyles.titulo, { color: colors.ink }]}>
        {formatarNumero(numero)}
      </Text>
      <Text style={[textStyles.legenda, { color: colors.ink2 }]}>{rotulo}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  margem: { paddingHorizontal: espacamento.margemLateral },
  topo: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingVertical: 8,
  },
  botaoDeIcone: {
    width: tamanhos.alvoDeToque,
    height: tamanhos.alvoDeToque,
    alignItems: "center",
    justifyContent: "center",
    marginRight: -10,
  },
  espera: { marginTop: 32 },
  cabecalho: { gap: 16, paddingTop: 8, paddingBottom: 16 },
  linhaDoTopo: { flexDirection: "row", alignItems: "center", gap: 12 },
  pontos: {
    borderRadius: raios.cardMenor,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  forte: { fontWeight: "700" },
  legendaDosPontos: { flexDirection: "row", alignItems: "center", gap: 2 },
  contagem: { flex: 1, alignItems: "center" },
  quem: { gap: 2 },
  botoes: { flexDirection: "row", gap: 8 },
  botao: { flex: 1 },
  abas: { flexDirection: "row", borderBottomWidth: 1 },
  aba: {
    flex: 1,
    alignItems: "center",
    paddingVertical: 12,
    borderBottomWidth: 2,
    borderBottomColor: "transparent",
  },
  tituloDaSecao: { paddingTop: 8, paddingBottom: 4 },
  turma: { paddingVertical: 12, borderBottomWidth: 1, gap: 2 },
});
