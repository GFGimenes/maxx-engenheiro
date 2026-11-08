// Textos e datas do prêmio de participação. As datas vão para a API em ISO (aaaa-mm-dd) e
// aparecem para o aluno em dd/mm, como no resto do app.

// "dd/mm/aaaa", com ou sem zero à esquerda, numa data que existe; nulo se não for.
export function lerData(texto: string): string | null {
  const partes = /^\s*(\d{1,2})\/(\d{1,2})\/(\d{4})\s*$/.exec(texto);
  if (!partes) return null;
  const [dia, mes, ano] = [
    Number(partes[1]),
    Number(partes[2]),
    Number(partes[3]),
  ];
  const data = new Date(Date.UTC(ano, mes - 1, dia));
  if (
    data.getUTCFullYear() !== ano ||
    data.getUTCMonth() !== mes - 1 ||
    data.getUTCDate() !== dia
  ) {
    return null;
  }
  return `${ano}-${String(mes).padStart(2, "0")}-${String(dia).padStart(2, "0")}`;
}

// "14/09" de "2026-09-14".
export function diaEMes(iso: string): string {
  const [, mes, dia] = iso.slice(0, 10).split("-");
  return `${dia}/${mes}`;
}

// "14/09" para um dia só, "14/09 a 18/09" para um período.
export function periodo(inicio: string, fim: string): string {
  return inicio === fim
    ? diaEMes(inicio)
    : `${diaEMes(inicio)} a ${diaEMes(fim)}`;
}

// O rótulo do estado de uma reivindicação que ainda não pontua.
export const rotulosDeEstado: Record<string, string> = {
  reivindicado: "Pendente",
  emAveriguacao: "Em averiguação",
  reprovado: "Reprovada",
  revogado: "Revogada",
};

// Sob o nome na faixa do post: o evento e, na apresentação em sala, a matéria; na medalha,
// a matéria e o bimestre.
export function descreverPremio(premio: {
  evento?: string | null;
  materia?: string | null;
  bimestre?: number | null;
}): string | undefined {
  const texto = [
    premio.evento,
    premio.materia,
    premio.bimestre ? `${premio.bimestre}º bimestre` : null,
  ]
    .filter(Boolean)
    .join(" · ");
  return texto === "" ? undefined : texto;
}
