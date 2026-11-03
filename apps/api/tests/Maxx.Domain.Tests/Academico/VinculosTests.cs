using Maxx.Domain.Academico;

namespace Maxx.Domain.Tests.Academico;

public class VinculosTests
{
    private static readonly Curso _computacao = new("Ciência da Computação", "CC", 8);
    private static readonly Turma _bancoDeDados2026 = new(new Materia(_computacao, "Banco de Dados"), Calendario2026.Segundo());

    private static readonly CadastroElegivel _aluno =
        CadastroElegivel.Aluno("123456", "ana@al.unieduk.com.br", "Ana Souza", _computacao, 3);

    private static readonly CadastroElegivel _professor =
        CadastroElegivel.Professor("bruno@prof.unieduk.com.br", "Bruno Lima");

    private static readonly CadastroElegivel _professorEInstituicao =
        CadastroElegivel.Professor("carla@unieduk.com.br", "Carla Dias", tambemInstituicao: true);

    private static readonly CadastroElegivel _instituicao =
        CadastroElegivel.Instituicao("secretaria@unieduk.com.br", "Secretaria UniMax");

    [Fact]
    public void Aluno_se_matricula_na_turma()
    {
        var matricula = new Matricula(_aluno, _bancoDeDados2026);

        Assert.Equal(_aluno.Id, matricula.AlunoId);
        Assert.Equal(_bancoDeDados2026.Id, matricula.TurmaId);
    }

    [Fact]
    public void Aluno_se_matricula_em_turma_de_materia_do_proprio_curso()
    {
        var administracao = new Curso("Administração", "ADM", 8);
        var aluna = CadastroElegivel.Aluno("654321", "bia@al.unieduk.com.br", "Bia Rocha", administracao, 1);
        var marketing = new Turma(new Materia(administracao, "Marketing"), Calendario2026.Segundo());

        var matricula = new Matricula(aluna, marketing);

        Assert.Equal(marketing.Id, matricula.TurmaId);
    }

    [Fact]
    public void Aluno_nao_se_matricula_em_turma_de_materia_de_outro_curso()
    {
        var administracao = new Curso("Administração", "ADM", 8);
        var aluna = CadastroElegivel.Aluno("654321", "bia@al.unieduk.com.br", "Bia Rocha", administracao, 1);
        var algoritmos = new Turma(new Materia(_computacao, "Algoritmos e Programação"), Calendario2026.Segundo());

        Assert.Throws<DomainException>(() => new Matricula(aluna, algoritmos));
    }

    [Fact]
    public void Professor_nao_se_matricula()
    {
        Assert.Throws<DomainException>(() => new Matricula(_professor, _bancoDeDados2026));
    }

    [Fact]
    public void Instituicao_nao_se_matricula()
    {
        Assert.Throws<DomainException>(() => new Matricula(_instituicao, _bancoDeDados2026));
    }

    [Fact]
    public void Professor_se_vincula_a_turma()
    {
        var vinculo = new VinculoProfessor(_professor, _bancoDeDados2026);

        Assert.Equal(_professor.Id, vinculo.ProfessorId);
        Assert.Equal(_bancoDeDados2026.Id, vinculo.TurmaId);
    }

    [Fact]
    public void Professor_que_tambem_e_instituicao_se_vincula_a_turma()
    {
        var vinculo = new VinculoProfessor(_professorEInstituicao, _bancoDeDados2026);

        Assert.Equal(_professorEInstituicao.Id, vinculo.ProfessorId);
    }

    [Fact]
    public void Aluno_nao_se_vincula_como_professor()
    {
        Assert.Throws<DomainException>(() => new VinculoProfessor(_aluno, _bancoDeDados2026));
    }

    [Fact]
    public void Instituicao_sem_professor_nao_se_vincula()
    {
        Assert.Throws<DomainException>(() => new VinculoProfessor(_instituicao, _bancoDeDados2026));
    }
}
