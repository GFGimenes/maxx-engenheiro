import { useQuery } from "@tanstack/react-query";

import { useApiClient } from "./ApiClientProvider";

export type HealthResult = { httpStatus: number; status: string };

export function useHealth() {
  const client = useApiClient();

  return useQuery({
    queryKey: ["health"],
    queryFn: async (): Promise<HealthResult> => {
      const { data, response } = await client.GET("/api/health");
      if (!response.ok || !data) {
        throw new Error(`GET /api/health respondeu ${response.status}.`);
      }
      return { httpStatus: response.status, status: data.status };
    },
  });
}
