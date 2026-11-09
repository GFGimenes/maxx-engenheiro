using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Tests.Academico;

namespace Maxx.Domain.Tests.Conquistas;

// O calendário de 2026 e fatos contáveis montados à mão, para testar o avaliador sem passar
// pela concessão. Cada instância guarda os próprios ids: semestres e bimestres são criados
// uma vez por instância desta classe.
internal sealed class Fatos
{
    public Fatos()
    {
        Ano = Calendario2026.Ano();
        Calendario = new Calendario([Ano]);
    }

    public static readonly Guid Aluno = Guid.CreateVersion7();
    public static readonly Guid BancoDeDados = Guid.CreateVersion7();
    public static readonly Guid Redes = Guid.CreateVersion7();
    public static readonly Guid Algoritmos = Guid.CreateVersion7();

    public AnoLetivo Ano { get; }

    public Calendario Calendario { get; }

    public SemestreLetivo Semestre(int numero) => Ano.Semestres[numero - 1];

    // Numerado de 1 a 4 no ano, como no calendário.
    public Bimestre Bimestre(int numero) => Ano.Bimestres.Single(bimestre => bimestre.Numero == numero);

    public InstanciaDePeriodo InstanciaDoBimestre(int numero) => new(Periodo.Bimestre, Bimestre(numero).Id);

    public InstanciaDePeriodo InstanciaDoSemestre(int numero) => new(Periodo.Semestre, Semestre(numero).Id);

    public InstanciaDePeriodo InstanciaDoAno => new(Periodo.Ano, Ano.Id);

    public static InstanciaDePeriodo InstanciaDoCurso => new(Periodo.Curso, Aluno);

    public FatoContavel Medalha(string item, int bimestre, Guid? materia = null)
    {
        var semestre = Ano.Semestres.Single(candidato => candidato.Bimestres.Any(b => b.Numero == bimestre));
        return new FatoContavel
        {
            Origem = OrigemDoFato.Medalha,
            Id = Guid.CreateVersion7(),
            ItemCodigo = item,
            MateriaId = materia ?? BancoDeDados,
            BimestreId = Bimestre(bimestre).Id,
            SemestreLetivoId = semestre.Id,
            AnoLetivoId = Ano.Id,
        };
    }

    public IEnumerable<FatoContavel> Medalhas(string item, int bimestre, int quantas, Guid? materia = null) =>
        Enumerable.Range(0, quantas).Select(_ => Medalha(item, bimestre, materia)).ToList();

    public FatoContavel Premio(string item, string tipoDeEvento, int semestre = 2) => new()
    {
        Origem = OrigemDoFato.PremioParticipacao,
        Id = Guid.CreateVersion7(),
        ItemCodigo = item,
        SemestreLetivoId = Semestre(semestre).Id,
        AnoLetivoId = Ano.Id,
        TipoDeEvento = tipoDeEvento,
    };

    public IEnumerable<FatoContavel> Premios(string item, string tipoDeEvento, int quantos, int semestre = 2) =>
        Enumerable.Range(0, quantos).Select(_ => Premio(item, tipoDeEvento, semestre)).ToList();

    // Contexto de uma instância comum: aluno de CC, data dentro do semestre 2026/2.
    public static ContextoDaAvaliacao Contexto(params Guid[] materias) =>
        new(Calendario2026.Data(12, 5), "CC", materias.Length == 0 ? null : materias.ToHashSet());

    public static ContextoDaAvaliacao SemMatriculas => new(Calendario2026.Data(12, 5), "CC", new HashSet<Guid>());
}
