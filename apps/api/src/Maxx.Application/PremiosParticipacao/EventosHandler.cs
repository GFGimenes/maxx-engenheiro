using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Premios;

namespace Maxx.Application.PremiosParticipacao;

// Os eventos semeados que o aluno pode escolher ao reivindicar (produto.md 4.6): abertos ao
// curso dele, reivindicáveis hoje e, com o item informado, que aceitam o item. A regra de
// quando um evento é reivindicável é do domínio (Evento.RecusaParaReivindicarEm). Quando o
// evento não está na lista, o aluno o digita na reivindicação.
public sealed class EventosHandler(IEventoRepository eventos, TimeProvider relogio)
{
    public async Task<IReadOnlyList<Evento>> ReivindicaveisAsync(
        Guid alunoId, string? itemCodigo, CancellationToken cancellationToken)
    {
        ItemPremioParticipacao? item = null;
        if (!string.IsNullOrWhiteSpace(itemCodigo))
        {
            item = await eventos.ItemAsync(itemCodigo.Trim(), cancellationToken)
                ?? throw new RecusaException(
                    TipoDeRecusa.NaoEncontrado,
                    CodigosDoPremioParticipacao.ItemNaoEncontrado,
                    $"{itemCodigo} não é prêmio de participação do catálogo.");
        }

        var hoje = FusoHorario.DataLocal(relogio.GetUtcNow());
        var ultimoEncerrado = await eventos.UltimoSemestreEncerradoAsync(hoje, cancellationToken);
        var cursoId = await eventos.CursoDoAlunoAsync(alunoId, cancellationToken);

        return [.. (await eventos.SemeadosAsync(cancellationToken))
            .Where(evento => evento.AbertoAoCurso(cursoId))
            .Where(evento => evento.RecusaParaReivindicarEm(hoje, ultimoEncerrado) is null)
            .Where(evento => item is null || evento.Aceita(item))
            .OrderByDescending(evento => evento.Inicio)
            .ThenBy(evento => evento.Nome, StringComparer.Ordinal)];
    }
}

public interface IEventoRepository
{
    // Poucos por semestre: a lista inteira, filtrada pela regra do domínio.
    Task<IReadOnlyList<Evento>> SemeadosAsync(CancellationToken cancellationToken);

    Task<ItemPremioParticipacao?> ItemAsync(string codigo, CancellationToken cancellationToken);

    // O semestre letivo de fim mais recente antes da data, com os bimestres; nulo se nenhum
    // terminou ainda.
    Task<SemestreLetivo?> UltimoSemestreEncerradoAsync(DateOnly data, CancellationToken cancellationToken);

    // O curso atual do aluno, pelo cadastro elegível.
    Task<Guid?> CursoDoAlunoAsync(Guid alunoId, CancellationToken cancellationToken);
}
