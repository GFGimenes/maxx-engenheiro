using Maxx.Application.Fila;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;

namespace Maxx.Application.Conquistas;

// No worker, quando um fato fica contável (regras-conquista.md 5): avalia as conquistas que
// esse fato pode mexer, na instância de período dele. Grava o progresso de todas e concede as
// de gatilho por fato que alcançaram o alvo, com as evidências e a notificação.
//
// A de fechamento espera o fechamento do período; com a instância já fechada, o fato que
// chega antes do congelamento a concede (D8 do passo 9). É o que cobre o prêmio que fica
// definitivo já na virada: a conquista pontua no período a que se refere.
//
// Idempotente: rodar de novo regrava o mesmo progresso e não concede o que já foi concedido.
// Fato que deixou de ser contável antes do trabalho (medalha anulada, conta purgada) não
// muda nada aqui.
public sealed class ReavaliarConquistasHandler(
    IConquistaRepository conquistas,
    AvaliacaoDeConquistas avaliacao,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio) : ITrabalhoHandler<ReavaliarConquistas>
{
    public async Task ExecutarAsync(ReavaliarConquistas carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);

        await conquistas.TravarAlunoAsync(carga.AlunoId, cancellationToken);
        var aluno = await conquistas.AlunoAsync(carga.AlunoId, cancellationToken);
        if (aluno is null)
        {
            return;
        }

        var calendario = await conquistas.CalendarioAsync(cancellationToken);
        var fatos = await conquistas.FatosAsync(aluno.Id, calendario, cancellationToken);
        if (fatos.FirstOrDefault(carga.Aponta) is not { } fato)
        {
            return;
        }

        var ativas = await conquistas.AtivasAsync(aluno.Id, cancellationToken);
        var agora = relogio.GetUtcNow();
        var hoje = FusoHorario.DataLocal(agora);
        var concedeu = false;

        foreach (var conquista in await conquistas.CatalogoAsync(cancellationToken))
        {
            var item = conquista.Item;
            if (!AvaliadorDeConquista.Conta(item.Regra, fato) || fato.InstanciaEm(item.Periodo, aluno.Id) is not { } instancia)
            {
                continue;
            }

            var podeConceder = item.Gatilho == Gatilho.PorFato
                || (instancia.AceitaReavaliacaoDeFechamento(calendario, hoje) && await conquistas.FechadaAsync(instancia, cancellationToken));
            concedeu |= await avaliacao.AvaliarAsync(
                conquista, instancia, aluno, calendario, fatos, ativas, podeConceder, agora, cancellationToken);
        }

        // Conquista concedida muda a pontuação e o numerador da raridade (ADR 0005).
        if (concedeu)
        {
            fila.PedirRecalculos();
        }

        await unitOfWork.SalvarAsync(cancellationToken);
    }
}
