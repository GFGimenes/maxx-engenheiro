import type { components } from "@maxx/api-client";
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";

export type ComprovacaoNaFila =
  components["schemas"]["ComprovacaoNaFilaResponse"];
export type EventoNaFila = components["schemas"]["EventoNaFilaResponse"];

// A fila de comprovações (produto.md 4.7): das mais antigas para as mais novas, 20 por
// página, só com os alunos do professor no semestre vigente, conferido pela API. Com o
// evento, só as dele.
export function useFilaDeComprovacoes(eventoId: string | null) {
  const client = useApiClient();

  return useInfiniteQuery({
    queryKey: ["comprovacoes", eventoId],
    initialPageParam: null as string | null,
    queryFn: async ({ pageParam }) => {
      const { data, error } = await client.GET("/api/v1/comprovacoes", {
        params: {
          query: {
            ...(eventoId ? { eventoId } : {}),
            ...(pageParam ? { cursor: pageParam } : {}),
          },
        },
      });
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
    getNextPageParam: (pagina) => pagina.proximo ?? null,
  });
}

// Os eventos com comprovação pendente, para os chips do filtro.
export function useEventosDaFila() {
  const client = useApiClient();

  return useQuery({
    queryKey: ["comprovacoes", "eventos"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/comprovacoes/eventos");
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// Decidida (ou decidida por outra pessoa), a comprovação sai da fila: a fila e os chips
// são relidos. O feed também, porque a marca do post muda.
function useAtualizarFila() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["comprovacoes"] }),
      queryClient.invalidateQueries({ queryKey: ["feed"] }),
    ]);
}

export function useAprovar() {
  const client = useApiClient();
  const atualizar = useAtualizarFila();

  return useMutation({
    mutationFn: async (comprovacaoId: string) => {
      const { error, response } = await client.POST(
        "/api/v1/comprovacoes/{comprovacaoId}/aprovacao",
        { params: { path: { comprovacaoId } } },
      );
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSettled: atualizar,
  });
}

export function useReprovar() {
  const client = useApiClient();
  const atualizar = useAtualizarFila();

  return useMutation({
    mutationFn: async ({
      comprovacaoId,
      motivo,
    }: {
      comprovacaoId: string;
      motivo: string;
    }) => {
      const { error, response } = await client.POST(
        "/api/v1/comprovacoes/{comprovacaoId}/reprovacao",
        { params: { path: { comprovacaoId } }, body: { motivo } },
      );
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSettled: atualizar,
  });
}

// Contestação do professor (produto.md 4.7), a partir do "..." do post. O post relido perde
// os pontos e o tier para quem não é o aluno nem a instituição.
export function useContestar() {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      premioId,
      motivo,
    }: {
      premioId: string;
      motivo: string;
    }) => {
      const { error, response } = await client.POST(
        "/api/v1/premios-participacao/{premioId}/contestacao",
        { params: { path: { premioId } }, body: { motivo } },
      );
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["feed"] }),
        queryClient.invalidateQueries({ queryKey: ["post"] }),
        queryClient.invalidateQueries({ queryKey: ["perfil"] }),
      ]),
  });
}
