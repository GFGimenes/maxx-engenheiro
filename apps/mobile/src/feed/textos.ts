// Os textos do feed, em português e no formato brasileiro (identidade-visual.md, Texto).
// Feitos à mão, sem Intl: o motor do nativo não garante o pt-BR em todo aparelho.

// 15020 vira "15.020".
export function formatarNumero(numero: number): string {
  return String(Math.trunc(numero)).replace(/\B(?=(\d{3})+(?!\d))/g, ".");
}

export function descreverCurtidas(curtidas: number): string | null {
  if (curtidas === 0) return null;
  return curtidas === 1 ? "1 curtida" : `${formatarNumero(curtidas)} curtidas`;
}

export function descreverComentarios(comentarios: number): string | null {
  if (comentarios === 0) return null;
  return comentarios === 1
    ? "Ver 1 comentário"
    : `Ver todos os ${formatarNumero(comentarios)} comentários`;
}

const MESES = [
  "jan.",
  "fev.",
  "mar.",
  "abr.",
  "maio",
  "jun.",
  "jul.",
  "ago.",
  "set.",
  "out.",
  "nov.",
  "dez.",
];

// "agora", "há 5 minutos", "há 2 horas", "há 3 dias"; depois de uma semana, a data.
export function tempoDesde(instante: string, agora: Date = new Date()): string {
  const quando = new Date(instante);
  const segundos = Math.max(0, (agora.getTime() - quando.getTime()) / 1000);
  const plural = (n: number, singular: string, varios: string) =>
    `há ${n} ${n === 1 ? singular : varios}`;

  if (segundos < 60) return "agora";
  if (segundos < 3600)
    return plural(Math.floor(segundos / 60), "minuto", "minutos");
  if (segundos < 86_400)
    return plural(Math.floor(segundos / 3600), "hora", "horas");
  if (segundos < 7 * 86_400)
    return plural(Math.floor(segundos / 86_400), "dia", "dias");

  const data = `${quando.getDate()} de ${MESES[quando.getMonth()]}`;
  return quando.getFullYear() === agora.getFullYear()
    ? data
    : `${data} de ${quando.getFullYear()}`;
}

// O nome de usuário do post, como no Instagram: o arroba sem "@" e sem o sufixo, que o curso
// ao lado já diz.
export function usuarioDoArroba(arroba: string): string {
  return arroba.replace(/^@/, "").replace(/-[^-]*$/, "");
}

// "CC · 3º semestre · #29": a posição é a do histórico geral (passo 11), quando há.
export function descreverAutor(autor: {
  curso?: string | null;
  semestre?: number | null;
  posicao?: number | null;
}): string {
  return [
    autor.curso,
    autor.semestre ? `${autor.semestre}º semestre` : null,
    autor.posicao ? `#${formatarNumero(autor.posicao)}` : null,
  ]
    .filter(Boolean)
    .join(" · ");
}

// Sob os pontos da faixa da medalha: "Banco de Dados · 2º bimestre".
export function descreverMedalha(premio: {
  materia?: string | null;
  bimestre?: number | null;
}): string | undefined {
  const texto = [
    premio.materia,
    premio.bimestre ? `${premio.bimestre}º bimestre` : null,
  ]
    .filter(Boolean)
    .join(" · ");
  return texto === "" ? undefined : texto;
}
