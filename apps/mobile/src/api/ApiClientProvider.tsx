import { createApiClient, type ApiClient } from "@maxx/api-client";
import { createContext, useContext, type ReactNode } from "react";
import { Platform } from "react-native";

// Origem única (ADR 0001): na web a API está no mesmo host, sob /api. No nativo, a URL
// pública vem de EXPO_PUBLIC_API_URL, que não é segredo e vai para o binário.
export function defaultBaseUrl(): string {
  return Platform.OS === "web" ? "" : (process.env.EXPO_PUBLIC_API_URL ?? "");
}

const ApiClientContext = createContext<ApiClient | null>(null);

export function ApiClientProvider({
  client,
  children,
}: {
  client: ApiClient;
  children: ReactNode;
}) {
  return (
    <ApiClientContext.Provider value={client}>
      {children}
    </ApiClientContext.Provider>
  );
}

export function useApiClient(): ApiClient {
  const client = useContext(ApiClientContext);
  if (!client) {
    throw new Error("useApiClient fora do ApiClientProvider.");
  }
  return client;
}

export { createApiClient };
