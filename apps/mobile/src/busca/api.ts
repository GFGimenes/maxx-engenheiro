import type { components } from "@maxx/api-client";
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";
import {
  ESPERA_DA_DIGITACAO_MS,
  useValorParado,
  type Disponibilidade,
} from "../cadastro/useDisponibilidadeDoPrefixo";
import { erroDoPrefixo } from "../cadastro/regras";
import type { Derivadas } from "../fotos/derivadas";
import { enviarFoto } from "../fotos/envio";

export type AlunoEncontrado = components["schemas"]["AlunoEncontradoResponse"];

// Os cursos dos filtros: mudam só com o seed.
export function useCursos() {
  const client = useApiClient();

  return useQuery({
    queryKey: ["cursos"],
    staleTime: 10 * 60_000,
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/cursos");
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

export interface FiltroDaBusca {
  termo: string;
  curso: string | null;
  semestre: number | null;
}

// A regra da API (decisão D11): ao menos 2 caracteres no termo, ou um filtro. O "@" e o
// sufixo de um arroba colado não contam.
export function temCriterio({ termo, curso, semestre }: FiltroDaBusca) {
  const limpo = termo.trim().replace(/^@/, "").replace(/-.*$/, "");
  return limpo.length >= 2 || curso !== null || semestre !== null;
}

// A busca de alunos (produto.md 6.4), 20 por página. O termo só vai à API quando a
// digitação para, como no cadastro.
export function useBuscaDeAlunos(filtro: FiltroDaBusca) {
  const client = useApiClient();
  const termo = useValorParado(filtro.termo.trim(), ESPERA_DA_DIGITACAO_MS);
  const parado = { ...filtro, termo };

  return useInfiniteQuery({
    queryKey: ["busca", parado],
    enabled: temCriterio(parado),
    initialPageParam: null as string | null,
    queryFn: async ({ pageParam, signal }) => {
      const { data, error } = await client.GET("/api/v1/busca/alunos", {
        params: {
          query: {
            ...(termo ? { termo } : {}),
            ...(filtro.curso ? { curso: filtro.curso } : {}),
            ...(filtro.semestre ? { semestre: filtro.semestre } : {}),
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

// A disponibilidade do prefixo para quem já tem conta: o atual e os antigos dele estão
// livres para ele (reserva permanente). O atual nem pergunta.
export function useDisponibilidadeDoMeuPrefixo(
  prefixo: string,
  atual: string,
): Disponibilidade {
  const client = useApiClient();
  const parado = useValorParado(prefixo, ESPERA_DA_DIGITACAO_MS);
  const formaInvalida = erroDoPrefixo(prefixo);
  const ehOAtual = prefixo.toLowerCase() === atual.toLowerCase();
  const perguntar = !formaInvalida && !ehOAtual && parado === prefixo;

  const consulta = useQuery({
    queryKey: ["eu", "disponibilidade", parado],
    enabled: perguntar,
    retry: false,
    staleTime: 30_000,
    queryFn: async ({ signal }) => {
      const { data, error } = await client.GET(
        "/api/v1/eu/perfil/disponibilidade",
        { params: { query: { prefixo: parado } }, signal },
      );
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data.disponivel;
    },
  });

  if (formaInvalida) return { estado: "invalido", mensagem: formaInvalida };
  if (ehOAtual) return { estado: "disponivel" };
  if (!perguntar || consulta.isPending) return { estado: "verificando" };
  if (consulta.isError) return { estado: "nao-verificado" };
  return consulta.data ? { estado: "disponivel" } : { estado: "indisponivel" };
}

export interface EdicaoDoPerfil {
  nomeDeExibicao?: string;
  prefixo?: string;
  // Nova foto já gerada em derivadas; ou null para remover; ou undefined para não mexer.
  foto?: Derivadas | null;
}

// Salvar: sobe a foto nova, se houver, e manda só o que mudou. A API grava tudo ou nada.
export function useEditarPerfil() {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ nomeDeExibicao, prefixo, foto }: EdicaoDoPerfil) => {
      const fotoUploadId = foto
        ? await enviarFoto(client, "fotoDePerfil", foto)
        : null;
      const { data, error } = await client.PATCH("/api/v1/eu/perfil", {
        body: {
          nomeDeExibicao: nomeDeExibicao ?? null,
          prefixo: prefixo ?? null,
          fotoUploadId,
          removerFoto: foto === null ? true : null,
        },
      });
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["eu"] }),
        queryClient.invalidateQueries({ queryKey: ["perfil"] }),
        queryClient.invalidateQueries({ queryKey: ["feed"] }),
      ]),
  });
}
