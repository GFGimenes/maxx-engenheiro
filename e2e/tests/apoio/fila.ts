import { sql } from "./contas";

// O seed demo do passo 12 deixa na fila do Postgres a atividade do mundo inteiro: a
// reavaliação das conquistas, os fechamentos de período, os avisos e os recálculos, cerca de
// 1.500 trabalhos. O worker consome um por vez, e os testes que esperam o próprio trabalho
// (a medalha que vira notificação, a conquista, o ranking) ficariam atrás dessa fila. Antes de
// qualquer teste, espera não haver trabalho pendente já disponível; os agendados para depois
// (o fim da janela de contestação, a verificação de fechamentos de hora em hora) não contam.
export default async function esperarAFila(): Promise<void> {
  const limite = Date.now() + 10 * 60_000;
  const inicio = Date.now();
  for (;;) {
    const pendentes = Number(
      sql(
        "SELECT count(*) FROM trabalho WHERE estado = 'pendente' AND disponivel_em <= now();",
      ),
    );
    if (pendentes === 0) {
      console.log(
        `Fila do worker vazia em ${Math.round((Date.now() - inicio) / 1000)} s.`,
      );
      return;
    }

    if (Date.now() > limite) {
      throw new Error(
        `A fila do worker não esvaziou em 10 min: ${pendentes} trabalhos pendentes.`,
      );
    }

    await new Promise((resolver) => setTimeout(resolver, 2_000));
  }
}
