import { useLocalSearchParams, useRouter } from "expo-router";
import { useState } from "react";
import {
  ActivityIndicator,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../../api/FalhaDaApi";
import { useCursos } from "../../busca/api";
import { Avatar } from "../../components/Avatar";
import { Button } from "../../components/Button";
import { FilterChip } from "../../components/FilterChip";
import { EmptyState, ErrorState } from "../../components/StateMessage";
import { TextField } from "../../components/TextField";
import { Medalhao } from "../../premios/Medalhao";
import { rotulosDeTier } from "../../premios/rotulos";
import { textStyles } from "../../theme/fonts";
import { useTheme } from "../../theme/ThemeProvider";
import { espacamento, tiers, type Tier } from "../../theme/tokens";
import { TelaDaArea } from "../TelaDaArea";
import {
  useAnularMedalha,
  useCorrigirEmail,
  useFicha,
  useMarcarExAluno,
  useTrocarDeCurso,
  type FichaDoAluno,
  type MedalhaDaFicha,
} from "./api";
import { FolhaDeAto } from "./FolhaDeAto";
import { dataCurta, situacao } from "./textos";

type Ato = "email" | "curso" | "exAluno" | null;

// A ficha do aluno na área da instituição (passo 12): a linha da base (nome, RA, e-mail,
// curso e semestre), a conta quando há, e os atos: corrigir o e-mail, trocar de curso,
// conceder prêmio de participação, marcar ex-aluno e anular medalha. Cada ato confirma numa
// folha, com a recusa da API dentro dela. Sem prancheta: segue a identidade visual.
export function FichaDoAlunoScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return (
    <TelaDaArea titulo="Ficha do aluno" voltarPara="/instituicao/alunos">
      <Ficha cadastroId={id} />
    </TelaDaArea>
  );
}

function Ficha({ cadastroId }: { cadastroId: string }) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const ficha = useFicha(cadastroId);
  const [ato, setAto] = useState<Ato>(null);
  const [anulando, setAnulando] = useState<MedalhaDaFicha | null>(null);

  if (ficha.isPending) {
    return <ActivityIndicator style={styles.espera} color={colors.ink2} />;
  }

  if (ficha.isError) {
    return (
      <View style={styles.margem}>
        <ErrorState
          message={falhaParaTela(ficha.error).mensagem}
          onRetry={() => void ficha.refetch()}
        />
      </View>
    );
  }

  const { aluno, medalhas } = ficha.data;
  const conta = aluno.conta;
  const exAluno = !!conta?.exAlunoDesde;

  return (
    <ScrollView
      contentContainerStyle={[
        styles.margem,
        styles.coluna,
        { paddingBottom: espacamento.respiroAntesDaNavegacao + insets.bottom },
      ]}
    >
      <View style={styles.cabecalho}>
        <Avatar
          nome={conta?.nomeDeExibicao ?? aluno.nomeCompleto}
          foto={conta?.foto}
          size={64}
        />
        <View style={styles.texto}>
          <Text
            role="heading"
            style={[textStyles.secao, { color: colors.ink }]}
          >
            {aluno.nomeCompleto}
          </Text>
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            {conta?.arroba ?? "Ainda não criou a conta"}
          </Text>
        </View>
      </View>

      <View style={[styles.dados, { borderColor: colors.line }]}>
        <Dado rotulo="E-mail" valor={aluno.email} />
        <Dado rotulo="RA" valor={aluno.ra} />
        <Dado
          rotulo="Curso"
          valor={`${aluno.curso} · ${aluno.semestre}º semestre`}
        />
        <Dado
          rotulo="Situação"
          valor={
            exAluno
              ? `Ex-aluno desde ${dataCurta(conta!.exAlunoDesde!)}`
              : situacao(aluno)
          }
        />
      </View>

      <View style={styles.acoes}>
        <Button
          label="Corrigir e-mail"
          variant="secondary"
          onPress={() => setAto("email")}
        />
        {!exAluno && (
          <Button
            label="Trocar de curso"
            variant="secondary"
            onPress={() => setAto("curso")}
          />
        )}
        {conta && !exAluno && (
          <Button
            label="Conceder prêmio"
            variant="secondary"
            onPress={() =>
              router.push(`/instituicao/alunos/${cadastroId}/conceder`)
            }
          />
        )}
        {conta && !exAluno && (
          <Button
            label="Marcar ex-aluno"
            variant="secondary"
            onPress={() => setAto("exAluno")}
          />
        )}
      </View>

      <View style={styles.secao}>
        <Text role="heading" style={[textStyles.secao, { color: colors.ink }]}>
          Medalhas
        </Text>
        {medalhas.length === 0 ? (
          <EmptyState icon="fita" title="Nenhuma medalha ativa." />
        ) : (
          medalhas.map((medalha) => (
            <LinhaDeMedalha
              key={medalha.id}
              medalha={medalha}
              onAnular={() => setAnulando(medalha)}
            />
          ))
        )}
      </View>

      <FolhaDoEmail
        aberta={ato === "email"}
        cadastroId={cadastroId}
        atual={aluno.email}
        onFechar={() => setAto(null)}
      />
      <FolhaDoCurso
        aberta={ato === "curso"}
        cadastroId={cadastroId}
        aluno={aluno}
        onFechar={() => setAto(null)}
      />
      <FolhaDoExAluno
        aberta={ato === "exAluno"}
        cadastroId={cadastroId}
        nome={aluno.nomeCompleto}
        onFechar={() => setAto(null)}
      />
      <FolhaDaAnulacao
        medalha={anulando}
        cadastroId={cadastroId}
        nome={aluno.nomeCompleto}
        onFechar={() => setAnulando(null)}
      />
    </ScrollView>
  );
}

