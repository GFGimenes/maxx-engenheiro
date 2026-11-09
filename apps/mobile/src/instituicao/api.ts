import type { components } from "@maxx/api-client";
import {
  useInfiniteQuery,
  useMutation,
  useQueryClient,
} from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";

export type ContestacaoNaFila =
  components["schemas"]["ContestacaoNaFilaResponse"];

// A fila de contestações da instituição (produto.md 4.7): as abertas, das mais antigas para
// as mais novas, 20 por página, com a marca de 7 dias.
export function useFilaDeContestacoes() {
  const client = useApiClient();

  return useInfiniteQuery({
    queryKey: ["contestacoes"],
    initialPageParam: null as string | null,
    queryFn: async ({ pageParam }) => {
      const { data, error } = await client.GET("/api/v1/contestacoes", {
        params: { query: pageParam ? { cursor: pageParam } : {} },
      });
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
    getNextPageParam: (pagina) => pagina.proximo ?? null,
  });
}

// Decidida (ou decidida por outra pessoa), a contestação sai da fila. O post do prêmio muda
// para quem o vê (volta a pontuar ou perde o vínculo), então feed, post e perfil são relidos.
function useAtualizarFila() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all(
      ["contestacoes", "feed", "post", "perfil"].map((chave) =>
        queryClient.invalidateQueries({ queryKey: [chave] }),
      ),
    );
}

export function useConfirmar() {
  const client = useApiClient();
  const atualizar = useAtualizarFila();

  return useMutation({
    mutationFn: async (contestacaoId: string) => {
      const { error, response } = await client.POST(
        "/api/v1/contestacoes/{contestacaoId}/confirmacao",
        { params: { path: { contestacaoId } } },
      );
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSettled: atualizar,
  });
}

export function useRevogar() {
  const client = useApiClient();
  const atualizar = useAtualizarFila();

  return useMutation({
    mutationFn: async ({
      contestacaoId,
      motivo,
    }: {
      contestacaoId: string;
      motivo: string;
    }) => {
      const { error, response } = await client.POST(
        "/api/v1/contestacoes/{contestacaoId}/revogacao",
        { params: { path: { contestacaoId } }, body: { motivo } },
      );
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSettled: atualizar,
  });
}
