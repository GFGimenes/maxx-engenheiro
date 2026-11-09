import type { components } from "@maxx/api-client";
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";

export type Notificacao = components["schemas"]["NotificacaoResponse"];

// Sem push na fase 1 (produto.md 8): o app pergunta ao voltar ao foco e a cada 60 s (D5 do
// passo 10). São três booleanos, baratos.
export const INTERVALO_DAS_NOVIDADES = 60_000;

// O que acende as três bolinhas: o sininho, a aba Mural e a aba Troféus. Sem resposta, nada
// acende: a bolinha é um convite, não um aviso de erro.
export function useNovidades() {
  const client = useApiClient();

  return useQuery({
    queryKey: ["novidades"],
    refetchInterval: INTERVALO_DAS_NOVIDADES,
    refetchOnWindowFocus: true,
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/eu/novidades");
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

export function useNotificacoes() {
  const client = useApiClient();

  return useInfiniteQuery({
    queryKey: ["notificacoes"],
    initialPageParam: null as string | null,
    queryFn: async ({ pageParam }) => {
      const { data, error } = await client.GET("/api/v1/eu/notificacoes", {
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

// Abrir o sininho marca tudo como lido (passo 10, pergunta 6), até a mais nova que a tela
// mostrou: a que chegar depois fica acesa. A lista não é relida aqui, para quem acabou de
// abrir ainda ver em negrito o que era novo; só as bolinhas.
export function useLerNotificacoes() {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (ate: string) => {
      const { error, response } = await client.POST(
        "/api/v1/eu/notificacoes/leitura",
        { body: { ate } },
      );
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["novidades"] }),
  });
}

// Abrir a aba Troféus apaga a bolinha dela (passo 10, pergunta 5). Não lê o sininho.
export function useVerTrofeus() {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      const { error, response } = await client.PUT("/api/v1/eu/trofeus/visto");
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["novidades"] }),
  });
}
