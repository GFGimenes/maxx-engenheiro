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
        var cursoId = (await eventos.CursoDoAlunoAsync(alunoId, cancellationToken))?.Id;

        return [.. (await eventos.SemeadosAsync(cancellationToken))
            .Where(evento => evento.AbertoAoCurso(cursoId))
            .Where(evento => evento.RecusaParaReivindicarEm(hoje, ultimoEncerrado) is null)
            .Where(evento => item is null || evento.Aceita(item))
            .OrderByDescending(evento => evento.Inicio)
            .ThenBy(evento => evento.Nome, StringComparer.Ordinal)];
    }
}

// As matérias que o aluno cursa hoje: o que a apresentação em sala (P17) oferece. A data do
// evento vale de hoje para trás e não começa antes do fim do semestre anterior (D3), então a
// matéria da apresentação é deste semestre.
public sealed class MateriasDoAlunoHandler(IEventoRepository eventos, TimeProvider relogio)
{
    public Task<IReadOnlyList<Materia>> CursadasAsync(Guid alunoId, CancellationToken cancellationToken) =>
        eventos.MateriasCursadasAsync(alunoId, FusoHorario.DataLocal(relogio.GetUtcNow()), cancellationToken);
}

public interface IEventoRepository
{
    // Poucos por semestre: a lista inteira, filtrada pela regra do domínio.
    Task<IReadOnlyList<Evento>> SemeadosAsync(CancellationToken cancellationToken);

    Task<ItemPremioParticipacao?> ItemAsync(string codigo, CancellationToken cancellationToken);

    Task<Evento?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    // As matérias das turmas do aluno (usuário) cujo semestre contém a data, por nome.
    Task<IReadOnlyList<Materia>> MateriasCursadasAsync(Guid alunoId, DateOnly data, CancellationToken cancellationToken);

    // O semestre letivo de fim mais recente antes da data, com os bimestres; nulo se nenhum
    // terminou ainda.
    Task<SemestreLetivo?> UltimoSemestreEncerradoAsync(DateOnly data, CancellationToken cancellationToken);

    // O curso atual do aluno, pelo cadastro elegível; nulo para quem não é aluno.
    Task<Curso?> CursoDoAlunoAsync(Guid alunoId, CancellationToken cancellationToken);
}
