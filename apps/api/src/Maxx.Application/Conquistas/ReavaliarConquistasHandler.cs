using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Application.Notificacoes;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Notificacoes;

namespace Maxx.Application.Conquistas;

// No worker, quando um fato fica contável (regras-conquista.md 5): avalia as conquistas que
// esse fato pode mexer, na instância de período dele. Grava o progresso de todas, inclusive
// as de fechamento, e concede as de gatilho por fato que alcançaram o alvo, com as evidências
// e a notificação. A de fechamento espera o fechamento do período.
//
// Idempotente: rodar de novo regrava o mesmo progresso e não concede o que já foi concedido.
// Fato que deixou de ser contável antes do trabalho (medalha anulada, conta purgada) não
// muda nada aqui.
public sealed class ReavaliarConquistasHandler(
    IConquistaRepository conquistas,
    INotificacaoRepository notificacoes,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    AvaliadorDeConquista avaliador,
    ContextoDasConquistas contextos,
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
        var concedeu = false;

        foreach (var (item, versao) in await conquistas.CatalogoAsync(cancellationToken))
        {
            if (!AvaliadorDeConquista.Conta(item.Regra, fato) || fato.InstanciaEm(item.Periodo, aluno.Id) is not { } instancia)
            {
                continue;
            }

            var contexto = await contextos.MontarAsync(item, instancia, aluno, calendario, agora, cancellationToken);
            var avaliacao = avaliador.Avaliar(item, instancia, fatos, contexto);
            if (!avaliacao.Aplicavel)
            {
                continue;
            }

            await conquistas.GravarProgressoAsync(aluno.Id, item.Codigo, instancia, avaliacao, agora, cancellationToken);

            var jaTem = ativas.Any(conquista => conquista.ItemCodigo == item.Codigo && conquista.Instancia == instancia);
            if (item.Gatilho != Gatilho.PorFato || !avaliacao.Satisfeita || jaTem)
            {
                continue;
            }

            var conquista = Conquista.Conceder(item, versao, aluno.Id, instancia, avaliacao, agora);
            conquistas.Adicionar(conquista);
            notificacoes.Adicionar(Notificacao.ConquistaObtida(conquista, agora));
            concedeu = true;
        }

        // Conquista concedida muda o numerador da raridade (ADR 0005). O ranking entra no
        // passo 11.
        if (concedeu)
        {
            fila.Enfileirar(new RecalcularRaridade());
        }

        await unitOfWork.SalvarAsync(cancellationToken);
    }
}
