using Maxx.Domain.Academico;

namespace Maxx.Domain.Tests.Academico;

public class CadastroElegivelTests
{
    private static readonly Curso _medicina = new("Medicina", "MED", 12);

    [Fact]
    public void Aluno_guarda_ra_curso_e_semestre()
    {
        var aluno = CadastroElegivel.Aluno(" 123456 ", "ana@al.unieduk.com.br", "Ana Souza", _medicina, 12);

        Assert.Equal(Papeis.Aluno, aluno.Papeis);
        Assert.Equal("123456", aluno.Ra);
        Assert.Equal(_medicina.Id, aluno.CursoId);
        Assert.Equal(12, aluno.Semestre);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Semestre_do_aluno_fica_entre_1_e_a_duracao_do_curso(int semestre)
    {
        Assert.Throws<DomainException>(
            () => CadastroElegivel.Aluno("123456", "ana@al.unieduk.com.br", "Ana Souza", _medicina, semestre));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Aluno_sem_ra_e_recusado(string ra)
    {
        Assert.Throws<DomainException>(
            () => CadastroElegivel.Aluno(ra, "ana@al.unieduk.com.br", "Ana Souza", _medicina, 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ana")]
    [InlineData("@al.unieduk.com.br")]
    [InlineData("ana@")]
    [InlineData("ana@x@y")]
    public void Email_sem_forma_de_email_e_recusado(string email)
    {
        Assert.Throws<DomainException>(() => CadastroElegivel.Aluno("123456", email, "Ana Souza", _medicina, 1));
    }

    [Fact]
    public void Professor_nao_tem_curso_nem_semestre_e_o_ra_e_opcional()
    {
        var professor = CadastroElegivel.Professor("bruno@prof.unieduk.com.br", "Bruno Lima");

        Assert.Equal(Papeis.Professor, professor.Papeis);
        Assert.Null(professor.Ra);
        Assert.Null(professor.CursoId);
        Assert.Null(professor.Semestre);
    }

    [Fact]
    public void Professor_pode_acumular_instituicao()
    {
        var professor = CadastroElegivel.Professor("bruno@unieduk.com.br", "Bruno Lima", tambemInstituicao: true);

        Assert.Equal(Papeis.Professor | Papeis.Instituicao, professor.Papeis);
        Assert.True(professor.EhProfessor);
        Assert.False(professor.EhAluno);
    }

    [Fact]
    public void Instituicao_nao_e_professor_nem_aluno()
    {
        var instituicao = CadastroElegivel.Instituicao("secretaria@unieduk.com.br", "Secretaria UniMax");

        Assert.False(instituicao.EhProfessor);
        Assert.False(instituicao.EhAluno);
    }

    [Theory]
    [InlineData("cc")]
    [InlineData("C C")]
    [InlineData("ADMÍ")]
    [InlineData("")]
    public void Sigla_do_curso_so_tem_letras_maiusculas_sem_acento(string sigla)
    {
        Assert.Throws<DomainException>(() => new Curso("Ciência da Computação", sigla, 8));
    }

    [Fact]
    public void Curso_dura_ao_menos_um_semestre()
    {
        Assert.Throws<DomainException>(() => new Curso("Ciência da Computação", "CC", 0));
    }
}
