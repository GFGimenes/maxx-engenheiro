import * as SecureStore from "expo-secure-store";

import { analisarContas, type ContaDeDemonstracao } from "./contas";

const CHAVE = "maxx.demo.contas";

// Nativo: as contas de demonstração deste aparelho no secure store, porque a chave abre a
// conta sem senha.
export async function lerContas(): Promise<ContaDeDemonstracao[]> {
  return analisarContas(await SecureStore.getItemAsync(CHAVE));
}

export async function gravarContas(
  contas: ContaDeDemonstracao[],
): Promise<void> {
  await SecureStore.setItemAsync(CHAVE, JSON.stringify(contas));
}
