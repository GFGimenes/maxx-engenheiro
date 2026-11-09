import type { components } from "@maxx/api-client";
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";

export type Aviso = components["schemas"]["AvisoResponse"];
export type EscopoDoAviso = "faculdade" | "curso" | "turma";

// O mural de quem está logado (produto.md 7), por cursor, com o filtro do chip. Os vínculos
// são da API: o app não decide o que cada um vê.
export function useMural(escopo: EscopoDoAviso | null) {
  const client = useApiClient();

  return useInfiniteQuery({
    queryKey: ["mural", escopo],
    initialPageParam: null as string | null,
    queryFn: async ({ pageParam }) => {
      const { data, error } = await client.GET("/api/v1/mural", {
        params: {
          query: {
            ...(escopo ? { escopo } : {}),
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

export function useAviso(avisoId: string) {
  const client = useApiClient();

  return useQuery({
    queryKey: ["aviso", avisoId],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/avisos/{avisoId}", {
        params: { path: { avisoId } },
      });
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// Ler muda o mural, o aviso aberto e as bolinhas: tudo isso é relido.
function useDepoisDaLeitura() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["mural"] }),
      queryClient.invalidateQueries({ queryKey: ["aviso"] }),
      queryClient.invalidateQueries({ queryKey: ["novidades"] }),
      queryClient.invalidateQueries({ queryKey: ["notificacoes"] }),
    ]);
}

// Abrir o aviso registra a leitura (passo 10, pergunta 2). Ler de novo não muda nada.
export function useLerAviso() {
  const client = useApiClient();
  const depois = useDepoisDaLeitura();

  return useMutation({
    mutationFn: async (avisoId: string) => {
      const { error, response } = await client.PUT(
        "/api/v1/avisos/{avisoId}/leitura",
        { params: { path: { avisoId } } },
      );
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSuccess: depois,
  });
}

// "Ler todos": só o que está no mural de quem pede.
export function useLerTodos() {
  const client = useApiClient();
  const depois = useDepoisDaLeitura();

  return useMutation({
    mutationFn: async () => {
      const { error, response } = await client.POST("/api/v1/mural/leitura");
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSuccess: depois,
  });
}
