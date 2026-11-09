using Maxx.Application.Fila;
using Maxx.Application.Medalhas;

namespace Maxx.Application.Ranking;

public static class PontuacaoMudou
{
    // Todo evento que muda a pontuação de algum aluno pede, na mesma transação, o recálculo
    // do ranking e o da raridade (ADR 0005): concessão e anulação de medalha, aprovação de
    // comprovação, contestação, decisão da instituição, remoção do post pela instituição
    // quando revoga, e conquista concedida ou removida. O aluno novo muda só o divisor da
    // raridade e pede só ela.
    public static void PedirRecalculos(this IFilaDeTrabalhos fila)
    {
        ArgumentNullException.ThrowIfNull(fila);

        fila.Enfileirar(new RecalcularRanking());
        fila.Enfileirar(new RecalcularRaridade());
    }
}
