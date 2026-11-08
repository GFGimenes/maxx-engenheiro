using Maxx.Application.Fila;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;

namespace Maxx.Application.Conquistas;

public interface IConquistaRepository
{
    // Serializa as avaliações do mesmo aluno até o fim da transação do trabalho: duas
    // reavaliações simultâneas nunca concedem a mesma conquista duas vezes (D2 do passo 9).
    // O índice único por instância continua de rede de segurança.
    Task TravarAlunoAsync(Guid alunoId, CancellationToken cancellationToken);

    // O aluno com conta, ex-alunos inclusive, com a sigla do curso atual. Nulo para quem não
    // é aluno, ou para a conta que a purga já levou.
    Task<AlunoDasConquistas?> AlunoAsync(Guid alunoId, CancellationToken cancellationToken);

    // As conquistas do catálogo, com a versão gravada pelo seeder, na ordem do código.
    Task<IReadOnlyList<ConquistaDoCatalogo>> CatalogoAsync(CancellationToken cancellationToken);

    Task<Calendario> CalendarioAsync(CancellationToken cancellationToken);

    // Os fatos contáveis do aluno: medalhas ativas e prêmios de participação definitivos.
    Task<IReadOnlyList<FatoContavel>> FatosAsync(Guid alunoId, Calendario calendario, CancellationToken cancellationToken);

    // As matérias distintas das turmas desses semestres em que o aluno está matriculado
    // (decisão do passo 9): o alvo dinâmico.
    Task<IReadOnlySet<Guid>> MateriasDoPeriodoAsync(
        Guid alunoId, IReadOnlyList<SemestreLetivo> semestres, CancellationToken cancellationToken);

    // As conquistas não removidas do aluno, com as evidências.
    Task<IReadOnlyList<Conquista>> AtivasAsync(Guid alunoId, CancellationToken cancellationToken);

    void Adicionar(Conquista conquista);

    // O progresso ("2 de 3") da instância, que a tela de Troféus lê sem calcular (D5).
    Task GravarProgressoAsync(
        Guid alunoId,
        string itemCodigo,
        InstanciaDePeriodo instancia,
        Avaliacao avaliacao,
        DateTimeOffset agora,
        CancellationToken cancellationToken);
}

public sealed record AlunoDasConquistas(Guid Id, string SiglaDoCurso);

public sealed record ConquistaDoCatalogo(ItemConquista Item, int Versao);

// Um fato ficou contável: a medalha concedida, ou o prêmio de participação que ficou
// definitivo, pelo fim da janela ou pela confirmação da contestação. Enfileirado na mesma
// transação do fato. Leva um dos dois ids, nunca os dois.
public sealed record ReavaliarConquistas(Guid AlunoId, Guid? MedalhaId, Guid? PremioParticipacaoId) : ICargaDeTrabalho
{
    public static string Tipo => "reavaliar-conquistas";

    public static ReavaliarConquistas PelaMedalha(Guid alunoId, Guid medalhaId) => new(alunoId, medalhaId, null);

    public static ReavaliarConquistas PeloPremio(Guid alunoId, Guid premioId) => new(alunoId, null, premioId);

    public bool Aponta(FatoContavel fato)
    {
        ArgumentNullException.ThrowIfNull(fato);

        return fato.Origem switch
        {
            OrigemDoFato.Medalha => fato.Id == MedalhaId,
            OrigemDoFato.PremioParticipacao => fato.Id == PremioParticipacaoId,
            _ => false,
        };
    }
}
