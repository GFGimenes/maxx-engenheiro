import type { ApiClient } from "@maxx/api-client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, screen } from "@testing-library/react-native";

import { ApiClientProvider } from "../api/ApiClientProvider";
import { renderWithTheme } from "../test/renderWithTheme";
import { ApiStatusScreen } from "./ApiStatusScreen";

function renderWith(get: jest.Mock) {
  const client = { GET: get } as unknown as ApiClient;
  // gcTime infinito: sem o timer de coleta, o Jest termina sem processo pendurado.
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity } },
  });
  return renderWithTheme(
    <QueryClientProvider client={queryClient}>
      <ApiClientProvider client={client}>
        <ApiStatusScreen />
      </ApiClientProvider>
    </QueryClientProvider>,
  );
}

const ok = { data: { status: "ok" }, response: { ok: true, status: 200 } };
const unavailable = { data: undefined, response: { ok: false, status: 503 } };

test("mostra que a API respondeu 200", async () => {
  const get = jest.fn().mockResolvedValue(ok);
  await renderWith(get);

  expect(await screen.findByText("A API respondeu 200.")).toBeTruthy();
  expect(get).toHaveBeenCalledWith("/api/health");
});

test("com a API fora, mostra mensagem amigável e deixa tentar de novo", async () => {
  const get = jest
    .fn()
    .mockResolvedValueOnce(unavailable)
    .mockResolvedValueOnce(ok);
  await renderWith(get);

  expect(
    await screen.findByText("Não conseguimos falar com o servidor agora."),
  ).toBeTruthy();

  fireEvent.press(screen.getByRole("button", { name: "Tentar de novo" }));

  expect(await screen.findByText("A API respondeu 200.")).toBeTruthy();
  expect(get).toHaveBeenCalledTimes(2);
});

test("sem rede, a mensagem também é amigável", async () => {
  const get = jest.fn().mockRejectedValue(new TypeError("Failed to fetch"));
  await renderWith(get);

  expect(
    await screen.findByText("Não conseguimos falar com o servidor agora."),
  ).toBeTruthy();
  expect(screen.queryByText(/Failed to fetch/)).toBeNull();
});
