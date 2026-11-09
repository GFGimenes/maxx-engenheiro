import type { components } from "@maxx/api-client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { useApiClient } from "../../api/ApiClientProvider";
import { FalhaDaApi } from "../../api/FalhaDaApi";
import type { FontesDaEscolha } from "../../premiosParticipacao/EscolhaDaReivindicacao";
import type { EventoEscolhido } from "../../premiosParticipacao/api";

export type AlunoDaBase = components["schemas"]["AlunoDaBaseResponse"];
export type FichaDoAluno = components["schemas"]["FichaDoAlunoResponse"];
export type MedalhaDaFicha = components["schemas"]["MedalhaDaFichaResponse"];

// A busca da instituição na base de alunos (passo 12): nome, arroba, e-mail ou RA, com conta
// ou sem. A API pede ao menos 2 caracteres; antes disso, nada a buscar.
export function useBuscaDeAlunos(termo: string) {
  const client = useApiClient();
  const limpo = termo.trim();

  return useQuery({
    queryKey: ["instituicao", "alunos", limpo],
    enabled: limpo.length >= 2,
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/instituicao/alunos", {
        params: { query: { busca: limpo } },
      });
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

export function useFicha(cadastroId: string) {
  const client = useApiClient();

  return useQuery({
    queryKey: ["instituicao", "ficha", cadastroId],
    queryFn: async () => {
      const { data, error } = await client.GET(
        "/api/v1/instituicao/alunos/{cadastroId}",
        { params: { path: { cadastroId } } },
      );
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// Todo ato muda a ficha e o que a busca mostra (e-mail, curso, sufixo do arroba). O ranking,
// a raridade e os pontos mudam pelo worker; o perfil e o feed são relidos ao voltar.
function useAtualizar(cadastroId: string) {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({
        queryKey: ["instituicao", "ficha", cadastroId],
      }),
      queryClient.invalidateQueries({ queryKey: ["instituicao", "alunos"] }),
      queryClient.invalidateQueries({ queryKey: ["perfil"] }),
      queryClient.invalidateQueries({ queryKey: ["feed"] }),
    ]);
}

function comResposta(response: Response, error: unknown) {
  if (!response.ok) {
    throw FalhaDaApi.doCorpo(error);
  }
}

export function useCorrigirEmail(cadastroId: string) {
  const client = useApiClient();
  const atualizar = useAtualizar(cadastroId);

  return useMutation({
    mutationFn: async (email: string) => {
      const { error, response } = await client.PUT(
        "/api/v1/instituicao/alunos/{cadastroId}/email",
        { params: { path: { cadastroId } }, body: { email } },
      );
      comResposta(response, error);
    },
    onSuccess: atualizar,
  });
}

export function useMarcarExAluno(cadastroId: string) {
  const client = useApiClient();
  const atualizar = useAtualizar(cadastroId);

  return useMutation({
    mutationFn: async () => {
      const { error, response } = await client.POST(
        "/api/v1/instituicao/alunos/{cadastroId}/ex-aluno",
        { params: { path: { cadastroId } } },
      );
      comResposta(response, error);
    },
    onSuccess: atualizar,
  });
}

export function useTrocarDeCurso(cadastroId: string) {
  const client = useApiClient();
  const atualizar = useAtualizar(cadastroId);

  return useMutation({
    mutationFn: async ({
      cursoId,
      semestre,
    }: {
      cursoId: string;
      semestre: number;
    }) => {
      const { error, response } = await client.PUT(
        "/api/v1/instituicao/alunos/{cadastroId}/curso",
        { params: { path: { cadastroId } }, body: { cursoId, semestre } },
      );
      comResposta(response, error);
    },
    onSuccess: atualizar,
  });
}

export function useAnularMedalha(cadastroId: string) {
  const client = useApiClient();
  const atualizar = useAtualizar(cadastroId);

  return useMutation({
    mutationFn: async (medalhaId: string) => {
      const { error, response } = await client.POST(
        "/api/v1/instituicao/medalhas/{medalhaId}/anulacao",
        { params: { path: { medalhaId } } },
      );
      comResposta(response, error);
    },
    onSuccess: atualizar,
  });
}

// Os prêmios de participação que a instituição pode conceder ao aluno da ficha.
export function useItensParaConcessao(cadastroId: string) {
  const client = useApiClient();

  return useQuery({
    queryKey: ["instituicao", "concessao", cadastroId, "itens"],
    queryFn: async () => {
      const { data, error } = await client.GET(
        "/api/v1/instituicao/alunos/{cadastroId}/premios-participacao",
        { params: { path: { cadastroId } } },
      );
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}

// Os eventos e as matérias do aluno da ficha, para a mesma escolha da reivindicação.
export function fontesDoAluno(cadastroId: string): FontesDaEscolha {
  return {
    useEventosDoItem: function useEventosDoAluno(item) {
      const client = useApiClient();
      return useQuery({
        queryKey: ["instituicao", "concessao", cadastroId, "eventos", item],
        enabled: item !== null,
        queryFn: async () => {
          const { data, error } = await client.GET(
            "/api/v1/instituicao/alunos/{cadastroId}/eventos",
            { params: { path: { cadastroId }, query: { item: item! } } },
          );
          if (!data) {
            throw FalhaDaApi.doCorpo(error);
          }
          return data;
        },
      });
    },
    useMaterias: function useMateriasDoAluno(ativo) {
      const client = useApiClient();
      return useQuery({
        queryKey: ["instituicao", "concessao", cadastroId, "materias"],
        enabled: ativo,
        queryFn: async () => {
          const { data, error } = await client.GET(
            "/api/v1/instituicao/alunos/{cadastroId}/materias",
            { params: { path: { cadastroId } } },
          );
          if (!data) {
            throw FalhaDaApi.doCorpo(error);
          }
          return data;
        },
      });
    },
  };
}

export function useConceder(cadastroId: string) {
  const client = useApiClient();
  const atualizar = useAtualizar(cadastroId);

  return useMutation({
    mutationFn: async ({
      item,
      evento,
      materiaId,
    }: {
      item: string;
      evento: EventoEscolhido;
      materiaId: string | null;
    }) => {
      const { data, error } = await client.POST(
        "/api/v1/instituicao/alunos/{cadastroId}/premios-participacao",
        {
          params: { path: { cadastroId } },
          body: {
            item,
            materiaId,
            ...(evento.tipo === "semeado"
              ? { eventoId: evento.id, eventoDigitado: null }
              : {
                  eventoId: null,
                  eventoDigitado: {
                    nome: evento.nome,
                    inicio: evento.inicio,
                    fim: evento.fim,
                  },
                }),
          },
        },
      );
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
    onSuccess: atualizar,
  });
}
