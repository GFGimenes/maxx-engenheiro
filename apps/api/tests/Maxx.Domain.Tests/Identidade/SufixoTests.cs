using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Identidade;

public class SufixoTests
{
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);

    [Fact]
    public void Aluno_usa_a_sigla_do_curso()
    {
        Assert.Equal("CC", Sufixo.Para(Papeis.Aluno, _cc, exAluno: false));
    }

    [Fact]
    public void Ex_aluno_usa_ex()
    {
        Assert.Equal("EX", Sufixo.Para(Papeis.Aluno, _cc, exAluno: true));
    }

    [Fact]
    public void Professor_usa_prof_mesmo_acumulando_instituicao()
    {
        Assert.Equal("PROF", Sufixo.Para(Papeis.Professor, curso: null, exAluno: false));
        Assert.Equal("PROF", Sufixo.Para(Papeis.Professor | Papeis.Instituicao, curso: null, exAluno: false));
    }

    [Fact]
    public void Instituicao_usa_unimax()
    {
        Assert.Equal("UNIMAX", Sufixo.Para(Papeis.Instituicao, curso: null, exAluno: false));
    }

    [Fact]
    public void Aluno_sem_curso_e_recusado()
    {
        Assert.Throws<DomainException>(() => Sufixo.Para(Papeis.Aluno, curso: null, exAluno: false));
    }

    [Fact]
    public void Arroba_e_exibido_com_arroba_e_hifen()
    {
        Assert.Equal("@guilhermeGimenes-CC", new Arroba("guilhermeGimenes", "CC").ToString());
    }
}
