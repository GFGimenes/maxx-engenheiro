using Maxx.Application.Paginacao;
using Maxx.Application.Posts;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Premios;

namespace Maxx.Application.PremiosParticipacao;

// O que se lê da contestação e do histórico do aluno. Só consulta, sem regra.
public interface IConsultaDeContestacoes
{
    // A fila de contestações da instituição (produto.md 4.7): as abertas, das mais antigas para
    // as mais novas, com a marca das que passaram de 7 dias desde a contestação.
    Task<Pagina<ContestacaoNaFila>> AbertasAsync(PosicaoNoTempo? depoisDe, int limite, DateTimeOffset agora, CancellationToken cancellationToken);

    // As reivindicações do aluno que não estão pontuando (D9): pendente, em averiguação,
    // reprovada e revogada, as duas últimas com o motivo. Da mais recente para a mais antiga,
    // pelo instante em que entrou no estado. Só o próprio aluno lê (produto.md 9.1).
    Task<IReadOnlyList<ReivindicacaoDoAluno>> ReivindicacoesAsync(Guid alunoId, CancellationToken cancellationToken);
}

// Uma contestação aberta, com o que a instituição precisa para decidir: o prêmio e a foto
// como na fila de comprovações, o motivo, quem contestou e quando, e quando foi aprovado.
public sealed record ContestacaoNaFila(
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
    string Motivo,
    string ContestadaPor,
    DateTimeOffset ContestadaEm,
    DateTimeOffset AprovadoEm,
    bool PassouDoPrazo);

// Motivo: o da reprovação ou o da revogação. Desde: quando entrou no estado.
public sealed record ReivindicacaoDoAluno(
    Guid PremioId,
    Guid? PostId,
    string Item,
    string NomeDoItem,
    Tier Tier,
    int Pontos,
    string Evento,
    string? Materia,
    EstadoDoPremioParticipacao Estado,
    string? Motivo,
    DateTimeOffset Desde);
