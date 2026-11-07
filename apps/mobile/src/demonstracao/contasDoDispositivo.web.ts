import AsyncStorage from "@react-native-async-storage/async-storage";

import { analisarContas, type ContaDeDemonstracao } from "./contas";

const CHAVE = "maxx.demo.contas";

// Web: no armazenamento local do navegador (produto.md 11, lembrança local). A chave só
// abre a conta de demonstração, e se perde ao trocar de aparelho ou limpar o navegador.
export async function lerContas(): Promise<ContaDeDemonstracao[]> {
  return analisarContas(await AsyncStorage.getItem(CHAVE));
}

export async function gravarContas(
  contas: ContaDeDemonstracao[],
): Promise<void> {
  await AsyncStorage.setItem(CHAVE, JSON.stringify(contas));
}
