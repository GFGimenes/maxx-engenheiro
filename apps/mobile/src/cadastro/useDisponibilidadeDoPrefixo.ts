import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";
import { erroDoPrefixo } from "./regras";

// Espera a digitação parar antes de perguntar: sugestão e disponibilidade somam 60
// consultas por minuto por IP (decisoes/README.md, Limites de taxa).
export const ESPERA_DA_DIGITACAO_MS = 500;

export type Disponibilidade =
  | { estado: "invalido"; mensagem: string }
  | { estado: "verificando" }
  | { estado: "disponivel" }
  | { estado: "indisponivel" }
  // 429 ou rede: não bloqueia. O cadastro confere de novo ao criar a conta.
  | { estado: "nao-verificado" };

// O valor depois que a digitação para: a pergunta à API sai uma vez, não a cada tecla.
export function useValorParado<T>(valor: T, espera: number): T {
  const [parado, setParado] = useState(valor);
  useEffect(() => {
    const relogio = setTimeout(() => setParado(valor), espera);
    return () => clearTimeout(relogio);
  }, [valor, espera]);
  return parado;
}

export function useDisponibilidadeDoPrefixo(
  ra: string,
  email: string,
  prefixo: string,
  prefixoSugerido: string,
): Disponibilidade {
  const client = useApiClient();
  const parado = useValorParado(prefixo, ESPERA_DA_DIGITACAO_MS);
  const formaInvalida = erroDoPrefixo(prefixo);
  // A sugestão acabou de sair livre da API: não precisa perguntar de novo.
  const ehASugestao = prefixo.toLowerCase() === prefixoSugerido.toLowerCase();
  const perguntar = !formaInvalida && !ehASugestao && parado === prefixo;

  const consulta = useQuery({
    queryKey: ["cadastro", "disponibilidade", ra, email, parado],
    enabled: perguntar,
    retry: false,
    staleTime: 30_000,
    // Trocar o prefixo cancela a pergunta anterior, que nem chega a ser lida.
    queryFn: async ({ signal }) => {
      const { data, error } = await client.POST(
        "/api/v1/auth/cadastro/disponibilidade",
        { body: { ra, email, prefixo: parado }, signal },
      );
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data.disponivel;
    },
  });

  if (formaInvalida) {
    return { estado: "invalido", mensagem: formaInvalida };
  }
  if (ehASugestao) {
    return { estado: "disponivel" };
  }
  if (!perguntar || consulta.isPending) {
    return { estado: "verificando" };
  }
  if (consulta.isError) {
    return { estado: "nao-verificado" };
  }
  return { estado: consulta.data ? "disponivel" : "indisponivel" };
}
