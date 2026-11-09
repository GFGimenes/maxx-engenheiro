import type { AlunoDaBase } from "./api";

// A linha de baixo do aluno na busca e na ficha: curso, semestre, RA e a situação.
export function descreverAluno(aluno: AlunoDaBase): string {
  return [
    aluno.curso,
    `${aluno.semestre}º semestre`,
    `RA ${aluno.ra}`,
    situacao(aluno),
  ]
    .filter(Boolean)
    .join(" · ");
}

export function situacao(aluno: AlunoDaBase): string {
  if (aluno.demonstracao) return "Demonstração";
  if (!aluno.conta) return "Sem conta";
  if (aluno.conta.exAlunoDesde) return "Ex-aluno";
  return "Ativo";
}

// dd/mm/aaaa em São Paulo, sem depender do Intl (UTC-3 fixo, como o mural).
export function dataCurta(iso: string): string {
  const local = new Date(new Date(iso).getTime() - 3 * 60 * 60 * 1000);
  const dois = (n: number) => String(n).padStart(2, "0");
  return `${dois(local.getUTCDate())}/${dois(local.getUTCMonth() + 1)}/${local.getUTCFullYear()}`;
}
