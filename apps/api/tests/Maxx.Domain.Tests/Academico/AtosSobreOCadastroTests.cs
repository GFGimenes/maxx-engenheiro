using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Academico;

// Os atos da instituição sobre a linha do aluno (passo 12): correção de e-mail e troca de
// curso, cada um com quem fez, quando, o antes e o depois.
public class AtosSobreOCadastroTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 9, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid _coordenacao = Guid.CreateVersion7();
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);
    private static readonly Curso _med = new("Medicina", "MED", 12);
    private static readonly DominiosDeEmailAceitos _dominios = new(["al.unieduk.com.br", "prof.unieduk.com.br"]);

    private static CadastroElegivel Ana() =>
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3);

    [Fact]
    public void Correcao_de_email_troca_o_email_e_registra_o_antes_e_o_depois()
    {
        var ana = Ana();

        var resultado = ana.CorrigirEmail("  ana.b.souza@al.unieduk.com.br ", _dominios, _coordenacao, _agora);

        Assert.Null(resultado.Recusa);
        var correcao = Assert.IsType<CorrecaoDeEmail>(resultado.Correcao);
        Assert.Equal("ana.b.souza@al.unieduk.com.br", ana.Email);
        Assert.Equal(ana.Id, correcao.CadastroElegivelId);
        Assert.Equal("ana.souza@al.unieduk.com.br", correcao.EmailAnterior);
        Assert.Equal("ana.b.souza@al.unieduk.com.br", correcao.EmailNovo);
        Assert.Equal(_coordenacao, correcao.CorrigidoPorId);
        Assert.Equal(_agora, correcao.CorrigidoEm);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sem-arroba")]
    [InlineData("@al.unieduk.com.br")]
    [InlineData("ana@")]
    [InlineData("ana@@al.unieduk.com.br")]
    [InlineData("ana souza@al.unieduk.com.br")]
    [InlineData(null)]
    public void Email_fora_da_forma_e_recusado_sem_mudar_nada(string? email)
    {
        var ana = Ana();

        var resultado = ana.CorrigirEmail(email, _dominios, _coordenacao, _agora);

        Assert.Equal(RecusaDaCorrecaoDeEmail.EmailInvalido, resultado.Recusa);
        Assert.Null(resultado.Correcao);
        Assert.Equal("ana.souza@al.unieduk.com.br", ana.Email);
    }

    [Theory]
    [InlineData("ana.souza@gmail.com")]
    [InlineData("ana.souza@x.al.unieduk.com.br")]
    public void Email_de_dominio_fora_da_lista_e_recusado_sem_mudar_nada(string email)
    {
        var ana = Ana();

        var resultado = ana.CorrigirEmail(email, _dominios, _coordenacao, _agora);

        Assert.Equal(RecusaDaCorrecaoDeEmail.DominioNaoAceito, resultado.Recusa);
        Assert.Null(resultado.Correcao);
        Assert.Equal("ana.souza@al.unieduk.com.br", ana.Email);
    }

    [Fact]
    public void O_mesmo_email_nao_muda_nada_mas_so_maiusculas_e_correcao()
    {
        var ana = Ana();

        Assert.Equal(new ResultadoDaCorrecaoDeEmail(null, null), ana.CorrigirEmail("ana.souza@al.unieduk.com.br", _dominios, _coordenacao, _agora));
        Assert.NotNull(ana.CorrigirEmail("Ana.Souza@al.unieduk.com.br", _dominios, _coordenacao, _agora).Correcao);
        Assert.Equal("Ana.Souza@al.unieduk.com.br", ana.Email);
    }

    [Fact]
    public void Troca_de_curso_muda_curso_e_semestre_e_registra_o_antes_e_o_depois()
    {
        var ana = Ana();

        var resultado = ana.TrocarDeCurso(_med, 11, _coordenacao, _agora);

        Assert.Null(resultado.Recusa);
        var troca = Assert.IsType<TrocaDeCurso>(resultado.Troca);
        Assert.Equal((_med.Id, 11), (ana.CursoId, ana.Semestre));
        Assert.Equal((_cc.Id, 3, _med.Id, 11), (troca.CursoAnteriorId, troca.SemestreAnterior, troca.CursoNovoId, troca.SemestreNovo));
        Assert.Equal((_coordenacao, _agora), (troca.TrocadoPorId, troca.TrocadoEm));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Semestre_fora_da_duracao_do_curso_novo_e_recusado(int semestre)
    {
        var ana = Ana();
        var adm = new Curso("Administração", "ADM", 8);

        var resultado = ana.TrocarDeCurso(adm, semestre, _coordenacao, _agora);

        Assert.Equal(RecusaDaTrocaDeCurso.SemestreForaDoCurso, resultado.Recusa);
        Assert.Equal((_cc.Id, 3), (ana.CursoId, ana.Semestre));
    }

    [Fact]
    public void O_mesmo_curso_e_recusado()
    {
        var ana = Ana();

        Assert.Equal(RecusaDaTrocaDeCurso.MesmoCurso, ana.TrocarDeCurso(_cc, 5, _coordenacao, _agora).Recusa);
        Assert.Equal(3, ana.Semestre);
    }

    [Fact]
    public void Linha_que_nao_e_de_aluno_nao_tem_email_corrigido_nem_troca_de_curso()
    {
        var renata = CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque");

        Assert.Throws<DomainException>(() => renata.CorrigirEmail("renata.a@prof.unieduk.com.br", _dominios, _coordenacao, _agora));
        Assert.Throws<DomainException>(() => renata.TrocarDeCurso(_cc, 1, _coordenacao, _agora));
    }
}
