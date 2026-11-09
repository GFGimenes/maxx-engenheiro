import type { components } from "@maxx/api-client";
import { useInfiniteQuery } from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";

export type Ranking = components["schemas"]["RankingResponse"];
export type LinhaDoRanking = components["schemas"]["LinhaDoRankingResponse"];

// As quatro visões (produto.md 5), como os chips da prancheta as nomeiam. A API recebe o
// escopo e o período em texto, como o mural (decisão da etapa 4 do passo 11).
export type Visao = "geral" | "curso" | "historico" | "historico-curso";

export const VISOES: Record<
  Visao,
  {
    escopo: "geral" | "curso";
    periodo: "semestre" | "historico";
    chip: string;
    nome: string;
  }
> = {
  geral: { escopo: "geral", periodo: "semestre", chip: "Geral", nome: "geral" },
  curso: { escopo: "curso", periodo: "semestre", chip: "Curso", nome: "curso" },
  historico: {
    escopo: "geral",
    periodo: "historico",
    chip: "Histórico",
    nome: "histórico",
  },
  "historico-curso": {
    escopo: "curso",
    periodo: "historico",
    chip: "Histórico do curso",
    nome: "histórico do curso",
  },
};

export function ehVisao(valor: unknown): valor is Visao {
  return typeof valor === "string" && valor in VISOES;
}

export interface QuemLe {
  aluno: boolean;
  exAluno: boolean;
}

// Quem vê o quê (pergunta 6 do passo 11): o aluno, as quatro; o ex-aluno, sem o curso no
// semestre, porque do curso vê só o histórico; professor e instituição, só o geral.
export function visoesPara({ aluno, exAluno }: QuemLe): Visao[] {
  if (!aluno) return ["geral", "historico"];
  if (exAluno) return ["geral", "historico", "historico-curso"];
  return ["geral", "curso", "historico", "historico-curso"];
}

// "2026/2" vira "Semestre 2/2026", como na prancheta.
export function rotuloDoSemestre(codigo: string | null | undefined) {
  if (!codigo) return null;
  const [ano, numero] = codigo.split("/");
  return `Semestre ${numero}/${ano}`;
}

// A visão, 20 linhas por página, com a linha do próprio aluno à parte. A posição vem do
// worker e pode estar alguns segundos atrás dos pontos (ADR 0005): ao voltar à tela, a
// consulta relê.
export function useRanking(visao: Visao, seguidos: boolean) {
  const client = useApiClient();
  const { escopo, periodo } = VISOES[visao];

  return useInfiniteQuery({
    queryKey: ["ranking", visao, seguidos],
    initialPageParam: null as string | null,
    queryFn: async ({ pageParam, signal }) => {
      const { data, error } = await client.GET("/api/v1/rankings", {
        params: {
          query: {
            escopo,
            periodo,
            ...(seguidos ? { seguidos: true } : {}),
            ...(pageParam ? { cursor: pageParam } : {}),
          },
        },
        signal,
      });
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
    getNextPageParam: (pagina) => pagina.proximo ?? null,
  });
}
