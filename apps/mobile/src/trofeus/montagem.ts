import type { components } from "@maxx/api-client";

import { rotulosDeTier } from "../premios/rotulos";
import type { Raridade, Tier } from "../theme/tokens";

export type PremiosDoAluno = components["schemas"]["PremiosDoAlunoResponse"];
export type ItemDoAluno = components["schemas"]["ItemDoAlunoResponse"];
export type Familia = components["schemas"]["FamiliaResponse"];

// As duas abas da tela (prancheta trofeus-*.png). "Troféu" é rótulo só de interface: cobre
// prêmio de participação e conquista; medalha fica na outra aba (produto.md 4.1).
export type AbaDeTrofeus = "trofeus" | "medalhas";

export const ehDaAba = (item: ItemDoAluno, aba: AbaDeTrofeus) =>
  aba === "medalhas" ? item.tipo === "medalha" : item.tipo !== "medalha";

const obtido = (item: ItemDoAluno) => item.obtidos.length > 0;

const pontosObtidos = (item: ItemDoAluno) =>
  item.obtidos.reduce((soma, premio) => soma + premio.pontos, 0);

// Os itens da aba: os obtidos primeiro, depois os que faltam, cada grupo do maior valor
// para o menor. Sem "mostrar não obtidos", só os obtidos.
export function itensDaAba(
  itens: readonly ItemDoAluno[],
  aba: AbaDeTrofeus,
  mostrarNaoObtidos: boolean,
): ItemDoAluno[] {
  const daAba = itens.filter((item) => ehDaAba(item, aba));
  const porValor = (a: ItemDoAluno, b: ItemDoAluno) =>
    b.pontos - a.pontos || a.nome.localeCompare(b.nome, "pt-BR");
  const obtidos = daAba.filter(obtido).sort(porValor);
  return mostrarNaoObtidos
    ? [...obtidos, ...daAba.filter((item) => !obtido(item)).sort(porValor)]
    : obtidos;
}

export interface PassoDaTrilha {
  id: string;
  rotulo: string;
  tier: Tier;
  glifo: "taca" | "fita";
  obtido: boolean;
  detalhes: string[];
}

export interface Trilha {
  codigo: string;
  titulo: string;
  resumo: string;
  passos: PassoDaTrilha[];
}

// As trilhas das famílias em que o aluno já obteve ao menos um item e que têm item da aba.
// A trilha mostra a família inteira, na ordem, com o próximo em cadeado (produto.md 4.4).
export function trilhasDaAba(
  premios: PremiosDoAluno,
  aba: AbaDeTrofeus,
): Trilha[] {
  return premios.familias
    .map((familia) => {
      const itens = premios.itens
        .filter((item) => item.familia === familia.codigo)
        .sort((a, b) => (a.ordemNaFamilia ?? 0) - (b.ordemNaFamilia ?? 0));
      return { familia, itens };
    })
    .filter(
      ({ itens }) =>
        itens.length >= 2 &&
        itens.some(obtido) &&
        itens.some((item) => ehDaAba(item, aba)),
    )
    .sort(
      (a, b) =>
        b.itens.filter(obtido).length - a.itens.filter(obtido).length ||
        a.familia.nome.localeCompare(b.familia.nome, "pt-BR"),
    )
    .map(({ familia, itens }) => {
      const proximo = itens.findIndex((item) => !obtido(item));
      return {
        codigo: familia.codigo,
        titulo: familia.nome,
        resumo: `${itens.filter(obtido).length} de ${itens.length} · ${formatarPontos(
          itens.reduce((soma, item) => soma + pontosObtidos(item), 0),
        )}`,
        passos: itens.map((item, indice) => ({
          id: item.codigo,
          // Como na prancheta, o passo diz o tier; o nome do item está na lista abaixo.
          rotulo: rotulosDeTier[item.tier as Tier],
          tier: item.tier as Tier,
          glifo: glifoDe(item),
          obtido: obtido(item),
          detalhes: obtido(item)
            ? [`+${formatarNumero(pontosObtidos(item))} pts`]
            : indice === proximo
              ? [`${formatarNumero(item.pontos)} pts`, "próximo"]
              : [`${formatarNumero(item.pontos)} pts`],
        })),
      };
    });
}

export const glifoDe = (item: ItemDoAluno): "taca" | "fita" =>
  item.tipo === "medalha" ? "fita" : "taca";

export const raridadeDe = (item: ItemDoAluno) =>
  item.raridade?.nivel as Raridade | undefined;

export const pontosDaLinha = (item: ItemDoAluno) =>
  obtido(item)
    ? `+${formatarNumero(pontosObtidos(item))}`
    : formatarNumero(item.pontos);

// Números no formato brasileiro: 15.020 (identidade-visual.md).
export const formatarNumero = (n: number) => n.toLocaleString("pt-BR");

export const formatarPontos = (n: number) => `${formatarNumero(n)} pts`;

// "12,5% dos alunos": o texto da raridade (produto.md 4.3).
export const textoDaRaridade = (percentual: number) =>
  `${percentual.toLocaleString("pt-BR", { maximumFractionDigits: 1 })}% dos alunos`;

// A linha de detalhe: o critério, ou, na medalha obtida, onde e quando veio a mais recente.
export function detalheDaLinha(item: ItemDoAluno): string {
  const [maisRecente] = item.obtidos;
  if (!maisRecente || !maisRecente.materia) {
    return item.criterio;
  }

  const vezes =
    item.obtidos.length > 1 ? ` · ${item.obtidos.length} vezes` : "";
  const bimestre = maisRecente.bimestre
    ? ` · ${maisRecente.bimestre}º bimestre`
    : "";
  return `${maisRecente.materia}${bimestre}${vezes}`;
}
