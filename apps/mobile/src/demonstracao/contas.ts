// Os três papéis da demonstração (passo 14). Coordenação é o rótulo da conta com o papel de
// instituição.
export type PapelDeDemonstracao = "aluno" | "professor" | "coordenacao";

const PAPEIS: readonly string[] = ["aluno", "professor", "coordenacao"];

// Uma conta de demonstração lembrada neste aparelho: a chave que abre a conta sem senha,
// e o que mostrar na lista para a pessoa reconhecer a sua. Curso vazio na coordenação.
export interface ContaDeDemonstracao {
  chave: string;
  nome: string;
  curso: string;
  papel: PapelDeDemonstracao;
  // O id da conta, lido logo depois de entrar: é por ele que a coordenação que purga o
  // próprio lote esquece a conta neste aparelho (passo 14, P2).
  usuarioId?: string;
}

// O que estiver guardado e não tiver a forma esperada é descartado: lista vazia, nunca
// uma tela quebrada. A conta guardada antes do passo 14 não tem papel, e é de aluno.
export function analisarContas(guardado: string | null): ContaDeDemonstracao[] {
  if (!guardado) {
    return [];
  }

  try {
    const valor: unknown = JSON.parse(guardado);
    return Array.isArray(valor)
      ? valor
          .filter(
            (
              conta,
            ): conta is Omit<ContaDeDemonstracao, "papel"> & {
              papel?: string;
            } =>
              typeof conta === "object" &&
              conta !== null &&
              typeof conta.chave === "string" &&
              typeof conta.nome === "string" &&
              typeof conta.curso === "string" &&
              (conta.papel === undefined || PAPEIS.includes(conta.papel)),
          )
          .map((conta) => ({
            chave: conta.chave,
            nome: conta.nome,
            curso: conta.curso,
            papel: (conta.papel ?? "aluno") as PapelDeDemonstracao,
            ...(typeof conta.usuarioId === "string"
              ? { usuarioId: conta.usuarioId }
              : {}),
          }))
      : [];
  } catch {
    return [];
  }
}

// A lista sem a conta de um usuário: a que a purga encerrou.
export function semAConta(
  contas: ContaDeDemonstracao[],
  usuarioId: string,
): ContaDeDemonstracao[] {
  return contas.filter((conta) => conta.usuarioId !== usuarioId);
}

// O botão da lista "Neste aparelho".
export function rotuloDaConta(conta: ContaDeDemonstracao): string {
  switch (conta.papel) {
    case "professor":
      return `Entrar como ${conta.nome} · Professor de ${conta.curso}`;
    case "coordenacao":
      return `Entrar como ${conta.nome} · Coordenação`;
    default:
      return `Entrar como ${conta.nome} · ${conta.curso}`;
  }
}
