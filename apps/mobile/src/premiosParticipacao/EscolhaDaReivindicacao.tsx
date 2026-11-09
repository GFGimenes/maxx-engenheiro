import type { UseQueryResult } from "@tanstack/react-query";
import type { ReactNode } from "react";
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  View,
} from "react-native";

import { falhaParaTela } from "../api/FalhaDaApi";
import { Icon } from "../components/Icon";
import { ErrorState } from "../components/StateMessage";
import { TextField } from "../components/TextField";
import { TextLink } from "../components/TextLink";
import { Medalhao } from "../premios/Medalhao";
import { rotulosDeTier } from "../premios/rotulos";
import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { raios, tiers, type Tier } from "../theme/tokens";
import type { ItemDoAluno } from "../trofeus/montagem";
import {
  useEventos,
  useMateriasDoAluno,
  type EventoEscolhido,
  type EventoSemeado,
  type MateriaDoAluno,
} from "./api";
import { lerData, periodo } from "./textos";

// O que o aluno escolheu até agora. eventoId "outro": o evento é digitado.
export interface Escolha {
  item: string | null;
  eventoId: string | null;
  nome: string;
  inicio: string;
  fim: string;
  materiaId: string | null;
}

export const ESCOLHA_VAZIA: Escolha = {
  item: null,
  eventoId: null,
  nome: "",
  inicio: "",
  fim: "",
  materiaId: null,
};

export const OUTRO_EVENTO = "outro";

// O evento pronto para a API, ou nulo enquanto falta algo. O fim vazio é o próprio início:
// evento de um dia só. Nome e datas inválidos a API recusa com mensagem própria; aqui só se
// exige o que dá para conferir sem ela.
export function eventoDaEscolha(
  escolha: Escolha,
  exigeMateria: boolean,
): EventoEscolhido | null {
  if (!exigeMateria && escolha.eventoId && escolha.eventoId !== OUTRO_EVENTO) {
    return { tipo: "semeado", id: escolha.eventoId };
  }

  if (!exigeMateria && escolha.eventoId !== OUTRO_EVENTO) return null;
  const inicio = lerData(escolha.inicio);
  const fim = escolha.fim.trim() === "" ? inicio : lerData(escolha.fim);
  if (escolha.nome.trim() === "" || !inicio || !fim) return null;
  return { tipo: "digitado", nome: escolha.nome.trim(), inicio, fim };
}

// O que a escolha mostra de cada prêmio: o catálogo do aluno, ou o que a instituição pode
// conceder a ele (passo 12).
export type ItemEscolhivel = Pick<
  ItemDoAluno,
  "codigo" | "nome" | "tier" | "pontos" | "criterio" | "exigeMateria"
>;

// De onde vêm os eventos e as matérias: do próprio aluno, na reivindicação, ou do aluno da
// ficha, na concessão direta da instituição.
export interface FontesDaEscolha {
  useEventosDoItem: (item: string | null) => UseQueryResult<EventoSemeado[]>;
  useMaterias: (ativo: boolean) => UseQueryResult<MateriaDoAluno[]>;
}

const DO_ALUNO: FontesDaEscolha = {
  useEventosDoItem: useEventos,
  useMaterias: useMateriasDoAluno,
};

interface EscolhaDaReivindicacaoProps {
  itens: ItemEscolhivel[];
  valor: Escolha;
  onChange: (escolha: Escolha) => void;
  fontes?: FontesDaEscolha;
  // Quem escolhe: o aluno fala de si; a instituição, do aluno.
  quem?: "aluno" | "instituicao";
}

