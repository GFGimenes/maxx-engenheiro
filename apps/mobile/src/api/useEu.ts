import { useQuery } from "@tanstack/react-query";

import { useApiClient } from "./ApiClientProvider";

// Quem é o dono da sessão, lido do banco pela API (o arroba pode ter mudado).
export function useEu() {
  const client = useApiClient();

  return useQuery({
    queryKey: ["eu"],
    queryFn: async () => {
      const { data, response } = await client.GET("/api/v1/eu");
      if (!data) {
        throw new Error(`GET /api/v1/eu respondeu ${response.status}.`);
      }
      return data;
    },
  });
}
