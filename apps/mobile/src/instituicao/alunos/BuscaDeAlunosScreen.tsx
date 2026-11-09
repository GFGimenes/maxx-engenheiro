import { useRouter } from "expo-router";
import { useEffect, useState } from "react";
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  StyleSheet,
  Text,
  View,
} from "react-native";

import { falhaParaTela } from "../../api/FalhaDaApi";
import { Avatar } from "../../components/Avatar";
import { Icon } from "../../components/Icon";
import { EmptyState, ErrorState } from "../../components/StateMessage";
import { TextField } from "../../components/TextField";
import { textStyles } from "../../theme/fonts";
import { useTheme } from "../../theme/ThemeProvider";
import { espacamento } from "../../theme/tokens";
import { TelaDaArea } from "../TelaDaArea";
import { useBuscaDeAlunos, type AlunoDaBase } from "./api";
import { descreverAluno } from "./textos";

// Espera a digitação parar antes de perguntar, como a disponibilidade do arroba.
const ESPERA_DA_DIGITACAO = 400;

// A busca da instituição na base de alunos (passo 12): por nome, arroba, e-mail ou RA, com
// conta ou sem, ex-alunos inclusive. Tocar abre a ficha. Sem prancheta: como a Busca do app.
export function BuscaDeAlunosScreen() {
  return (
    <TelaDaArea titulo="Alunos" voltarPara="/instituicao">
      <Busca />
    </TelaDaArea>
  );
}

function Busca() {
  const { colors } = useTheme();
  const router = useRouter();
  const [texto, setTexto] = useState("");
  const [termo, setTermo] = useState("");
  useEffect(() => {
    const espera = setTimeout(() => setTermo(texto), ESPERA_DA_DIGITACAO);
    return () => clearTimeout(espera);
  }, [texto]);
  const busca = useBuscaDeAlunos(termo);
  const curto = termo.trim().length < 2;

  const vazio = curto ? (
    <EmptyState
      icon="search"
      title="Busque um aluno."
      message="Digite ao menos 2 letras do nome, do arroba ou do e-mail, ou o RA inteiro."
    />
  ) : busca.isPending ? (
    <ActivityIndicator style={styles.espera} color={colors.ink2} />
  ) : busca.isError ? (
    <ErrorState
      message={falhaParaTela(busca.error).mensagem}
      onRetry={() => void busca.refetch()}
    />
  ) : (
    <EmptyState icon="search" title="Ninguém na base com essa busca." />
  );

  return (
    <View style={styles.fill}>
      <View style={styles.margem}>
        <TextField
          label="Buscar aluno"
          value={texto}
          onChangeText={setTexto}
          placeholder="Nome, arroba, e-mail ou RA"
          autoCapitalize="none"
          autoCorrect={false}
        />
      </View>
      <FlatList
        style={styles.fill}
        contentContainerStyle={styles.lista}
        data={curto ? [] : (busca.data ?? [])}
        keyExtractor={(aluno) => aluno.id}
        renderItem={({ item }) => (
          <Linha
            aluno={item}
            onPress={() => router.push(`/instituicao/alunos/${item.id}`)}
          />
        )}
        ListEmptyComponent={<View style={styles.estado}>{vazio}</View>}
      />
    </View>
  );
}

function Linha({
  aluno,
  onPress,
}: {
  aluno: AlunoDaBase;
  onPress: () => void;
}) {
  const { colors } = useTheme();
  return (
    <Pressable
      role="button"
      accessibilityLabel={aluno.nomeCompleto}
      onPress={onPress}
      style={({ pressed }) => [
        styles.linha,
        { borderBottomColor: colors.line },
        pressed && styles.pressionado,
      ]}
    >
      <Avatar
        nome={aluno.conta?.nomeDeExibicao ?? aluno.nomeCompleto}
        foto={aluno.conta?.foto}
        size={44}
      />
      <View style={styles.texto}>
        <Text style={[textStyles.secao, { color: colors.ink }]}>
          {aluno.nomeCompleto}
        </Text>
        <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
          {aluno.conta?.arroba ?? "Sem conta"}
        </Text>
        <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
          {descreverAluno(aluno)}
        </Text>
      </View>
      <Icon name="avancar" color={colors.ink3} size={20} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  margem: { paddingHorizontal: espacamento.margemLateral, paddingBottom: 8 },
  lista: { paddingHorizontal: espacamento.margemLateral },
  linha: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    minHeight: 72,
    borderBottomWidth: 1,
  },
  texto: { flex: 1, gap: 2 },
  pressionado: { opacity: 0.85 },
  espera: { marginTop: 32 },
  estado: { paddingTop: 24 },
});