function Dado({ rotulo, valor }: { rotulo: string; valor: string }) {
  const { colors } = useTheme();
  return (
    <View style={styles.dado}>
      <Text style={[textStyles.legenda, { color: colors.ink2 }]}>{rotulo}</Text>
      <Text style={[textStyles.corpo, { color: colors.ink }]}>{valor}</Text>
    </View>
  );
}

function LinhaDeMedalha({
  medalha,
  onAnular,
}: {
  medalha: MedalhaDaFicha;
  onAnular: () => void;
}) {
  const { colors, name } = useTheme();
  const tier = medalha.tier as Tier;
  return (
    <View
      testID={`medalha-${medalha.id}`}
      style={[styles.medalha, { borderBottomColor: colors.line }]}
    >
      <Medalhao tier={tier} glifo="fita" size={40} />
      <View style={styles.texto}>
        <Text style={[textStyles.secao, { color: colors.ink }]}>
          {medalha.nomeDoItem}
        </Text>
        <Text style={textStyles.legenda}>
          <Text style={{ color: tiers[tier].texto[name] }}>
            {rotulosDeTier[tier]}
          </Text>
          <Text style={{ color: colors.ink2 }}>{` · +${medalha.pontos}`}</Text>
        </Text>
        <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
          {`${medalha.materia} · ${medalha.bimestre}º bimestre de ${medalha.ano} · ${medalha.concedidaPor}`}
        </Text>
      </View>
      <Button
        label="Anular"
        variant="secondary"
        accessibilityLabel={`Anular ${medalha.nomeDoItem} de ${dataCurta(medalha.concedidaEm)}`}
        onPress={onAnular}
      />
    </View>
  );
}

function FolhaDoEmail({
  aberta,
  cadastroId,
  atual,
  onFechar,
}: {
  aberta: boolean;
  cadastroId: string;
  atual: string;
  onFechar: () => void;
}) {
  const corrigir = useCorrigirEmail(cadastroId);
  const [email, setEmail] = useState(atual);
  const fechar = () => {
    corrigir.reset();
    setEmail(atual);
    onFechar();
  };

  return (
    <FolhaDeAto
      aberta={aberta}
      titulo="Corrigir e-mail"
      explicacao="O e-mail da base é o que vale para o cadastro, o login e a recuperação de senha. Os links de recuperação já enviados deixam de valer."
      confirmar="Salvar e-mail"
      podeConfirmar={email.trim() !== "" && email.trim() !== atual}
      enviando={corrigir.isPending}
      erro={corrigir.isError ? falhaParaTela(corrigir.error).mensagem : null}
      onConfirmar={() => corrigir.mutate(email.trim(), { onSuccess: fechar })}
      onFechar={fechar}
    >
      <TextField
        label="E-mail"
        value={email}
        onChangeText={setEmail}
        keyboardType="email-address"
        autoCapitalize="none"
        autoCorrect={false}
      />
    </FolhaDeAto>
  );
}

