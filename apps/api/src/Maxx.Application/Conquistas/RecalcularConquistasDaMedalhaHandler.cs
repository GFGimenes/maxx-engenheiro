using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Application.Notificacoes;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Notificacoes;

namespace Maxx.Application.Conquistas;

// A medalha foi anulada (produto.md 4.8): enfileirado pela anulação, na mesma transação.
public sealed record RecalcularConquistasDaMedalha(Guid MedalhaId) : ICargaDeTrabalho
{
    public static string Tipo => "recalcular-conquistas-da-medalha";
}

// No worker, depois da anulação (regras-conquista.md 6): reavalia só as conquistas que têm a
// medalha nas evidências, sem limite de data, porque a anulação corrige um erro (D9). A que
// deixou de ser satisfeita é removida, os pontos saem e o aluno é notificado; a que continua
// satisfeita fica, com as evidências trocadas pelo conjunto atual (D4). O progresso das
// instâncias que a medalha tocava é refeito sem ela.
//
// Anulação nunca concede: tirar um fato não fecha critério nenhum. A conquista que o item do
// catálogo deixou de alcançar (outro curso, fora da vigência) fica como está: o que a
// remove é o fato, não uma mudança de catálogo.
//
// Idempotente: rodar de novo não acha mais a medalha nas evidências de conquista ativa.
public sealed class RecalcularConquistasDaMedalhaHandler(
    IMedalhaRepository medalhas,
    IConquistaRepository conquistas,
    INotificacaoRepository notificacoes,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    AvaliadorDeConquista avaliador,
    ContextoDasConquistas contextos,
    TimeProvider relogio) : ITrabalhoHandler<RecalcularConquistasDaMedalha>
{
    public async Task ExecutarAsync(RecalcularConquistasDaMedalha carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);

        // A purga já levou, ou a medalha voltou a valer: nada a recalcular.
        var medalha = await medalhas.BuscarAsync(carga.MedalhaId, cancellationToken);
        if (medalha is null || medalha.EstaAtiva)
        {
            return;
        }

        await conquistas.TravarAlunoAsync(medalha.AlunoId, cancellationToken);
        var aluno = await conquistas.AlunoAsync(medalha.AlunoId, cancellationToken);
        if (aluno is null)
        {
            return;
        }

        var calendario = await conquistas.CalendarioAsync(cancellationToken);
        var fatos = await conquistas.FatosAsync(aluno.Id, calendario, cancellationToken);
        var catalogo = (await conquistas.CatalogoAsync(cancellationToken)).ToDictionary(conquista => conquista.Item.Codigo);
        var ativas = await conquistas.AtivasAsync(aluno.Id, cancellationToken);
        var anulada = FatoContavel.DescreverMedalha(medalha, calendario);
        var agora = relogio.GetUtcNow();

        // O progresso das instâncias que a medalha tocava, agora sem ela.
        foreach (var (item, _) in catalogo.Values)
        {
            if (!AvaliadorDeConquista.Conta(item.Regra, anulada) || anulada.InstanciaEm(item.Periodo, aluno.Id) is not { } instancia)
            {
                continue;
            }

            var avaliacao = avaliador.Avaliar(
                item, instancia, fatos, await contextos.MontarAsync(item, instancia, aluno, calendario, agora, cancellationToken));
            if (avaliacao.Aplicavel)
            {
                await conquistas.GravarProgressoAsync(aluno.Id, item.Codigo, instancia, avaliacao, agora, cancellationToken);
            }
        }

        // As conquistas que a medalha compunha, uma a uma.
        var removeu = false;
        foreach (var conquista in ativas.Where(conquista => conquista.Evidencias.Any(evidencia => evidencia.MedalhaId == medalha.Id)))
        {
            if (!catalogo.TryGetValue(conquista.ItemCodigo, out var doCatalogo))
            {
                continue;
            }

            var item = doCatalogo.Item;
            var instancia = conquista.Instancia;
            var avaliacao = avaliador.Avaliar(
                item, instancia, fatos, await contextos.MontarAsync(item, instancia, aluno, calendario, agora, cancellationToken));
            if (!avaliacao.Aplicavel)
            {
                continue;
            }

            if (conquista.Recalcular(avaliacao, agora))
            {
                notificacoes.Adicionar(Notificacao.ConquistaRemovida(conquista, agora));
                removeu = true;
            }
        }

        // Conquista removida muda o numerador da raridade (ADR 0005). O ranking entra no
        // passo 11.
        if (removeu)
        {
            fila.Enfileirar(new RecalcularRaridade());
        }

        await unitOfWork.SalvarAsync(cancellationToken);
    }
}
