using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Ranking;
using Maxx.Domain.Tests.Catalogo;
using Maxx.Domain.Tests.Conquistas;
using static Maxx.Domain.Tests.Academico.Calendario2026;

namespace Maxx.Domain.Tests.Ranking;

// O que pontua no ranking e em que semestre letivo conta (produto.md 4.9 e 5): a medalha no
// semestre da concessão, o prêmio de participação no do início do evento, a conquista no da
// instância dela.
public class ParcelaDoRankingTests
{
    // 10/11/2026, 10h em São Paulo: bimestre 4, em 2026/2.
    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);

    // 21/12/2026, 10h em São Paulo: recesso, depois do fim de 2026/2 (05/12).
    private static readonly DateTimeOffset _noRecesso = new(2026, 12, 21, 13, 0, 0, TimeSpan.Zero);

    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);
    private static readonly Materia _bancoDeDados = new(_cc, "Banco de Dados");

    private readonly Fatos _fatos = new();
    private readonly AvaliadorDeConquista _avaliador = new();
    private readonly Usuario _ana = Usuario.CadastrarAluno(
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3),
        _cc,
        "Ana",
        "anaSouza",
        "hash",
        _agora.AddDays(-90));

    private static Usuario Renata() => Usuario.CriarPeloSeed(
        CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque", true), "renataA", _agora.AddDays(-90));

    private static Usuario Coordenacao() => Usuario.CriarPeloSeed(
        CadastroElegivel.Instituicao("coordenacao@unieduk.com.br", "Coordenação UniMax"), "coordenacao", _agora.AddDays(-90));

    // O calendário de 2026 com o ano seguinte, para o recesso ter semestre seguinte.
    private Calendario ComOAnoSeguinte() => new([_fatos.Ano, Ano2027()]);

    private static AnoLetivo Ano2027() => new(
        2027,
        new SemestreLetivo(2027, 1, new Bimestre(1, new(2027, 2, 1), new(2027, 4, 3)), new Bimestre(2, new(2027, 4, 5), new(2027, 6, 26))),
        new SemestreLetivo(2027, 2, new Bimestre(3, new(2027, 7, 26), new(2027, 9, 25)), new Bimestre(4, new(2027, 9, 27), new(2027, 12, 4))));

    private Medalha Medalha()
    {
        var semestre = _fatos.Semestre(2);
        return Domain.Premios.Medalha.Conceder(
            CatalogoDeTeste.M01, 1, _ana, "CC", new Turma(_bancoDeDados, semestre), semestre, Renata(), [], _agora).Medalha!;
    }

    private (PremioParticipacao Premio, Evento Evento) Reivindicado(DateOnly inicio, DateTimeOffset agora)
    {
        var evento = Evento.TextoLivre("Palestra sobre LGPD", inicio, inicio, _ana);
        var premio = PremioParticipacao.Reivindicar(
            CatalogoDeTeste.P01, 1, _ana, _cc, evento, null, ultimoEncerrado: null, agora).Premio!;
        return (premio, evento);
    }

    private (PremioParticipacao Premio, Evento Evento) Aprovado(DateOnly inicio, DateTimeOffset? agora = null)
    {
        var quando = agora ?? _agora;
        var (premio, evento) = Reivindicado(inicio, quando);
        Assert.Null(premio.Aprovar(_ana, Renata(), quando));
        return (premio, evento);
    }

    private Conquista ConquistaDe(Periodo periodo, InstanciaDePeriodo instancia, DateTimeOffset concedidaEm, int bimestreDasMedalhas = 4)
    {
        var item = CatalogoDeTeste.C02 with { Periodo = periodo };
        var medalhas = _fatos.Medalhas("M01", bimestreDasMedalhas, 3).ToList();
        return Conquista.Conceder(item, 1, Fatos.Aluno, instancia, _avaliador.Avaliar(item, instancia, medalhas, Fatos.Contexto()), concedidaEm);
    }

    [Fact]
    public void Medalha_ativa_conta_os_pontos_congelados_no_semestre_e_no_instante_da_concessao()
    {
        var medalha = Medalha();

        var parcela = ParcelaDoRanking.DaMedalha(medalha);

        Assert.NotNull(parcela);
        Assert.Equal((_ana.Id, 30, _fatos.Semestre(2).Id, _agora), (parcela.AlunoId, parcela.Pontos, parcela.SemestreLetivoId, parcela.ConcedidaEm));
    }

    [Fact]
    public void Medalha_anulada_nao_pontua()
    {
        var medalha = Medalha();
        medalha.Anular(Guid.CreateVersion7(), _agora.AddDays(1));

        Assert.Null(ParcelaDoRanking.DaMedalha(medalha));
    }

    [Fact]
    public void Premio_valido_conta_no_semestre_do_inicio_do_evento_e_nao_no_da_aprovacao()
    {
        var (premio, evento) = Aprovado(Data(6, 20));

        var parcela = ParcelaDoRanking.DoPremio(premio, evento, _fatos.Calendario);

        Assert.NotNull(parcela);
        Assert.Equal((_ana.Id, premio.Pontos, _fatos.Semestre(1).Id, _agora), (parcela.AlunoId, parcela.Pontos, parcela.SemestreLetivoId, parcela.ConcedidaEm));
    }

    [Fact]
    public void Evento_que_comeca_no_recesso_conta_no_semestre_seguinte()
    {
        var (premio, evento) = Aprovado(Data(7, 10));

        Assert.Equal(_fatos.Semestre(2).Id, ParcelaDoRanking.DoPremio(premio, evento, _fatos.Calendario)!.SemestreLetivoId);
    }

    [Fact]
    public void Evento_no_recesso_sem_o_semestre_seguinte_no_calendario_pontua_sem_semestre_ate_o_calendario_ganhar_o_semestre()
    {
        var (premio, evento) = Aprovado(Data(12, 15), _noRecesso);

        var calendario = ComOAnoSeguinte();

        var semSeguinte = ParcelaDoRanking.DoPremio(premio, evento, _fatos.Calendario);
        var comSeguinte = ParcelaDoRanking.DoPremio(premio, evento, calendario);

        Assert.NotNull(semSeguinte);
        Assert.Null(semSeguinte.SemestreLetivoId);
        Assert.Equal(calendario.Anos[1].Semestres[0].Id, comSeguinte!.SemestreLetivoId);
    }

    [Fact]
    public void Premio_reivindicado_ou_reprovado_nao_pontua()
    {
        var (pendente, evento) = Reivindicado(Data(10, 1), _agora);
        var (reprovado, doReprovado) = Reivindicado(Data(10, 2), _agora);
        Assert.Null(reprovado.Reprovar(Renata(), "Foto de outro evento.", _agora));

        Assert.Null(ParcelaDoRanking.DoPremio(pendente, evento, _fatos.Calendario));
        Assert.Null(ParcelaDoRanking.DoPremio(reprovado, doReprovado, _fatos.Calendario));
    }

    [Fact]
    public void Premio_em_averiguacao_nao_pontua_e_revogado_tambem_nao()
    {
        var (premio, evento) = Aprovado(Data(10, 1));
        var contestacao = premio.Contestar(Renata(), "A foto não mostra o aluno.", _agora.AddDays(1)).Contestacao!;

        Assert.Null(ParcelaDoRanking.DoPremio(premio, evento, _fatos.Calendario));

        Assert.Null(premio.Revogar(Coordenacao(), "Foto de outro evento.", contestacao, _agora.AddDays(2)));
        Assert.Null(ParcelaDoRanking.DoPremio(premio, evento, _fatos.Calendario));
    }

    [Fact]
    public void Contestacao_confirmada_volta_a_pontuar_com_o_instante_da_aprovacao()
    {
        var (premio, evento) = Aprovado(Data(10, 1));
        var contestacao = premio.Contestar(Renata(), "A foto não mostra o aluno.", _agora.AddDays(1)).Contestacao!;
        Assert.Null(premio.Confirmar(contestacao, Coordenacao(), _agora.AddDays(2)));

        var parcela = ParcelaDoRanking.DoPremio(premio, evento, _fatos.Calendario);

        Assert.Equal(_agora, parcela!.ConcedidaEm);
    }

    [Fact]
    public void Premio_definitivo_pelo_fim_da_janela_continua_pontuando_com_o_instante_da_aprovacao()
    {
        var (premio, evento) = Aprovado(Data(10, 1));
        Assert.True(premio.TornarDefinitivoSeAJanelaFechou(_agora.AddDays(15)));

        Assert.Equal(_agora, ParcelaDoRanking.DoPremio(premio, evento, _fatos.Calendario)!.ConcedidaEm);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    public void Conquista_de_bimestre_conta_no_semestre_do_bimestre(int bimestre, int semestre)
    {
        var conquista = ConquistaDe(Periodo.Bimestre, _fatos.InstanciaDoBimestre(bimestre), _agora, bimestre);

        Assert.Equal(_fatos.Semestre(semestre).Id, ParcelaDoRanking.DaConquista(conquista, _fatos.Calendario)!.SemestreLetivoId);
    }

    [Fact]
    public void Conquista_de_semestre_conta_no_proprio_semestre_mesmo_concedida_depois()
    {
        var conquista = ConquistaDe(Periodo.Semestre, _fatos.InstanciaDoSemestre(1), _agora, bimestreDasMedalhas: 2);

        var parcela = ParcelaDoRanking.DaConquista(conquista, _fatos.Calendario);

        Assert.Equal((Fatos.Aluno, 40, _fatos.Semestre(1).Id, _agora), (parcela!.AlunoId, parcela.Pontos, parcela.SemestreLetivoId, parcela.ConcedidaEm));
    }

    [Fact]
    public void Conquista_de_ano_conta_no_segundo_semestre_do_ano()
    {
        var conquista = ConquistaDe(Periodo.Ano, _fatos.InstanciaDoAno, _noRecesso);

        Assert.Equal(_fatos.Semestre(2).Id, ParcelaDoRanking.DaConquista(conquista, _fatos.Calendario)!.SemestreLetivoId);
    }

    [Fact]
    public void Conquista_de_curso_conta_no_semestre_da_concessao()
    {
        var conquista = ConquistaDe(Periodo.Curso, Fatos.InstanciaDoCurso, _agora);

        Assert.Equal(_fatos.Semestre(2).Id, ParcelaDoRanking.DaConquista(conquista, _fatos.Calendario)!.SemestreLetivoId);
    }

    [Fact]
    public void Conquista_de_curso_concedida_no_recesso_conta_no_semestre_seguinte()
    {
        var conquista = ConquistaDe(Periodo.Curso, Fatos.InstanciaDoCurso, _noRecesso);
        var calendario = ComOAnoSeguinte();

        Assert.Equal(calendario.Anos[1].Semestres[0].Id, ParcelaDoRanking.DaConquista(conquista, calendario)!.SemestreLetivoId);
        Assert.Null(ParcelaDoRanking.DaConquista(conquista, _fatos.Calendario)!.SemestreLetivoId);
    }

    [Fact]
    public void Conquista_removida_nao_pontua()
    {
        var conquista = ConquistaDe(Periodo.Bimestre, _fatos.InstanciaDoBimestre(4), _agora);
        var insatisfeita = _avaliador.Avaliar(
            CatalogoDeTeste.C02, _fatos.InstanciaDoBimestre(4), _fatos.Medalhas("M01", 4, 2), Fatos.Contexto());
        Assert.True(conquista.Recalcular(insatisfeita, _agora.AddDays(1)));

        Assert.Null(ParcelaDoRanking.DaConquista(conquista, _fatos.Calendario));
    }
}
