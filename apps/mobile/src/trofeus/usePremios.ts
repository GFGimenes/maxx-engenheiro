import { useQuery } from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";

// O catálogo do próprio aluno, com critério, raridade e o que ele já obteve. Só o dono vê
// os faltantes (produto.md 9.1): a rota é de /eu. `ativo` falso para quem não é aluno.
export function usePremios(ativo: boolean) {
  const client = useApiClient();

  return useQuery({
    queryKey: ["eu", "premios"],
    enabled: ativo,
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/eu/premios");
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}
