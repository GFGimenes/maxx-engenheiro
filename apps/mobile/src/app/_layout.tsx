import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Slot } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { useState } from "react";

import {
  ApiClientProvider,
  createApiClient,
  defaultBaseUrl,
} from "../api/ApiClientProvider";
import { PhoneFrame } from "../components/PhoneFrame";
import { GerenciadorDeSessao } from "../sessao/GerenciadorDeSessao";
import {
  armazenamentoDaPlataforma,
  ehWeb,
  travaDaPlataforma,
} from "../sessao/plataforma";
import { SessaoProvider } from "../sessao/SessaoProvider";
import { FontGate } from "../theme/FontGate";
import { ThemeProvider, useTheme } from "../theme/ThemeProvider";

// A barra de status segue o tema do app, que pode diferir do tema do aparelho.
function ThemedStatusBar() {
  const { name } = useTheme();
  return <StatusBar style={name === "escuro" ? "light" : "dark"} />;
}

export default function RootLayout() {
  const [queryClient] = useState(() => new QueryClient());
  const [gerenciador] = useState(
    () =>
      new GerenciadorDeSessao({
        baseUrl: defaultBaseUrl(),
        web: ehWeb,
        armazenamento: armazenamentoDaPlataforma,
        trava: travaDaPlataforma,
        fetch: (request) => globalThis.fetch(request),
      }),
  );
  const [apiClient] = useState(() =>
    createApiClient(defaultBaseUrl(), { fetch: gerenciador.fetch }),
  );

  return (
    <QueryClientProvider client={queryClient}>
      <SessaoProvider gerenciador={gerenciador}>
        <ApiClientProvider client={apiClient}>
          <ThemeProvider>
            <PhoneFrame>
              <FontGate>
                <Slot />
              </FontGate>
            </PhoneFrame>
            <ThemedStatusBar />
          </ThemeProvider>
        </ApiClientProvider>
      </SessaoProvider>
    </QueryClientProvider>
  );
}
