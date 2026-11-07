import { Platform } from "react-native";

// O token do link de recuperação vem depois do "#" (decisoes/README.md, Link de
// recuperação): o navegador não o manda a servidor nenhum. Lido e apagado da barra de
// endereço e do histórico, para não ficar à vista nem voltar com o "voltar". No nativo
// não há fragmento: o link do e-mail abre no navegador.
export function capturarTokenDoFragmento(): string | null {
  if (Platform.OS !== "web" || typeof window === "undefined") {
    return null;
  }

  const { hash, pathname, search } = window.location;
  if (!pathname.endsWith("/redefinir-senha") || !hash) {
    return null;
  }

  const token = new URLSearchParams(hash.slice(1)).get("token");
  window.history.replaceState(window.history.state, "", pathname + search);
  return token && token.trim() !== "" ? token : null;
}

// Capturado uma vez, quando o bundle carrega: antes de o roteador mexer na URL e antes de
// qualquer montagem da tela, que pode acontecer mais de uma vez.
const tokenDaAbertura = capturarTokenDoFragmento();

export function tokenDoLinkDeRecuperacao(): string | null {
  return tokenDaAbertura;
}
