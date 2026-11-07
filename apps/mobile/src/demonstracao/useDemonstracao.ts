import { useQuery } from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";

// A demonstração existe? A API responde com os cursos só com a flag ligada; desligada, a
// rota nem existe e responde 404. O app não carrega flag nenhuma no bundle: pergunta.
export function useDemonstracao() {
  const client = useApiClient();

  return useQuery({
    queryKey: ["demonstracao"],
    retry: false,
    staleTime: 5 * 60_000,
    queryFn: async () => {
      const { data } = await client.GET("/api/v1/demo");
      // 404, ou qualquer outra resposta sem corpo: sem demonstração. Falha de rede vira
      // erro da consulta, e o botão também não aparece.
      return data?.cursos ?? null;
    },
  });
}
