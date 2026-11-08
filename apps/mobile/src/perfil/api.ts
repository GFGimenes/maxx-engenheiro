import type { components } from "@maxx/api-client";
import {
  useInfiniteQuery,
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";
import { useTurmasDoProfessor } from "../professor/api";

export type Perfil = components["schemas"]["PerfilResponse"];

// O perfil de alguém, ou o próprio (sem id). A API responde com ETag, e o cache do app
// relê em segundo plano (stale-while-revalidate).
export function usePerfil(usuarioId?: string) {
  const client = useApiClient();

  return useQuery({
    queryKey: ["perfil", usuarioId ?? "eu"],
    queryFn: async () => {
      const { data, error } = usuarioId
        ? await client.GET("/api/v1/perfis/{usuarioId}", {
            params: { path: { usuarioId } },
          })
        : await client.GET("/api/v1/eu/perfil");
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data as Perfil;
    },
  });
}

// A grade de posts, 18 por página (seis linhas de três).
export function usePostsDoPerfil(usuarioId: string | undefined) {
  const client = useApiClient();

  return useInfiniteQuery({
    queryKey: ["perfil", usuarioId, "posts"],
    enabled: !!usuarioId,
    initialPageParam: null as string | null,
    queryFn: async ({ pageParam }) => {
      const { data, error } = await client.GET(
        "/api/v1/perfis/{usuarioId}/posts",
        {
          params: {
            path: { usuarioId: usuarioId! },
            query: pageParam ? { cursor: pageParam } : {},
          },
        },
      );
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
    getNextPageParam: (pagina) => pagina.proximo ?? null,
  });
}

// Só os prêmios obtidos: faltantes e progresso são do próprio aluno, na tela de Troféus.
export function usePremiosDoPerfil(
  usuarioId: string | undefined,
  ativo: boolean,
) {
  const client = useApiClient();

  return useQuery({
    queryKey: ["perfil", usuarioId, "premios"],
    enabled: !!usuarioId && ativo,
    queryFn: async () => {
      const { data, error } = await client.GET(
        "/api/v1/perfis/{usuarioId}/premios",
        { params: { path: { usuarioId: usuarioId! } } },
      );
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// Seguir e deixar de seguir; depois, os perfis (o meu "seguindo" e o "seguidores" dele) e
// o feed, que muda de trecho, são relidos.
export function useSeguir(usuarioId: string) {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (seguir: boolean) => {
      const opcoes = { params: { path: { usuarioId } } };
      const { error, response } = seguir
        ? await client.PUT("/api/v1/seguindo/{usuarioId}", opcoes)
        : await client.DELETE("/api/v1/seguindo/{usuarioId}", opcoes);
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["perfil"] }),
        queryClient.invalidateQueries({ queryKey: ["feed"] }),
      ]),
  });
}

// As turmas vigentes do professor em que o aluno está: é por elas que o professor concede
// pelo perfil (produto.md 4.8). Lê os alunos de cada turma do professor, que são poucas.
export function useTurmasEmComum(alunoId: string | undefined, ativo: boolean) {
  const client = useApiClient();
  const turmas = useTurmasDoProfessor(ativo);
  const alunos = useQueries({
    queries: (ativo ? (turmas.data ?? []) : []).map((turma) => ({
      queryKey: ["professor", "turmas", turma.id, "alunos"],
      queryFn: async () => {
        const { data, error } = await client.GET(
          "/api/v1/professor/turmas/{turmaId}/alunos",
          { params: { path: { turmaId: turma.id } } },
        );
        if (!data) {
          throw FalhaDaApi.doCorpo(error);
        }
        return data;
      },
    })),
  });

  return (turmas.data ?? []).filter((_, indice) =>
    alunos[indice]?.data?.some((aluno) => aluno.id === alunoId),
  );
}
