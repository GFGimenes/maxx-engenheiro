using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Tests.Catalogo;
using static Maxx.Domain.Tests.Academico.Calendario2026;

namespace Maxx.Domain.Tests.Conquistas;

// O que entra na contagem (regras-conquista.md 3), a partir dos prêmios de verdade: a
// medalha ativa, e o prêmio de participação só depois de definitivo.
public class FatoContavelTests
{
    // 10/11/2026, 10h em São Paulo: bimestre 4, em 2026/2.
    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);
    private static readonly Materia _bancoDeDados = new(_cc, "Banco de Dados");

    private readonly Fatos _fatos = new();
    private readonly Usuario _ana = Usuario.CadastrarAluno(
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3),
        _cc,
        "Ana",
        "anaSouza",
        "hash",
        _agora.AddDays(-90));

    private static Usuario Renata() => Usuario.CriarPeloSeed(
        CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque", true), "renataA", _agora.AddDays(-90));

    private Medalha Medalha()
    {
        var semestre = _fatos.Semestre(2);
        return Domain.Premios.Medalha.Conceder(
            CatalogoDeTeste.M01, 1, _ana, "CC", new Turma(_bancoDeDados, semestre), semestre, Renata(), [], _agora).Medalha!;
    }

    // Sem último semestre encerrado: o teste olha só o semestre em que o prêmio conta, e a
    // trava de data da reivindicação (D3 do passo 8) é coberta nos testes do prêmio.
    private (PremioParticipacao Premio, Evento Evento) Aprovado(DateOnly inicio)
    {
        var evento = Evento.TextoLivre("Palestra sobre LGPD", inicio, inicio, _ana);
        var premio = PremioParticipacao.Reivindicar(
            CatalogoDeTeste.P01, 1, _ana, _cc, evento, null, ultimoEncerrado: null, _agora).Premio!;
        Assert.Null(premio.Aprovar(_ana, Renata(), _agora));
        return (premio, evento);
    }

    [Fact]
    public void Medalha_ativa_e_fato_com_materia_bimestre_semestre_e_ano()
    {
        var medalha = Medalha();

        var fato = FatoContavel.DaMedalha(medalha, _fatos.Calendario);

        Assert.NotNull(fato);
        Assert.Equal((OrigemDoFato.Medalha, medalha.Id, "M01"), (fato.Origem, fato.Id, fato.ItemCodigo));
        Assert.Equal(_bancoDeDados.Id, fato.MateriaId);
        Assert.Equal(_fatos.Bimestre(4).Id, fato.BimestreId);
        Assert.Equal(_fatos.Semestre(2).Id, fato.SemestreLetivoId);
        Assert.Equal(_fatos.Ano.Id, fato.AnoLetivoId);
    }

    [Fact]
    public void Medalha_anulada_deixa_de_ser_contavel()
    {
        var medalha = Medalha();
        medalha.Anular(Guid.CreateVersion7(), _agora.AddDays(1));

        Assert.Null(FatoContavel.DaMedalha(medalha, _fatos.Calendario));
    }

    [Fact]
    public void Medalha_anulada_ainda_e_descrita_para_localizar_as_instancias_que_tocava()
    {
        var medalha = Medalha();
        var ativa = FatoContavel.DaMedalha(medalha, _fatos.Calendario);
        medalha.Anular(Guid.CreateVersion7(), _agora.AddDays(1));

        var descrita = FatoContavel.DescreverMedalha(medalha, _fatos.Calendario);

        Assert.Equal(ativa, descrita);
        Assert.Equal(_fatos.InstanciaDoBimestre(4), descrita.InstanciaEm(Periodo.Bimestre, Fatos.Aluno));
    }

    [Fact]
    public void Premio_de_participacao_so_conta_depois_de_definitivo()
    {
        var (premio, evento) = Aprovado(Data(10, 1));

        Assert.Equal(EstadoDoPremioParticipacao.Valido, premio.Estado);
        Assert.Null(FatoContavel.DoPremio(premio, evento, CatalogoDeTeste.P01, _fatos.Calendario));

        Assert.True(premio.TornarDefinitivoSeAJanelaFechou(_agora.AddDays(15)));
        var fato = FatoContavel.DoPremio(premio, evento, CatalogoDeTeste.P01, _fatos.Calendario);

        Assert.NotNull(fato);
        Assert.Equal((OrigemDoFato.PremioParticipacao, premio.Id, "palestra"), (fato.Origem, fato.Id, fato.TipoDeEvento));
        Assert.Null(fato.MateriaId);
        Assert.Null(fato.BimestreId);
    }

    [Fact]
    public void Premio_em_averiguacao_nao_conta_e_confirmado_conta()
    {
        var (premio, evento) = Aprovado(Data(10, 1));
        var contestacao = premio.Contestar(Renata(), "Revi a foto.", _agora.AddDays(1)).Contestacao!;

        Assert.Null(FatoContavel.DoPremio(premio, evento, CatalogoDeTeste.P01, _fatos.Calendario));

        Assert.Null(premio.Confirmar(contestacao, Renata(), _agora.AddDays(2)));
        Assert.NotNull(FatoContavel.DoPremio(premio, evento, CatalogoDeTeste.P01, _fatos.Calendario));
    }

    [Theory]
    [InlineData(10, 1, 2)]
    [InlineData(7, 15, 2)]
    [InlineData(6, 27, 1)]
    public void Premio_conta_no_semestre_do_inicio_do_evento_e_no_recesso_no_seguinte(int mes, int dia, int semestre)
    {
        var (premio, evento) = Aprovado(Data(mes, dia));
        premio.TornarDefinitivoSeAJanelaFechou(_agora.AddDays(15));

        var fato = FatoContavel.DoPremio(premio, evento, CatalogoDeTeste.P01, _fatos.Calendario)!;

        Assert.Equal(_fatos.Semestre(semestre).Id, fato.SemestreLetivoId);
        Assert.Equal(_fatos.Ano.Id, fato.AnoLetivoId);
    }

    [Fact]
    public void Premio_de_participacao_nao_tem_instancia_de_bimestre()
    {
        var fato = _fatos.Premio("P01", "palestra");

        Assert.Null(fato.InstanciaEm(Periodo.Bimestre, Fatos.Aluno));
        Assert.Equal(_fatos.InstanciaDoSemestre(2), fato.InstanciaEm(Periodo.Semestre, Fatos.Aluno));
        Assert.Equal(_fatos.InstanciaDoAno, fato.InstanciaEm(Periodo.Ano, Fatos.Aluno));
        Assert.Equal(Fatos.InstanciaDoCurso, fato.InstanciaEm(Periodo.Curso, Fatos.Aluno));
    }

    [Fact]
    public void Medalha_tem_instancia_em_todos_os_periodos_de_conquista()
    {
        var fato = _fatos.Medalha("M01", 3);

        Assert.Equal(_fatos.InstanciaDoBimestre(3), fato.InstanciaEm(Periodo.Bimestre, Fatos.Aluno));
        Assert.Equal(_fatos.InstanciaDoSemestre(2), fato.InstanciaEm(Periodo.Semestre, Fatos.Aluno));
        Assert.True(fato.PertenceA(_fatos.InstanciaDoBimestre(3)));
        Assert.False(fato.PertenceA(_fatos.InstanciaDoBimestre(4)));
    }

    [Fact]
    public void Instancia_de_evento_nao_existe()
    {
        Assert.Throws<DomainException>(() => new InstanciaDePeriodo(Periodo.Evento, Guid.CreateVersion7()));
        Assert.Throws<DomainException>(() => new InstanciaDePeriodo(Periodo.Bimestre, Guid.Empty));
    }
}
