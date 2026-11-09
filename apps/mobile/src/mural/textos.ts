// O que o mural escreve a partir do aviso (prancheta mural-*.png).

// A data do aviso como na prancheta, "24/07", no fuso de São Paulo (decisoes/README.md,
// Tempo). São Paulo está em UTC-3 o ano todo desde 2019: a conta fixa evita depender do
// suporte a fuso horário do Intl em cada plataforma.
export function dataDoAviso(instante: string): string {
  const local = new Date(new Date(instante).getTime() - 3 * 3600_000);
  const dia = String(local.getUTCDate()).padStart(2, "0");
  const mes = String(local.getUTCMonth() + 1).padStart(2, "0");
  return `${dia}/${mes}`;
}

// O rótulo em caixa alta acima do título: "FACULDADE", "CURSO · CC" ou as matérias das
// turmas do professor, "BANCO DE DADOS · ALGORITMOS". A caixa alta é do estilo.
export function rotuloDoEscopo(aviso: {
  escopo: string;
  cursos: string[];
  materias: string[];
}): string {
  switch (aviso.escopo) {
    case "faculdade":
      return "Faculdade";
    case "curso":
      return ["Curso", ...aviso.cursos].join(" · ");
    default:
      return aviso.materias.length > 0 ? aviso.materias.join(" · ") : "Turma";
  }
}

// Quem assina: a instituição aparece como "UniMax", como na prancheta; o professor, pelo
// nome.
export function autorDoAviso(autor: {
  nomeDeExibicao: string;
  instituicao: boolean;
}): string {
  return autor.instituicao ? "UniMax" : autor.nomeDeExibicao;
}
