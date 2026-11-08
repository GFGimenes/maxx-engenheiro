import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRef } from "react";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";
import type { Derivadas } from "../fotos/derivadas";
import { enviarFoto } from "../fotos/envio";
import { usePremios } from "../trofeus/usePremios";
import type { Tier } from "../theme/tokens";

// Uma medalha obtida que ainda não rendeu post: o que a tela de novo post oferece (um prêmio
// rende um post, produto.md 6.1). Cada concessão é uma opção, com a matéria e o bimestre.
export interface PremioParaPostar {
  id: string;
  nome: string;
  tier: Tier;
  pontos: number;
  materia: string | null;
  bimestre: number | null;
}

export function usePremiosParaPostar(ativo: boolean) {
  const premios = usePremios(ativo);
  const opcoes: PremioParaPostar[] = (premios.data?.itens ?? [])
    .filter((item) => item.tipo === "medalha")
    .flatMap((item) =>
      item.obtidos
        .filter((obtido) => !obtido.postId)
        .map((obtido) => ({
          id: obtido.id,
          nome: item.nome,
          tier: obtido.tier as Tier,
          pontos: obtido.pontos,
          materia: obtido.materia ?? null,
          bimestre: obtido.bimestre ?? null,
          obtidoEm: obtido.obtidoEm,
        })),
    )
    .sort((a, b) => b.obtidoEm.localeCompare(a.obtidoEm))
    .map(({ obtidoEm: _, ...opcao }) => opcao);

  return { ...premios, opcoes };
}

export interface NovoPost {
  legenda: string;
  derivadas: Derivadas | null;
  medalhaId: string | null;
}

// Os códigos que dizem que a foto enviada não serve mais: a próxima tentativa sobe de novo.
const FOTO_PERDIDA = new Set([
  "upload-nao-encontrado",
  "upload-ja-usado",
  "foto-nao-enviada",
]);

// Publicar: sobe a foto, se houver, e cria o post com o id do upload. Se o post falhar por
// outro motivo (rede, limite), o upload já feito é reaproveitado ao tentar de novo, para não
// gastar outro pedido do limite de 30 por hora.
export function usePublicarPost() {
  const client = useApiClient();
  const queryClient = useQueryClient();
  const enviado = useRef<{ derivadas: Derivadas; uploadId: string } | null>(
    null,
  );

  return useMutation({
    mutationFn: async ({ legenda, derivadas, medalhaId }: NovoPost) => {
      let uploadId: string | null = null;
      if (derivadas) {
        uploadId =
          enviado.current?.derivadas === derivadas
            ? enviado.current.uploadId
            : await enviarFoto(client, "post", derivadas);
        enviado.current = { derivadas, uploadId };
      }

      const { data, error } = await client.POST("/api/v1/posts", {
        body: {
          legenda: legenda.trim() === "" ? null : legenda,
          uploadId,
          medalhaId,
        },
      });
      if (!data) {
        const falha = FalhaDaApi.doCorpo(error);
        if (falha.codigo && FOTO_PERDIDA.has(falha.codigo)) {
          enviado.current = null;
        }
        throw falha;
      }
      enviado.current = null;
      return data;
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["feed"] }),
        queryClient.invalidateQueries({ queryKey: ["eu", "premios"] }),
        queryClient.invalidateQueries({ queryKey: ["perfil"] }),
      ]),
  });
}
