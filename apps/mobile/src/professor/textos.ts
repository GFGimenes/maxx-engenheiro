// Os textos curtos das telas do professor, em português do Brasil e sem emoji.

export function plural(n: number, singular: string, varios: string) {
  return `${n.toLocaleString("pt-BR")} ${n === 1 ? singular : varios}`;
}

export function descreverTurma(turma: {
  curso: string;
  semestre: string;
  alunos: number;
}) {
  return `${turma.curso} · ${turma.semestre} · ${plural(turma.alunos, "aluno", "alunos")}`;
}

const PERIODOS: Record<string, [string, string]> = {
  bimestre: ["no bimestre", "por bimestre"],
  semestre: ["no semestre", "por semestre"],
};

// "Uma vez no bimestre" ou "Até 2 por bimestre", por matéria.
export function descreverLimite(limite: number, periodo: string) {
  const [uma, varias] = PERIODOS[periodo] ?? ["no período", "por período"];
  return limite === 1
    ? `Uma vez ${uma}, por matéria`
    : `Até ${limite} ${varias}, por matéria`;
}
