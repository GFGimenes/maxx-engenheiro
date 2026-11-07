import type {
  ArmazenamentoDoRefresh,
  TravaDoRefresh,
} from "./GerenciadorDeSessao";

// Web: o refresh mora no cookie HttpOnly, fora do alcance do JavaScript. Nada a guardar.
export const armazenamentoDaPlataforma: ArmazenamentoDoRefresh = {
  ler: async () => null,
  gravar: async () => {},
  apagar: async () => {},
};

// Cada aba tem a própria memória, mas todas mandam o mesmo cookie: duas abas abrindo
// juntas renovariam com o mesmo refresh, e isso conta como reuso. A Web Locks API põe as
// abas em fila, e a segunda já manda o cookie que a primeira recebeu. Sem ela (contexto
// sem HTTPS), fica só a fila da aba.
export const travaDaPlataforma: TravaDoRefresh = (renovacao) => {
  const locks = globalThis.navigator?.locks;
  return locks ? locks.request("maxx-refresh", renovacao) : renovacao();
};

export const ehWeb = true;
