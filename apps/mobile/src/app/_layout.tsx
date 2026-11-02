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

export default function RootLayout() {
  const [queryClient] = useState(() => new QueryClient());
  const [apiClient] = useState(() => createApiClient(defaultBaseUrl()));

  return (
    <QueryClientProvider client={queryClient}>
      <ApiClientProvider client={apiClient}>
        <PhoneFrame>
          <Slot />
        </PhoneFrame>
        <StatusBar style="auto" />
      </ApiClientProvider>
    </QueryClientProvider>
  );
}
