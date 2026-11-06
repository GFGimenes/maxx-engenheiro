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
import { FontGate } from "../theme/FontGate";
import { ThemeProvider, useTheme } from "../theme/ThemeProvider";

// A barra de status segue o tema do app, que pode diferir do tema do aparelho.
function ThemedStatusBar() {
  const { name } = useTheme();
  return <StatusBar style={name === "escuro" ? "light" : "dark"} />;
}

export default function RootLayout() {
  const [queryClient] = useState(() => new QueryClient());
  const [apiClient] = useState(() => createApiClient(defaultBaseUrl()));

  return (
    <QueryClientProvider client={queryClient}>
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
    </QueryClientProvider>
  );
}