// Reivindicação de prêmio de participação (produto.md 4.6 e 4.7): o prêmio, o evento, da
// lista ou digitado com nome e período, e a matéria quando o prêmio pede (apresentação em
// sala). Sem prancheta: segue a identidade visual, em linhas como as do modal de medalha.
// Escolhido o prêmio, a lista recolhe para ele, com "Trocar prêmio": o catálogo tem 17 itens
// e empurraria o evento para longe da foto.
export function EscolhaDaReivindicacao({
  itens,
  valor,
  onChange,
  fontes = DO_ALUNO,
  quem = "aluno",
}: EscolhaDaReivindicacaoProps) {
  const { colors } = useTheme();
  const { useEventosDoItem, useMaterias } = fontes;
  const daInstituicao = quem === "instituicao";
  const item = itens.find((candidato) => candidato.codigo === valor.item);
  const exigeMateria = item?.exigeMateria ?? false;
  const eventos = useEventosDoItem(item && !exigeMateria ? item.codigo : null);
  const materias = useMaterias(exigeMateria);
  const digitado = exigeMateria || valor.eventoId === OUTRO_EVENTO;
  const mudar = (parcial: Partial<Escolha>) =>
    onChange({ ...valor, ...parcial });

  return (
    <View style={styles.coluna}>
      <View style={styles.secao}>
        <Titulo texto="Prêmio" />
        <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
          {daInstituicao
            ? "O prêmio que a instituição concede, já aprovado."
            : "O prêmio que a foto comprova."}
        </Text>
        {(item ? [item] : itens).map((opcao) => (
          <Linha
            key={opcao.codigo}
            marcada={valor.item === opcao.codigo}
            rotulo={`${opcao.nome}, ${rotulosDeTier[opcao.tier as Tier]}, ${opcao.pontos} pontos`}
            onPress={() =>
              onChange({
                ...ESCOLHA_VAZIA,
                item: opcao.codigo,
                nome: valor.nome,
                inicio: valor.inicio,
                fim: valor.fim,
              })
            }
          >
            <Medalhao tier={opcao.tier as Tier} glifo="taca" size={40} />
            <View style={styles.texto}>
              <Text style={[textStyles.secao, { color: colors.ink }]}>
                {opcao.nome}
              </Text>
              <Pontos tier={opcao.tier as Tier} pontos={opcao.pontos} />
              <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                {opcao.criterio}
              </Text>
            </View>
          </Linha>
        ))}
        {item && (
          <View style={styles.trocar}>
            <TextLink
              label="Trocar prêmio"
              onPress={() =>
                onChange({
                  ...valor,
                  item: null,
                  eventoId: null,
                  materiaId: null,
                })
              }
            />
          </View>
        )}
      </View>

      {item && (
        <View style={styles.secao}>
          <Titulo texto="Evento" />
          {exigeMateria ? (
            <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
              {daInstituicao
                ? "Diga o que o aluno apresentou e quando."
                : "Diga o que você apresentou e quando."}
            </Text>
          ) : (
            <>
              {eventos.isPending && <ActivityIndicator color={colors.ink2} />}
              {eventos.isError && (
                <ErrorState
                  message={falhaParaTela(eventos.error).mensagem}
                  onRetry={() => void eventos.refetch()}
                />
              )}
              {(eventos.data ?? []).map((evento) => (
                <Linha
                  key={evento.id}
                  marcada={valor.eventoId === evento.id}
                  rotulo={`${evento.nome}, ${periodo(evento.inicio, evento.fim)}`}
                  onPress={() => mudar({ eventoId: evento.id })}
                >
                  <View style={styles.texto}>
                    <Text style={[textStyles.secao, { color: colors.ink }]}>
                      {evento.nome}
                    </Text>
                    <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                      {periodo(evento.inicio, evento.fim)}
                    </Text>
                  </View>
                </Linha>
              ))}
              <Linha
                marcada={valor.eventoId === OUTRO_EVENTO}
                rotulo="Outro evento"
                onPress={() => mudar({ eventoId: OUTRO_EVENTO })}
              >
                <View style={styles.texto}>
                  <Text style={[textStyles.secao, { color: colors.ink }]}>
                    Outro evento
                  </Text>
                  <Text style={[textStyles.legenda, { color: colors.ink2 }]}>
                    Não está na lista? Digite o nome e as datas.
                  </Text>
                </View>
              </Linha>
            </>
          )}

          {digitado && (
            <View style={styles.campos}>
              <TextField
                label={
                  exigeMateria
                    ? daInstituicao
                      ? "O que o aluno apresentou"
                      : "O que você apresentou"
                    : "Nome do evento"
                }
                value={valor.nome}
                onChangeText={(nome) => mudar({ nome })}
                placeholder={
                  exigeMateria
                    ? "Seminário de normalização"
                    : "Palestra sobre LGPD"
                }
              />
              <TextField
                label="Data de início"
                value={valor.inicio}
                onChangeText={(inicio) => mudar({ inicio })}
                placeholder="dd/mm/aaaa"
                keyboardType="numbers-and-punctuation"
                error={
                  valor.inicio.trim() !== "" && !lerData(valor.inicio)
                    ? "Use o formato dd/mm/aaaa."
                    : undefined
                }
              />
              <TextField
                label="Data de fim, se durou mais de um dia"
                value={valor.fim}
                onChangeText={(fim) => mudar({ fim })}
                placeholder="dd/mm/aaaa"
                keyboardType="numbers-and-punctuation"
                error={
                  valor.fim.trim() !== "" && !lerData(valor.fim)
                    ? "Use o formato dd/mm/aaaa."
                    : undefined
                }
              />
            </View>
          )}
        </View>
      )}

      {exigeMateria && (
        <View style={styles.secao}>
          <Titulo texto="Matéria" />
          {materias.isPending && <ActivityIndicator color={colors.ink2} />}
          {materias.isError && (
            <ErrorState
              message={falhaParaTela(materias.error).mensagem}
              onRetry={() => void materias.refetch()}
            />
          )}
          {materias.isSuccess && materias.data.length === 0 && (
            <Text style={[textStyles.legenda, { color: colors.ink3 }]}>
              {daInstituicao
                ? "O aluno não tem matéria neste semestre."
                : "Você não tem matéria neste semestre."}
            </Text>
          )}
          {(materias.data ?? []).map((materia) => (
            <Linha
              key={materia.id}
              marcada={valor.materiaId === materia.id}
              rotulo={materia.nome}
              onPress={() => mudar({ materiaId: materia.id })}
            >
              <Text
                style={[textStyles.secao, styles.texto, { color: colors.ink }]}
              >
                {materia.nome}
              </Text>
            </Linha>
          ))}
        </View>
      )}
    </View>
  );
}

