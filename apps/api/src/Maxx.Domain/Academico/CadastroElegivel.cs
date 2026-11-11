using Maxx.Domain.Identidade;

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

    // Professor e coordenação de demonstração (passo 14): papel simples, sem acúmulo, sem
    // RA, com e-mail no mesmo domínio reservado. O identificador, gerado por quem chama,
    // só faz o e-mail ser único.
    public static CadastroElegivel ProfessorDeDemonstracao(string identificador, string nomeCompleto) =>
        FuncionarioDeDemonstracao(Papeis.Professor, identificador, nomeCompleto);

    public static CadastroElegivel CoordenacaoDeDemonstracao(string identificador, string nomeCompleto) =>
        FuncionarioDeDemonstracao(Papeis.Instituicao, identificador, nomeCompleto);

    private static CadastroElegivel FuncionarioDeDemonstracao(Papeis papeis, string identificador, string nomeCompleto)
    {
        identificador = Texto.Obrigatorio(identificador, "O identificador da demonstração");
        var cadastro = Funcionario(papeis, $"{identificador.ToLowerInvariant()}@{DominioDaDemonstracao}", nomeCompleto, ra: null);
        cadastro.Demonstracao = true;
        return cadastro;
    }

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


    // Correção de e-mail pela instituição (produto.md 2.3, passo 12): só a linha de aluno,
    // com ou sem conta. Confere a forma e o domínio (produto.md 2.4, passo 13); a unicidade é
    // do banco. O mesmo e-mail, escrito igual, não muda nada e não gera registro; trocar só
    // maiúsculas é correção. Quem corrige por uma conta de demonstração (passo 14, P5) só
    // escreve endereço em domínio reservado .invalid (RFC 2606): um endereço real faria o
    // Maxx mandar e-mail a terceiros e daria a conta do aluno a quem corrigiu, pela
    // recuperação de senha.
    public ResultadoDaCorrecaoDeEmail CorrigirEmail(
        string? novo, DominiosDeEmailAceitos dominios, Guid corrigidoPorId, DateTimeOffset agora, bool soDominioReservado = false)
    {
        ArgumentNullException.ThrowIfNull(dominios);
        GarantirAluno("ter o e-mail corrigido");

        novo = novo?.Trim();
        if (novo is null || !EmailBemFormado(novo))
        {
            return new(null, RecusaDaCorrecaoDeEmail.EmailInvalido);
        }

        if (!dominios.Aceita(novo))
        {
            return new(null, RecusaDaCorrecaoDeEmail.DominioNaoAceito);
        }

        if (soDominioReservado && !novo.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
        {
            return new(null, RecusaDaCorrecaoDeEmail.DominioNaoReservado);
        }

        if (string.Equals(novo, Email, StringComparison.Ordinal))
        {
            return new(null, null);
        }

        var correcao = new CorrecaoDeEmail(this, Email, novo, corrigidoPorId, agora);
        Email = novo;
        return new(correcao, null);
    }

    // Troca de curso pela instituição (passo 12, P4): curso novo e o semestre do aluno nele.
    // As matrículas antigas ficam. O sufixo do arroba, quando há conta, troca no Usuario.
    public ResultadoDaTrocaDeCurso TrocarDeCurso(Curso curso, int semestre, Guid trocadoPorId, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(curso);
        GarantirAluno("trocar de curso");

        if (curso.Id == CursoId)
        {
            return new(null, RecusaDaTrocaDeCurso.MesmoCurso);
        }

        if (semestre < 1 || semestre > curso.DuracaoEmSemestres)
        {
            return new(null, RecusaDaTrocaDeCurso.SemestreForaDoCurso);
        }

        var troca = new TrocaDeCurso(this, CursoId!.Value, Semestre!.Value, curso.Id, semestre, trocadoPorId, agora);
        CursoId = curso.Id;
        Semestre = semestre;
        return new(troca, null);
    }

    public static bool EmailBemFormado(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var arroba = email.IndexOf('@', StringComparison.Ordinal);
        return email.Length > 0
            && email.Trim().Length == email.Length
            && !email.Any(char.IsWhiteSpace)
            && arroba > 0
            && arroba == email.LastIndexOf('@')
            && arroba < email.Length - 1;
    }

    private void GarantirAluno(string acao)
    {
        if (!EhAluno)
        {
            throw new DomainException($"Só linha de aluno pode {acao}; {Email} é {Papeis}.");
        }
    }

    // Só a forma. Os domínios aceitos (DominiosDeEmailAceitos) valem no cadastro e na
    // correção, não na carga da base.
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
