using Maxx.Application.Notificacoes;
using Maxx.Domain.Academico;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Notificacoes;

namespace Maxx.Application.Conquistas;

// Avalia uma conquista numa instância, grava o progresso e, se quem chama permite, concede
// com as evidências e a notificação. A mesma rotina serve ao fato que fica contável e ao
// fechamento do período; quem chama decide quando a concessão é permitida.
public sealed class AvaliacaoDeConquistas(
    IConquistaRepository conquistas,
    INotificacaoRepository notificacoes,
    AvaliadorDeConquista avaliador,
    ContextoDasConquistas contextos)
{
    // true: concedeu agora. Item que não se aplica ao aluno não grava progresso.
    public async Task<bool> AvaliarAsync(
        ConquistaDoCatalogo conquista,
        InstanciaDePeriodo instancia,
        AlunoDasConquistas aluno,
        Calendario calendario,
        IReadOnlyList<FatoContavel> fatos,
        IReadOnlyList<Conquista> ativas,
        bool podeConceder,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conquista);
        ArgumentNullException.ThrowIfNull(ativas);

        var (item, versao) = conquista;
        var contexto = await contextos.MontarAsync(item, instancia, aluno, calendario, agora, cancellationToken);
        var avaliacao = avaliador.Avaliar(item, instancia, fatos, contexto);
        if (!avaliacao.Aplicavel)
        {
            return false;
        }

        await conquistas.GravarProgressoAsync(aluno.Id, item.Codigo, instancia, avaliacao, agora, cancellationToken);

        var jaTem = ativas.Any(ativa => ativa.ItemCodigo == item.Codigo && ativa.Instancia == instancia);
        if (!podeConceder || !avaliacao.Satisfeita || jaTem)
        {
            return false;
        }

        var concedida = Conquista.Conceder(item, versao, aluno.Id, instancia, avaliacao, agora);
        conquistas.Adicionar(concedida);
        notificacoes.Adicionar(Notificacao.ConquistaObtida(concedida, agora));
        return true;
    }
}