function FolhaDoCurso({
  aberta,
  cadastroId,
  aluno,
  onFechar,
}: {
  aberta: boolean;
  cadastroId: string;
  aluno: FichaDoAluno["aluno"];
  onFechar: () => void;
}) {
  const { colors } = useTheme();
  const cursos = useCursos();
  const trocar = useTrocarDeCurso(cadastroId);
  const [cursoId, setCursoId] = useState<string | null>(null);
  const [semestre, setSemestre] = useState<number | null>(null);
  const curso = cursos.data?.find((candidato) => candidato.id === cursoId);
  const fechar = () => {
    trocar.reset();
    setCursoId(null);
    setSemestre(null);
    onFechar();
  };

  return (
    <FolhaDeAto
      aberta={aberta}
      titulo="Trocar de curso"
      explicacao="O aluno leva os pontos para o ranking do curso novo, e o sufixo do arroba muda. As matrículas antigas ficam."
      confirmar="Trocar de curso"
      podeConfirmar={!!curso && semestre !== null}
      enviando={trocar.isPending}
      erro={trocar.isError ? falhaParaTela(trocar.error).mensagem : null}
      onConfirmar={() =>
        trocar.mutate(
          { cursoId: cursoId!, semestre: semestre! },
          { onSuccess: fechar },
        )
      }
      onFechar={fechar}
    >
      <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
        Curso novo
      </Text>
      <View style={styles.chips}>
        {(cursos.data ?? [])
          .filter((candidato) => candidato.id !== aluno.cursoId)
          .map((candidato) => (
            <FilterChip
              key={candidato.id}
              label={candidato.sigla}
              role="radio"
              selected={cursoId === candidato.id}
              onPress={() => {
                setCursoId(candidato.id);
                setSemestre(null);
              }}
            />
          ))}
      </View>
      {curso && (
        <>
          <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
            {`Semestre em ${curso.nome}`}
          </Text>
          <View style={styles.chips}>
            {Array.from(
              { length: curso.duracaoEmSemestres },
              (_, i) => i + 1,
            ).map((numero) => (
              <FilterChip
                key={numero}
                label={`${numero}º`}
                role="radio"
                selected={semestre === numero}
                onPress={() => setSemestre(numero)}
              />
            ))}
          </View>
        </>
      )}
    </FolhaDeAto>
  );
}

function FolhaDoExAluno({
  aberta,
  cadastroId,
  nome,
  onFechar,
}: {
  aberta: boolean;
  cadastroId: string;
  nome: string;
  onFechar: () => void;
}) {
  const marcar = useMarcarExAluno(cadastroId);
  const fechar = () => {
    marcar.reset();
    onFechar();
  };

  return (
    <FolhaDeAto
      aberta={aberta}
      titulo="Marcar ex-aluno"
      explicacao={`${nome} sai do ranking do semestre agora e fica só no histórico. Continua entrando, lendo o mural e comentando, mas não posta nem recebe prêmio. O arroba passa a terminar em -EX. Não dá para desfazer.`}
      confirmar="Marcar ex-aluno"
      enviando={marcar.isPending}
      erro={marcar.isError ? falhaParaTela(marcar.error).mensagem : null}
      onConfirmar={() => marcar.mutate(undefined, { onSuccess: fechar })}
      onFechar={fechar}
    />
  );
}

function FolhaDaAnulacao({
  medalha,
  cadastroId,
  nome,
  onFechar,
}: {
  medalha: MedalhaDaFicha | null;
  cadastroId: string;
  nome: string;
  onFechar: () => void;
}) {
  const anular = useAnularMedalha(cadastroId);
  const fechar = () => {
    anular.reset();
    onFechar();
  };

  return (
    <FolhaDeAto
      aberta={medalha !== null}
      titulo="Anular medalha"
      explicacao={
        medalha
          ? `${medalha.nomeDoItem} de ${nome}, em ${medalha.materia}, sai da pontuação, e as conquistas que dependiam dela são recalculadas. Sem motivo público. Use para corrigir erro de concessão.`
          : ""
      }
      confirmar="Anular"
      enviando={anular.isPending}
      erro={anular.isError ? falhaParaTela(anular.error).mensagem : null}
      onConfirmar={() =>
        medalha && anular.mutate(medalha.id, { onSuccess: fechar })
      }
      onFechar={fechar}
    />
  );
}

const styles = StyleSheet.create({
  margem: { paddingHorizontal: espacamento.margemLateral },
  coluna: { gap: 20 },
  espera: { marginTop: 32 },
  cabecalho: { flexDirection: "row", alignItems: "center", gap: 16 },
  texto: { flex: 1, gap: 2 },
  dados: { borderWidth: 1, borderRadius: 16, padding: 16, gap: 12 },
  dado: { gap: 2 },
  acoes: { gap: 8 },
  secao: { gap: 8 },
  medalha: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    paddingVertical: 10,
    borderBottomWidth: 1,
  },
  chips: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
});
