using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Tests.Catalogo;
using static Maxx.Domain.Tests.Academico.Calendario2026;

namespace Maxx.Domain.Tests.Premios;

public class EventoTests
{
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);
    private static readonly SemestreLetivo _semestre = Segundo();

    private static Usuario Aluna() => Usuario.CadastrarAluno(
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3),
        _cc,
        "Ana",
        "anaSouza",
        "hash",
        new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero));

    private static Evento Semana(DateOnly? inicio = null, DateOnly? fim = null, Curso? curso = null) => Evento.Semeado(
        "semana-academica-cc-2026-2",
        "Semana Acadêmica de Ciência da Computação",
        inicio ?? Data(9, 14),
        fim ?? Data(9, 18),
        [CatalogoDeTeste.P06, CatalogoDeTeste.P01],
        curso);

    [Fact]
    public void Semeado_guarda_codigo_datas_curso_e_itens_em_ordem()
    {
        var evento = Semana(curso: _cc);

        Assert.Equal("semana-academica-cc-2026-2", evento.Codigo);
        Assert.Equal(OrigemDoEvento.Semeado, evento.Origem);
        Assert.Equal((Data(9, 14), Data(9, 18)), (evento.Inicio, evento.Fim));
        Assert.Equal(["P01", "P06"], evento.ItensAceitos);
        Assert.Equal(_cc.Id, evento.CursoId);
        Assert.Null(evento.CriadoPorId);
    }

    [Fact]
    public void Texto_livre_guarda_quem_digitou_e_nao_tem_codigo_curso_nem_lista()
    {
        var ana = Aluna();

        var evento = Evento.TextoLivre("  Palestra sobre LGPD  ", Data(10, 1), Data(10, 1), ana);

        Assert.Equal("Palestra sobre LGPD", evento.Nome);
        Assert.Equal(OrigemDoEvento.TextoLivre, evento.Origem);
        Assert.Null(evento.Codigo);
        Assert.Null(evento.CursoId);
        Assert.Empty(evento.ItensAceitos);
        Assert.Equal(ana.Id, evento.CriadoPorId);
    }

    [Fact]
    public void Evento_de_um_dia_so_vale()
    {
        var evento = Semana(Data(9, 14), Data(9, 14));

        Assert.Equal(evento.Inicio, evento.Fim);
    }

    [Fact]
    public void Fim_antes_do_inicio_e_recusado()
    {
        var falha = Assert.Throws<DomainException>(() => Semana(Data(9, 18), Data(9, 14)));

        Assert.Contains("termina", falha.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TI")]
    public void Nome_vazio_ou_curto_e_recusado(string nome)
    {
        Assert.Throws<DomainException>(() => Evento.TextoLivre(nome, Data(10, 1), Data(10, 1), Aluna()));
    }

    [Fact]
    public void Nome_conta_emoji_como_um_e_vai_ate_120()
    {
        var limite = new string('a', 119) + "\U0001F393";

        Assert.Equal(limite, Evento.TextoLivre(limite, Data(10, 1), Data(10, 1), Aluna()).Nome);
        Assert.Throws<DomainException>(() => Evento.TextoLivre(limite + "a", Data(10, 1), Data(10, 1), Aluna()));
    }

    [Theory]
    [InlineData("Semana-CC")]
    [InlineData("semana_cc")]
    [InlineData("1-semana")]
    [InlineData("semana-")]
    [InlineData("semana--cc")]
    [InlineData("semana acadêmica")]
    public void Codigo_fora_da_forma_e_recusado(string codigo)
    {
        Assert.Throws<DomainException>(() =>
            Evento.Semeado(codigo, "Semana Acadêmica", Data(9, 14), Data(9, 18), [CatalogoDeTeste.P06], null));
    }

    [Fact]
    public void Semeado_sem_item_e_recusado()
    {
        Assert.Throws<DomainException>(() =>
            Evento.Semeado("semana", "Semana Acadêmica", Data(9, 14), Data(9, 18), [], null));
    }

    [Fact]
    public void Semeado_com_item_repetido_e_recusado()
    {
        Assert.Throws<DomainException>(() => Evento.Semeado(
            "semana", "Semana Acadêmica", Data(9, 14), Data(9, 18), [CatalogoDeTeste.P06, CatalogoDeTeste.P06], null));
    }

    [Fact]
    public void Semeado_nao_aceita_o_item_que_deduplica_por_materia()
    {
        var falha = Assert.Throws<DomainException>(() => Evento.Semeado(
            "aula", "Aula de Banco de Dados", Data(9, 14), Data(9, 14), [CatalogoDeTeste.P17], null));

        Assert.Contains("P17", falha.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Semeado_aceita_so_os_itens_da_lista()
    {
        var evento = Semana();

        Assert.True(evento.Aceita(CatalogoDeTeste.P06));
        Assert.False(evento.Aceita(CatalogoDeTeste.P17));
    }

    [Fact]
    public void Texto_livre_aceita_qualquer_item_inclusive_o_de_sala()
    {
        var evento = Evento.TextoLivre("Apresentação de Banco de Dados", Data(10, 1), Data(10, 1), Aluna());

        Assert.True(evento.Aceita(CatalogoDeTeste.P01));
        Assert.True(evento.Aceita(CatalogoDeTeste.P17));
    }

    [Fact]
    public void Evento_sem_curso_e_aberto_a_todos_e_o_de_curso_so_ao_dele()
    {
        var outro = new Curso("Administração", "ADM", 8);

        Assert.True(Semana().AbertoAoCurso(outro.Id));
        Assert.True(Semana(curso: _cc).AbertoAoCurso(_cc.Id));
        Assert.False(Semana(curso: _cc).AbertoAoCurso(outro.Id));
        Assert.False(Semana(curso: _cc).AbertoAoCurso(null));
    }

    // 2026/1 vai de 26/01 a 27/06, e 2026/2, de 27/07 a 05/12. O limite inferior é o fim do
    // último semestre encerrado antes de hoje.
    public static TheoryData<DateOnly, DateOnly, DateOnly, RecusaDoEvento?> Reivindicacao => new()
    {
        // Começou hoje, e o fim pode estar adiante.
        { Data(9, 14), Data(9, 18), Data(9, 14), null },
        { Data(9, 14), Data(9, 18), Data(11, 8), null },

        // Em 2026/2: no último dia de 2026/1, não; no dia seguinte, já no recesso de julho, sim.
        { Data(6, 27), Data(6, 27), Data(8, 1), RecusaDoEvento.DeSemestreEncerrado },
        { Data(6, 28), Data(6, 28), Data(8, 1), null },
        { Data(5, 10), Data(5, 10), Data(11, 8), RecusaDoEvento.DeSemestreEncerrado },

        // Começou no fim de 2026/1 e terminou em 2026/2: conta o início.
        { Data(6, 20), Data(8, 10), Data(8, 15), RecusaDoEvento.DeSemestreEncerrado },

        // Ainda não começou.
        { Data(11, 20), Data(11, 20), Data(11, 19), RecusaDoEvento.NaoComecou },
    };

    [Theory]
    [MemberData(nameof(Reivindicacao))]
    public void Reivindicavel_se_ja_comecou_e_depois_do_fim_do_semestre_anterior(
        DateOnly inicio, DateOnly fim, DateOnly hoje, RecusaDoEvento? esperada)
    {
        Assert.Equal(esperada, Semana(inicio, fim).RecusaParaReivindicarEm(hoje, Primeiro()));
    }

    [Fact]
    public void No_recesso_o_evento_do_recesso_vale_e_o_do_semestre_que_acabou_nao()
    {
        // 10/12/2026: 2026/2 acabou em 05/12, e é o último encerrado.
        var hoje = Data(12, 10);

        Assert.Null(Semana(Data(12, 6), Data(12, 8)).RecusaParaReivindicarEm(hoje, _semestre));
        Assert.Equal(RecusaDoEvento.DeSemestreEncerrado, Semana(Data(12, 5), Data(12, 5)).RecusaParaReivindicarEm(hoje, _semestre));
        Assert.Equal(RecusaDoEvento.DeSemestreEncerrado, Semana().RecusaParaReivindicarEm(hoje, _semestre));
    }

    [Fact]
    public void Sem_semestre_encerrado_nao_ha_limite_inferior()
    {
        Assert.Null(Semana(Data(1, 5), Data(1, 5)).RecusaParaReivindicarEm(Data(2, 1), null));
    }

    [Fact]
    public void Semestre_que_ainda_nao_terminou_nao_serve_de_limite()
    {
        Assert.Throws<DomainException>(() => Semana().RecusaParaReivindicarEm(Data(11, 8), _semestre));
        Assert.Throws<DomainException>(() => Semana().RecusaParaReivindicarEm(Data(12, 5), _semestre));
    }
}
