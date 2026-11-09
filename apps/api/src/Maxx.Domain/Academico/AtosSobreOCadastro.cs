namespace Maxx.Domain.Academico;

// Os registros dos atos da instituição sobre a linha do aluno (passo 12, P5): quem fez e
// quando, com o antes e o depois. Nascem de CadastroElegivel.CorrigirEmail e
// CadastroElegivel.TrocarDeCurso.
public sealed class CorrecaoDeEmail
{
    // Para o EF Core materializar do banco.
    private CorrecaoDeEmail()
    {
        EmailAnterior = null!;
        EmailNovo = null!;
    }

    internal CorrecaoDeEmail(CadastroElegivel cadastro, string anterior, string novo, Guid corrigidoPorId, DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        CadastroElegivelId = cadastro.Id;
        EmailAnterior = anterior;
        EmailNovo = novo;
        CorrigidoPorId = corrigidoPorId;
        CorrigidoEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid CadastroElegivelId { get; private set; }

    public string EmailAnterior { get; private set; }

    public string EmailNovo { get; private set; }

    public Guid CorrigidoPorId { get; private set; }

    public DateTimeOffset CorrigidoEm { get; private set; }
}

public sealed class TrocaDeCurso
{
    // Para o EF Core materializar do banco.
    private TrocaDeCurso()
    {
    }

    internal TrocaDeCurso(
        CadastroElegivel cadastro,
        Guid cursoAnteriorId,
        int semestreAnterior,
        Guid cursoNovoId,
        int semestreNovo,
        Guid trocadoPorId,
        DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        CadastroElegivelId = cadastro.Id;
        CursoAnteriorId = cursoAnteriorId;
        SemestreAnterior = semestreAnterior;
        CursoNovoId = cursoNovoId;
        SemestreNovo = semestreNovo;
        TrocadoPorId = trocadoPorId;
        TrocadoEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid CadastroElegivelId { get; private set; }

    public Guid CursoAnteriorId { get; private set; }

    public int SemestreAnterior { get; private set; }

    public Guid CursoNovoId { get; private set; }

    public int SemestreNovo { get; private set; }

    public Guid TrocadoPorId { get; private set; }

    public DateTimeOffset TrocadoEm { get; private set; }
}

// Correcao nula e Recusa nula: o mesmo e-mail, nada mudou.
public sealed record ResultadoDaCorrecaoDeEmail(CorrecaoDeEmail? Correcao, RecusaDaCorrecaoDeEmail? Recusa);

public enum RecusaDaCorrecaoDeEmail
{
    EmailInvalido,
}

public sealed record ResultadoDaTrocaDeCurso(TrocaDeCurso? Troca, RecusaDaTrocaDeCurso? Recusa);

public enum RecusaDaTrocaDeCurso
{
    MesmoCurso,
    SemestreForaDoCurso,
}
