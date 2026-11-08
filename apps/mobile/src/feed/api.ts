import type { components } from "@maxx/api-client";
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";

export type Post = components["schemas"]["PostResponse"];
export type Comentario = components["schemas"]["ComentarioResponse"];

// O feed por cursor (produto.md 6.2). A primeira página sorteia a semente da sessão;
// rolar segue o cursor, com a mesma ordem. Puxar para atualizar zera as páginas
// (resetQueries), e a primeira página de novo é uma sessão nova.
export function useFeed() {
  const client = useApiClient();

  return useInfiniteQuery({
    queryKey: ["feed"],
    initialPageParam: null as string | null,
    queryFn: async ({ pageParam }) => {
      const { data, error } = await client.GET("/api/v1/feed", {
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

export function usePost(postId: string) {
  const client = useApiClient();

  return useQuery({
    queryKey: ["post", postId],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/posts/{postId}", {
        params: { path: { postId } },
      });
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// Curtir e descurtir respondem o estado depois do pedido. O cartão mostra a mudança na
// hora e acerta com a resposta; o cache não é relido, para a lista não pular.
export function useCurtir(postId: string) {
  const client = useApiClient();

  return useMutation({
    mutationFn: async (curtir: boolean) => {
      const opcoes = { params: { path: { postId } } };
      const { data, error } = curtir
        ? await client.PUT("/api/v1/posts/{postId}/curtida", opcoes)
        : await client.DELETE("/api/v1/posts/{postId}/curtida", opcoes);
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// Apagar (autor) e remover (instituição) são a mesma rota: quem decide o modo é a API.
export function useRemoverPost() {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (postId: string) => {
      const { error, response } = await client.DELETE(
        "/api/v1/posts/{postId}",
        { params: { path: { postId } } },
      );
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSuccess: (_, postId) => {
      queryClient.removeQueries({ queryKey: ["post", postId] });
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: ["feed"] }),
        queryClient.invalidateQueries({ queryKey: ["eu", "premios"] }),
        queryClient.invalidateQueries({ queryKey: ["perfil"] }),
      ]);
    },
  });
}

// Do mais antigo ao mais novo, 20 por página.
export function useComentarios(postId: string) {
  const client = useApiClient();

  return useInfiniteQuery({
    queryKey: ["post", postId, "comentarios"],
    initialPageParam: null as string | null,
    queryFn: async ({ pageParam }) => {
      const { data, error } = await client.GET(
        "/api/v1/posts/{postId}/comentarios",
        {
          params: {
            path: { postId },
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

// Comentar ou apagar relê os comentários e o post, que conta os comentários.
function useAtualizarComentarios(postId: string) {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["post", postId] }),
      queryClient.invalidateQueries({ queryKey: ["feed"] }),
    ]);
}

export function useComentar(postId: string) {
  const client = useApiClient();
  const atualizar = useAtualizarComentarios(postId);

  return useMutation({
    mutationFn: async (texto: string) => {
      const { data, error } = await client.POST(
        "/api/v1/posts/{postId}/comentarios",
        { params: { path: { postId } }, body: { texto } },
      );
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
    onSuccess: atualizar,
  });
}

export function useRemoverComentario(postId: string) {
  const client = useApiClient();
  const atualizar = useAtualizarComentarios(postId);

  return useMutation({
    mutationFn: async (comentarioId: string) => {
      const { error, response } = await client.DELETE(
        "/api/v1/posts/{postId}/comentarios/{comentarioId}",
        { params: { path: { postId, comentarioId } } },
      );
      if (!response.ok) {
        throw FalhaDaApi.doCorpo(error);
      }
    },
    onSuccess: atualizar,
  });
}
