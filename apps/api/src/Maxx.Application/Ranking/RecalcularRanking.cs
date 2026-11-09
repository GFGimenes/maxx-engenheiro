using Maxx.Application.Fila;

namespace Maxx.Application.Ranking;

// O pedido de recálculo das quatro visões do ranking (ADR 0005). Enfileirado, na mesma
// transação, por todo evento que muda a pontuação de algum aluno. Sem carga: o recálculo lê
// o estado de agora, e uma rajada de pedidos vira uma execução só.
public sealed record RecalcularRanking : ICargaDeTrabalho
{
    public static string Tipo => "recalcular-ranking";
}
