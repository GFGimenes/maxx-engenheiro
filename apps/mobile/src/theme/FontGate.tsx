import { useFonts } from "expo-font";
import type { ReactNode } from "react";

import { fontAssets } from "./fonts";

// Segura a primeira pintura até as fontes carregarem, para o texto não trocar de forma na
// frente do usuário. Se a carga falhar, segue com a fonte do sistema: o app nunca trava.
export function FontGate({ children }: { children: ReactNode }) {
  const [loaded, error] = useFonts(fontAssets);

  if (!loaded && !error) return null;

  return children;
}