function Titulo({ texto }: { texto: string }) {
  const { colors } = useTheme();
  return (
    <Text role="heading" style={[textStyles.secao, { color: colors.ink }]}>
      {texto}
    </Text>
  );
}

function Pontos({ tier, pontos }: { tier: Tier; pontos: number }) {
  const { colors, name } = useTheme();
  return (
    <Text style={textStyles.legenda}>
      <Text style={{ color: tiers[tier].texto[name] }}>
        {rotulosDeTier[tier]}
      </Text>
      <Text style={{ color: colors.ink2 }}>{` · +${pontos}`}</Text>
    </Text>
  );
}

// Uma opção de escolha única, como as do modal de medalha: borda em ink quando marcada.
function Linha({
  marcada,
  rotulo,
  onPress,
  children,
}: {
  marcada: boolean;
  rotulo: string;
  onPress: () => void;
  children: ReactNode;
}) {
  const { colors } = useTheme();
  return (
    <Pressable
      role="radio"
      aria-checked={marcada}
      accessibilityLabel={rotulo}
      onPress={onPress}
      style={({ pressed }) => [
        styles.linha,
        {
          borderColor: marcada ? colors.ink : colors.line,
          backgroundColor: marcada ? colors.surface : colors.bg,
        },
        pressed && styles.pressionado,
      ]}
    >
      {children}
      {marcada && <Icon name="check" color={colors.ink} size={20} />}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  coluna: { gap: 16 },
  secao: { gap: 8 },
  campos: { gap: 12, marginTop: 4 },
  trocar: { alignSelf: "flex-start" },
  linha: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    padding: 12,
    borderWidth: 1,
    borderRadius: raios.cardMenor,
    minHeight: 56,
  },
  texto: { flex: 1, gap: 2 },
  pressionado: { opacity: 0.85 },
});
