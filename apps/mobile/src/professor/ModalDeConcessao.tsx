import { useState } from "react";
import {
  ActivityIndicator,
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";

import { falhaParaTela } from "../api/FalhaDaApi";
import { AvisoDeErro } from "../autenticacao/AvisoDeErro";
import { Button } from "../components/Button";
import { Icon } from "../components/Icon";
import { ErrorState } from "../components/StateMessage";
import { Medalhao } from "../premios/Medalhao";
import { rotulosDeTier } from "../premios/rotulos";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import {
  espacamento,
  raios,
  tamanhos,
  tiers,
  type Tier,
} from "../theme/tokens";
import { useConcederMedalha, useMedalhasDoCatalogo } from "./api";
import { descreverLimite } from "./textos";

interface ModalDeConcessaoProps {
  turmaId: string;
  // O aluno que recebe; sem aluno, a folha está fechada.
  aluno: { id: string; nomeDeExibicao: string } | null;
  onFechar: () => void;
  onConcedida: (mensagem: string) => void;
}

// A concessão de medalha a partir do catálogo (produto.md 10.1, modal de medalha). Uma folha
// que sobe de baixo, como as do Instagram: o professor escolhe o item, e ninguém digita
// valor nem tier (produto.md 4.2).
export function ModalDeConcessao({
  turmaId,
  aluno,
  onFechar,
  onConcedida,
}: ModalDeConcessaoProps) {
  const { colors, name } = useTheme();
  const insets = useSafeAreaInsets();
  const medalhas = useMedalhasDoCatalogo();
  const conceder = useConcederMedalha(turmaId);
  const [escolhida, setEscolhida] = useState<string | null>(null);

  const fechar = () => {
    setEscolhida(null);
    conceder.reset();
    onFechar();
  };

  const confirmar = () => {
    if (!aluno || !escolhida) return;
    const item = medalhas.data?.find((medalha) => medalha.codigo === escolhida);
    conceder.mutate(
      { alunoId: aluno.id, item: escolhida },
      {
        onSuccess: () => {
          setEscolhida(null);
          conceder.reset();
          onConcedida(
            `Medalha ${item?.nome ?? escolhida} concedida a ${aluno.nomeDeExibicao}.`,
          );
        },
      },
    );
  };

  return (
    <Modal
      visible={aluno !== null}
      transparent
      animationType="slide"
      onRequestClose={fechar}
    >
      <View style={styles.fundo}>
        <Pressable
          accessibilityLabel="Fechar"
          style={[StyleSheet.absoluteFill, { backgroundColor: colors.scrim }]}
          onPress={fechar}
        />
        <View
          role="dialog"
          aria-modal
          accessibilityLabel="Conceder medalha"
          style={[
            styles.folha,
            {
              backgroundColor: colors.bg,
              paddingBottom:
                espacamento.respiroAntesDaNavegacao + insets.bottom,
            },
          ]}
        >
          <View style={styles.topo}>
            <View style={styles.titulos}>
              <Text
                role="heading"
                style={[textStyles.secao, { color: colors.ink }]}
              >
                Conceder medalha
              </Text>
              <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                Para {aluno?.nomeDeExibicao}
              </Text>
            </View>
            <Pressable
              role="button"
              accessibilityLabel="Fechar"
              hitSlop={8}
              style={styles.fechar}
              onPress={fechar}
            >
              <Icon name="fechar" color={colors.ink} size={24} />
            </Pressable>
          </View>

          {medalhas.isPending && (
            <ActivityIndicator style={styles.espera} color={colors.ink2} />
          )}
          {medalhas.isError && (
            <ErrorState
              message={falhaParaTela(medalhas.error).mensagem}
              onRetry={() => void medalhas.refetch()}
            />
          )}
          {medalhas.isSuccess && (
            <ScrollView style={styles.lista}>
              {medalhas.data.map((medalha) => {
                const tier = medalha.tier as Tier;
                const marcada = escolhida === medalha.codigo;
                return (
                  <Pressable
                    key={medalha.codigo}
                    role="radio"
                    aria-checked={marcada}
                    accessibilityLabel={`${medalha.nome}, ${rotulosDeTier[tier]}, ${medalha.pontos} pontos`}
                    style={({ pressed }) => [
                      styles.item,
                      {
                        borderColor: marcada ? colors.ink : colors.line,
                        backgroundColor: marcada ? colors.surface : colors.bg,
                      },
                      pressed && styles.pressionado,
                    ]}
                    onPress={() => {
                      conceder.reset();
                      setEscolhida(medalha.codigo);
                    }}
                  >
                    <Medalhao tier={tier} glifo="fita" size={40} />
                    <View style={styles.textoDoItem}>
                      <Text style={[textStyles.secao, { color: colors.ink }]}>
                        {medalha.nome}
                      </Text>
                      <Text style={textStyles.legenda}>
                        <Text style={{ color: tiers[tier].texto[name] }}>
                          {rotulosDeTier[tier]}
                        </Text>
                        <Text style={{ color: colors.ink2 }}>
                          {` · ${medalha.pontos} pts`}
                        </Text>
                      </Text>
                      <Text
                        style={[textStyles.legenda, { color: colors.ink2 }]}
                      >
                        {medalha.descricao}
                      </Text>
                      <Text
                        style={[textStyles.legenda, { color: colors.ink3 }]}
                      >
                        {descreverLimite(
                          medalha.limitePorPeriodo,
                          medalha.periodo,
                        )}
                      </Text>
                    </View>
                    {marcada && (
                      <Icon name="check" color={colors.ink} size={20} />
                    )}
                  </Pressable>
                );
              })}
            </ScrollView>
          )}

          {conceder.isError && (
            <AvisoDeErro mensagem={falhaParaTela(conceder.error).mensagem} />
          )}
          <Button
            label={conceder.isPending ? "Concedendo..." : "Conceder"}
            onPress={confirmar}
            disabled={!escolhida || conceder.isPending}
          />
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  fundo: { flex: 1, justifyContent: "flex-end" },
  folha: {
    maxHeight: "85%",
    borderTopLeftRadius: raios.cardMaior,
    borderTopRightRadius: raios.cardMaior,
    paddingTop: 16,
    paddingHorizontal: espacamento.margemLateral,
    gap: 12,
  },
  topo: { flexDirection: "row", alignItems: "flex-start", gap: 8 },
  titulos: { flex: 1, gap: 2 },
  fechar: {
    width: tamanhos.alvoDeToque,
    height: tamanhos.alvoDeToque,
    alignItems: "center",
    justifyContent: "center",
    marginTop: -10,
    marginRight: -10,
  },
  espera: { marginVertical: 32 },
  lista: { flexGrow: 0 },
  item: {
    flexDirection: "row",
    alignItems: "flex-start",
    gap: 12,
    padding: 12,
    borderWidth: 1,
    borderRadius: raios.cardMenor,
    marginBottom: 8,
  },
  textoDoItem: { flex: 1, gap: 2 },
  pressionado: { opacity: 0.85 },
});
