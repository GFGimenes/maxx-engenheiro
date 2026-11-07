import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen } from "@testing-library/react-native";
import { Text } from "react-native";

import {
  GerenciadorDeSessao,
  type SessaoResponse,
} from "./GerenciadorDeSessao";
import { SessaoProvider, useSessao } from "./SessaoProvider";

const sessao: SessaoResponse = {
  accessToken: "a1",
  accessTokenExpiraEm: "2999-01-01T00:00:00Z",
  refreshToken: "r1",
  refreshTokenExpiraEm: "2999-01-01T00:00:00Z",
};

function Estado() {
  const { estado } = useSessao();
  return <Text testID="estado">{estado}</Text>;
}

function montar(refreshGuardado: string | null, fetch: jest.Mock) {
  let valor = refreshGuardado;
  const gerenciador = new GerenciadorDeSessao({
    baseUrl: "http://maxx.test",
    web: false,
    armazenamento: {
      ler: async () => valor,
      gravar: async (novo) => {
        valor = novo;
      },
      apagar: async () => {
        valor = null;
      },
    },
    trava: (renovacao) => renovacao(),
    fetch,
  });
  const queryClient = new QueryClient();
  return { gerenciador, queryClient };
}

async function renderizar(
  gerenciador: GerenciadorDeSessao,
  queryClient: QueryClient,
) {
  await render(
    <QueryClientProvider client={queryClient}>
      <SessaoProvider gerenciador={gerenciador}>
        <Estado />
      </SessaoProvider>
    </QueryClientProvider>,
  );
  await act(async () => {});
}

describe("SessaoProvider", () => {
  it("restaura a sessão guardada ao abrir", async () => {
    const fetch = jest.fn(
      async () => new Response(JSON.stringify(sessao), { status: 200 }),
    );
    const { gerenciador, queryClient } = montar("r0", fetch);

    await renderizar(gerenciador, queryClient);

    expect(screen.getByTestId("estado")).toHaveTextContent("autenticado");
  });

  it("sem rede ao abrir fica sem conexão, e tentar de novo entra", async () => {
    const fetch = jest.fn(async (): Promise<Response> => {
      throw new TypeError("Network request failed");
    });
    const { gerenciador, queryClient } = montar("r0", fetch);

    await renderizar(gerenciador, queryClient);
    expect(screen.getByTestId("estado")).toHaveTextContent("sem-conexao");

    fetch.mockImplementation(
      async () => new Response(JSON.stringify(sessao), { status: 200 }),
    );
    await act(() => gerenciador.restaurar());
    expect(screen.getByTestId("estado")).toHaveTextContent("autenticado");
  });

  it("ao sair, o cache de consultas da conta é esvaziado", async () => {
    const fetch = jest.fn(
      async () => new Response(JSON.stringify(sessao), { status: 200 }),
    );
    const { gerenciador, queryClient } = montar("r0", fetch);
    await renderizar(gerenciador, queryClient);
    queryClient.setQueryData(["eu"], { arroba: "@anaSouza-CC" });

    fetch.mockImplementation(async () => new Response(null, { status: 204 }));
    await act(() => gerenciador.sair());

    expect(screen.getByTestId("estado")).toHaveTextContent("anonimo");
    expect(queryClient.getQueryData(["eu"])).toBeUndefined();
  });
});
