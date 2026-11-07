import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";

// As consultas e a concessão do professor (passo 6). O escopo de turma é conferido pela
// API no banco: uma turma que não é dele responde 403 com `fora-do-escopo`.

export function useTurmasDoProfessor() {
  const client = useApiClient();

  return useQuery({
    queryKey: ["professor", "turmas"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/professor/turmas");
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

export function useAlunosDaTurma(turmaId: string) {
  const client = useApiClient();

  return useQuery({
    queryKey: ["professor", "turmas", turmaId, "alunos"],
    queryFn: async () => {
      const { data, error } = await client.GET(
        "/api/v1/professor/turmas/{turmaId}/alunos",
        { params: { path: { turmaId } } },
      );
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// O catálogo muda só com o seed: pode ficar em cache por mais tempo.
export function useMedalhasDoCatalogo() {
  const client = useApiClient();

  return useQuery({
    queryKey: ["professor", "medalhas"],
    staleTime: 10 * 60_000,
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/professor/medalhas");
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// Concedida, a lista da turma é lida de novo: a medalha nova aparece na linha do aluno.
export function useConcederMedalha(turmaId: string) {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (pedido: { alunoId: string; item: string }) => {
      const { data, error } = await client.POST(
        "/api/v1/professor/turmas/{turmaId}/medalhas",
        { params: { path: { turmaId } }, body: pedido },
      );
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: ["professor", "turmas"],
      }),
  });
}
