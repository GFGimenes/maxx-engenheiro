// Única parte escrita à mão: a ligação com o openapi-fetch.
// `openapi.json` e `schema.d.ts` são gerados por `npm run gerar` e nunca editados.
import createClient from "openapi-fetch";
import type { paths } from "./schema";

export type { components, paths } from "./schema";

// `fetch` troca o fetch global: é por onde o app põe o access token e renova a sessão.
export function createApiClient(
  baseUrl: string,
  options: { fetch?: (request: Request) => Promise<Response> } = {},
) {
  return createClient<paths>({ baseUrl, fetch: options.fetch });
}

export type ApiClient = ReturnType<typeof createApiClient>;
