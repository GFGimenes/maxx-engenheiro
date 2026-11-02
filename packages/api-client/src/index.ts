// Única parte escrita à mão: a ligação com o openapi-fetch.
// `openapi.json` e `schema.d.ts` são gerados por `npm run gerar` e nunca editados.
import createClient from "openapi-fetch";
import type { paths } from "./schema";

export type { components, paths } from "./schema";

export function createApiClient(baseUrl: string) {
  return createClient<paths>({ baseUrl });
}

export type ApiClient = ReturnType<typeof createApiClient>;
