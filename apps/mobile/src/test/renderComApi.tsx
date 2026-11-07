import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render } from "@testing-library/react-native";
import type { ReactElement } from "react";
import { SafeAreaProvider } from "react-native-safe-area-context";

import { ApiClientProvider, createApiClient } from "../api/ApiClientProvider";
import { GerenciadorDeSessao } from "../sessao/GerenciadorDeSessao";
import { SessaoProvider } from "../sessao/SessaoProvider";
import { ThemeProvider } from "../theme/ThemeProvider";

export const BASE_DE_TESTE = "http://maxx.test";

export function json(status: number, corpo: unknown): Response {
  return new Response(JSON.stringify(corpo), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

// O caminho de cada pedido que a tela fez à API simulada.
export const caminhos = (api: jest.Mock<Promise<Response>, [Request]>) =>
  api.mock.calls.map(([pedido]) => new URL(pedido.url).pathname);

// Uma tela inteira, com tema, área segura, sessão e cliente da API, sendo `api` quem
// responde no lugar da rede. Sem sessão guardada: a abertura vai direto ao anônimo.
export async function renderComApi(
  ui: ReactElement,
  api: jest.Mock<Promise<Response>, [Request]>,
) {
  const gerenciador = new GerenciadorDeSessao({
    baseUrl: BASE_DE_TESTE,
    web: false,
    armazenamento: {
      ler: async () => null,
      gravar: async () => {},
      apagar: async () => {},
    },
    trava: (renovacao) => renovacao(),
    fetch: api,
  });
  // Sem coleta de cache: o temporizador dela (5 minutos) segura o Jest depois dos testes.
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: Infinity },
      mutations: { gcTime: Infinity },
    },
  });

  const resultado = await render(
    <SafeAreaProvider
      initialMetrics={{
        insets: { top: 0, bottom: 0, left: 0, right: 0 },
        frame: { x: 0, y: 0, width: 390, height: 844 },
      }}
    >
      <QueryClientProvider client={queryClient}>
        <SessaoProvider gerenciador={gerenciador}>
          <ApiClientProvider
            client={createApiClient(BASE_DE_TESTE, {
              fetch: gerenciador.fetch,
            })}
          >
            <ThemeProvider>{ui}</ThemeProvider>
          </ApiClientProvider>
        </SessaoProvider>
      </QueryClientProvider>
    </SafeAreaProvider>,
  );
  await act(async () => {});
  return { ...resultado, gerenciador };
}
