import * as SecureStore from "expo-secure-store";

import type {
  ArmazenamentoDoRefresh,
  TravaDoRefresh,
} from "./GerenciadorDeSessao";

const CHAVE = "maxx.refresh";

// Nativo: o refresh no secure store (Keychain no iOS, Keystore no Android).
export const armazenamentoDaPlataforma: ArmazenamentoDoRefresh = {
  ler: () => SecureStore.getItemAsync(CHAVE),
  gravar: (refresh) => SecureStore.setItemAsync(CHAVE, refresh),
  apagar: () => SecureStore.deleteItemAsync(CHAVE),
};

// Um processo só: a fila do gerenciador já serializa.
export const travaDaPlataforma: TravaDoRefresh = (renovacao) => renovacao();

export const ehWeb = false;
