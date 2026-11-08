import { Platform, Share } from "react-native";

import { defaultBaseUrl } from "../api/ApiClientProvider";

// O link público do post (produto.md 6.1): /post/:id na origem do app (ADR 0001). Quem não
// tem conta cai no login e, depois de entrar, chega ao post.
export function linkDoPost(postId: string): string {
  const origem =
    Platform.OS === "web" ? globalThis.location.origin : defaultBaseUrl();
  return `${origem}/post/${postId}`;
}

export type ResultadoDoEnvio = "compartilhado" | "copiado" | "cancelado";

// "Enviar" abre a folha de compartilhamento do sistema. Na web sem folha (navegador de
// desktop), o link vai para a área de transferência, e a tela avisa.
export async function enviarPost(postId: string): Promise<ResultadoDoEnvio> {
  const link = linkDoPost(postId);

  if (Platform.OS === "web") {
    const navegador = globalThis.navigator;
    if (typeof navegador.share === "function") {
      try {
        await navegador.share({ url: link });
        return "compartilhado";
      } catch (erro) {
        if (erro instanceof Error && erro.name === "AbortError") {
          return "cancelado";
        }
      }
    }
    await navegador.clipboard.writeText(link);
    return "copiado";
  }

  const resultado = await Share.share({ message: link, url: link });
  return resultado.action === Share.dismissedAction
    ? "cancelado"
    : "compartilhado";
}
