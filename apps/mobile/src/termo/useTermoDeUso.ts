import { useQuery } from "@tanstack/react-query";

import { useApiClient } from "../api/ApiClientProvider";
import { FalhaDaApi } from "../api/FalhaDaApi";

// O termo vigente, sempre da API: o app instalado atrasa, e o aceite é do texto que vale.
export function useTermoDeUso() {
  const client = useApiClient();

  return useQuery({
    queryKey: ["termo-de-uso"],
    queryFn: async () => {
      const { data, error } = await client.GET("/api/v1/termo-de-uso");
      if (!data) {
        throw FalhaDaApi.doCorpo(error);
      }
      return data;
    },
  });
}
