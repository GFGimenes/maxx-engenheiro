// Uma conta de demonstração lembrada neste aparelho: a chave que abre a conta sem senha,
// e o que mostrar na lista para a pessoa reconhecer a sua.
export interface ContaDeDemonstracao {
  chave: string;
  nome: string;
  curso: string;
}

// O que estiver guardado e não tiver a forma esperada é descartado: lista vazia, nunca
// uma tela quebrada.
export function analisarContas(guardado: string | null): ContaDeDemonstracao[] {
  if (!guardado) {
    return [];
  }

  try {
    const valor: unknown = JSON.parse(guardado);
    return Array.isArray(valor)
      ? valor.filter(
          (conta): conta is ContaDeDemonstracao =>
            typeof conta === "object" &&
            conta !== null &&
            typeof conta.chave === "string" &&
            typeof conta.nome === "string" &&
            typeof conta.curso === "string",
        )
      : [];
  } catch {
    return [];
  }
}
