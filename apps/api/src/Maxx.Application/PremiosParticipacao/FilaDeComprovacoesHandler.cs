using Maxx.Application.Autenticacao;
using Maxx.Application.Paginacao;
using Maxx.Application.Posts;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;

namespace Maxx.Application.PremiosParticipacao;

// A fila de comprovações (produto.md 4.7): as pendentes, das mais antigas para as mais novas,
// com a marca das que passaram de 7 dias, sem decisão automática. A instituição vê todas; o
// professor, as de alunos matriculados em turma sua do semestre vigente, conferido no banco.
// Filtrável por evento, pensando na aprovação em lote da fase 2.
public sealed class FilaDeComprovacoesHandler(IUsuarioRepository usuarios, IConsultaDeComprovacoes consulta, TimeProvider relogio)
{
    public const int PorPagina = 20;

    public async Task<Pagina<ComprovacaoNaFila>> PendentesAsync(
        Guid quemId, Guid? eventoId, string? cursor, CancellationToken cancellationToken)
    {
        var posicao = CursorDeTempo.Ler(cursor);
        var escopo = await EscopoAsync(quemId, cancellationToken);
        return await consulta.PendentesAsync(escopo, eventoId, posicao, PorPagina, relogio.GetUtcNow(), cancellationToken);
    }

    // Os eventos que têm comprovação pendente no escopo, para o filtro da fila.
    public async Task<IReadOnlyList<EventoNaFila>> EventosAsync(Guid quemId, CancellationToken cancellationToken) =>
        await consulta.EventosAsync(await EscopoAsync(quemId, cancellationToken), relogio.GetUtcNow(), cancellationToken);

    // O papel lido do banco, não do token: a conta pode ter mudado depois da emissão.
    private async Task<EscopoDaFila> EscopoAsync(Guid quemId, CancellationToken cancellationToken)
    {
        var quem = await usuarios.BuscarAsync(quemId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {quemId} não existe.");
        return new EscopoDaFila(quem.Id, quem.Papeis.HasFlag(Papeis.Instituicao));
    }
}

// Quem lê a fila. Instituição: tudo. Senão, professor: só os alunos dele no semestre vigente.
public sealed record EscopoDaFila(Guid UsuarioId, bool Instituicao);

public interface IConsultaDeComprovacoes
{
    // agora: para o semestre vigente do escopo e para a marca de 7 dias.
    Task<Pagina<ComprovacaoNaFila>> PendentesAsync(
        EscopoDaFila escopo, Guid? eventoId, PosicaoNoTempo? depoisDe, int limite, DateTimeOffset agora, CancellationToken cancellationToken);

    Task<IReadOnlyList<EventoNaFila>> EventosAsync(EscopoDaFila escopo, DateTimeOffset agora, CancellationToken cancellationToken);
}

// Uma comprovação pendente, com o que o aprovador precisa para decidir: o aluno com o curso,
// o item com tier e pontos do momento, o evento, a foto e a legenda do post. Matérias: as que o
// aluno cursa no semestre vigente e, para o professor, só as que ele também leciona (D13).
public sealed record ComprovacaoNaFila(
    Guid Id,
    Guid PremioId,
    Guid? PostId,
    AutorDoPost Aluno,
    string Item,
    string NomeDoItem,
    Tier Tier,
    int Pontos,
    EventoDaComprovacao Evento,
    string? Materia,
    string? ChaveDaFotoDoFeed,
    string? ChaveDaFotoDaGrade,
    string? Legenda,
    DateTimeOffset ReivindicadaEm,
    bool PassouDoPrazo,
    IReadOnlyList<string> Materias);

public sealed record EventoDaComprovacao(Guid Id, string Nome, DateOnly Inicio, DateOnly Fim, bool Digitado);

public sealed record EventoNaFila(Guid Id, string Nome, DateOnly Inicio, int Pendentes);
