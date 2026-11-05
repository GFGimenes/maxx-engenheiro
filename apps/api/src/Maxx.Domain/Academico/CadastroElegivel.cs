namespace Maxx.Domain.Academico;

// A base que a faculdade abastece. Estar nela não é ter conta: o Usuario do passo 3 se
// liga a uma linha daqui, uma para uma. O papel vem daqui, nunca do domínio do e-mail.
public sealed class CadastroElegivel
{
    // Para o EF Core materializar do banco.
    private CadastroElegivel()
    {
        Email = null!;
        NomeCompleto = null!;
    }

    private CadastroElegivel(
        Papeis papeis, string? ra, string email, string nomeCompleto, Guid? cursoId, int? semestre)
    {
        Id = Guid.CreateVersion7();
        Papeis = papeis;
        Ra = ra;
        Email = email;
        NomeCompleto = nomeCompleto;
        CursoId = cursoId;
        Semestre = semestre;
    }

    public Guid Id { get; private set; }

    public Papeis Papeis { get; private set; }

    // Identidade interna e credencial de login do aluno. Opcional para os outros papéis.
    public string? Ra { get; private set; }

    public string Email { get; private set; }

    public string NomeCompleto { get; private set; }

    // Só do aluno: curso atual e semestre do aluno (perfil, não pontua).
    public Guid? CursoId { get; private set; }

    public int? Semestre { get; private set; }

    // Linha criada pela banca no modo de demonstração (produto.md 11), não pela faculdade.
    public bool Demonstracao { get; private set; }

    public bool EhAluno => Papeis == Papeis.Aluno;

    public bool EhProfessor => Papeis.HasFlag(Papeis.Professor);

    public static CadastroElegivel Aluno(string ra, string email, string nomeCompleto, Curso curso, int semestre)
    {
        ArgumentNullException.ThrowIfNull(curso);

        if (semestre < 1 || semestre > curso.DuracaoEmSemestres)
        {
            throw new DomainException(
                $"O curso {curso.Sigla} vai do semestre 1 ao {curso.DuracaoEmSemestres}, não tem o {semestre}.");
        }

        return new CadastroElegivel(
            Papeis.Aluno,
            Texto.Obrigatorio(ra, "O RA do aluno"),
            ValidarEmail(email),
            Texto.Obrigatorio(nomeCompleto, "O nome completo"),
            curso.Id,
            semestre);
    }

    // RA gerado e e-mail num domínio reservado (RFC 2606), que nunca recebe nada: o aluno
    // de demonstração não tem senha nem recuperação. Semestre 1, que é só perfil.
    public static CadastroElegivel AlunoDeDemonstracao(string ra, string nomeCompleto, Curso curso)
    {
        ra = Texto.Obrigatorio(ra, "O RA do aluno");
        var cadastro = Aluno(ra, $"{ra.ToLowerInvariant()}@{DominioDaDemonstracao}", nomeCompleto, curso, semestre: 1);
        cadastro.Demonstracao = true;
        return cadastro;
    }

    public const string DominioDaDemonstracao = "demonstracao.invalid";

    public static CadastroElegivel Professor(string email, string nomeCompleto, bool tambemInstituicao = false, string? ra = null) =>
        Funcionario(tambemInstituicao ? Papeis.Professor | Papeis.Instituicao : Papeis.Professor, email, nomeCompleto, ra);

    public static CadastroElegivel Instituicao(string email, string nomeCompleto, string? ra = null) =>
        Funcionario(Papeis.Instituicao, email, nomeCompleto, ra);

    private static CadastroElegivel Funcionario(Papeis papeis, string email, string nomeCompleto, string? ra) =>
        new(
            papeis,
            ra is null ? null : Texto.Obrigatorio(ra, "O RA"),
            ValidarEmail(email),
            Texto.Obrigatorio(nomeCompleto, "O nome completo"),
            cursoId: null,
            semestre: null);

    // Só a forma. Os domínios aceitos são configuração do passo 3.
    private static string ValidarEmail(string email)
    {
        email = Texto.Obrigatorio(email, "O e-mail");
        var arroba = email.IndexOf('@', StringComparison.Ordinal);
        if (arroba <= 0 || arroba != email.LastIndexOf('@') || arroba == email.Length - 1)
        {
            throw new DomainException($"E-mail inválido: {email}.");
        }

        return email;
    }
}
