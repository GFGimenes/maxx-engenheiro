import type { components } from "@maxx/api-client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef } from "react";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";
import type { Derivadas } from "../fotos/derivadas";
import { enviarFoto } from "../fotos/envio";

export type EventoSemeado = components["schemas"]["EventoResponse"];
export type MateriaDoAluno = components["schemas"]["MateriaResponse"];
export type Reivindicacao = components["schemas"]["ReivindicacaoResponse"];

// Os eventos semeados que o aluno pode escolher para o item (produto.md 4.6). Sem item,
// nada a buscar: o evento depende do prêmio escolhido.
export function useEventos(item: string | null) {
  const client = useApiClient();

  return useQuery({
    queryKey: ["eventos", item],
    enabled: item !== null,
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/eventos", {
        params: { query: { item: item! } },
      });
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// As matérias que o aluno cursa hoje, para a apresentação em sala.
export function useMateriasDoAluno(ativo: boolean) {
  const client = useApiClient();

  return useQuery({
    queryKey: ["eu", "materias"],
    enabled: ativo,
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/eu/materias");
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// As reivindicações que ainda não pontuam (D9): pendente, em averiguação, reprovada e
// revogada, com o motivo nas duas últimas.
export function useReivindicacoes(ativo: boolean) {
  const client = useApiClient();

  return useQuery({
    queryKey: ["eu", "reivindicacoes"],
    enabled: ativo,
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/eu/reivindicacoes");
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// O evento: o id de um semeado ou o digitado, com datas ISO (aaaa-mm-dd).
export type EventoEscolhido =
  | { tipo: "semeado"; id: string }
  | { tipo: "digitado"; nome: string; inicio: string; fim: string };

export interface NovaReivindicacao {
  item: string;
  evento: EventoEscolhido;
  materiaId: string | null;
  derivadas: Derivadas;
  legenda: string;
}

const FOTO_PERDIDA = new Set([
  "upload-nao-encontrado",
  "upload-ja-usado",
  "foto-nao-enviada",
]);

// Reivindicar: sobe a foto, que é a comprovação, e cria o post de reivindicação. Como na
// publicação, o upload já feito é reaproveitado se a reivindicação falhar por outro motivo.
export function useReivindicar() {
  const client = useApiClient();
  const queryClient = useQueryClient();
  const enviado = useRef<{ derivadas: Derivadas; uploadId: string } | null>(
    null,
  );

  return useMutation({
    mutationFn: async ({
      item,
      evento,
      materiaId,
      derivadas,
      legenda,
    }: NovaReivindicacao) => {
      const uploadId =
        enviado.current?.derivadas === derivadas
          ? enviado.current.uploadId
          : await enviarFoto(client, "post", derivadas);
      enviado.current = { derivadas, uploadId };

      const { data, error } = await client.POST("/api/v1/reivindicacoes", {
        body: {
          item,
          eventoId: evento.tipo === "semeado" ? evento.id : null,
          eventoDigitado:
            evento.tipo === "digitado"
              ? { nome: evento.nome, inicio: evento.inicio, fim: evento.fim }
              : null,
          materiaId,
          uploadId,
          legenda: legenda.trim() === "" ? null : legenda,
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
        queryClient.invalidateQueries({ queryKey: ["eu", "reivindicacoes"] }),
        queryClient.invalidateQueries({ queryKey: ["perfil"] }),
      ]),
  });
}
