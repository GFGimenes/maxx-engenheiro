using Maxx.Domain.Ranking;

namespace Maxx.Domain.Tests.Ranking;

// As quatro visões a partir das parcelas (produto.md 5): soma, ordem, desempate, ex-aluno só
// no histórico, curso atual, aluno sem pontos fora da tabela.
public class ClassificacaoTests
{
    private static readonly DateTimeOffset _inicio = new(2026, 8, 3, 13, 0, 0, TimeSpan.Zero);

    private static readonly Guid _semestre1 = Guid.CreateVersion7();
    private static readonly Guid _semestre2 = Guid.CreateVersion7();
    private static readonly Guid _cc = Guid.CreateVersion7();
    private static readonly Guid _adm = Guid.CreateVersion7();

    private static AlunoDoRanking Aluno(Guid? curso = null, bool exAluno = false) => new(Guid.CreateVersion7(), curso ?? _cc, exAluno);

    private static ParcelaDoRanking Parcela(AlunoDoRanking aluno, int pontos, int diasDepois = 0, Guid? semestre = null) => new()
    {
        AlunoId = aluno.Id,
        Pontos = pontos,
        SemestreLetivoId = semestre ?? _semestre2,
        ConcedidaEm = _inicio.AddDays(diasDepois),
    };

    private static List<LinhaDoRanking> Da(IEnumerable<LinhaDoRanking> linhas, EscopoDoRanking escopo, Guid? curso = null) =>
        [.. linhas.Where(linha => linha.Visao.Escopo == escopo && linha.Visao.CursoId == curso).OrderBy(linha => linha.Posicao)];

    [Fact]
    public void Pontos_somam_por_aluno_e_a_posicao_segue_os_pontos()
    {
        var ana = Aluno();
        var bruno = Aluno();
        var carla = Aluno();

        var linhas = Classificacao.Historico(
            [ana, bruno, carla],
            [Parcela(ana, 30), Parcela(ana, 40), Parcela(bruno, 100), Parcela(carla, 10)]);

        var geral = Da(linhas, EscopoDoRanking.Geral);
        Assert.Equal([(bruno.Id, 1, 100), (ana.Id, 2, 70), (carla.Id, 3, 10)], geral.Select(linha => (linha.AlunoId, linha.Posicao, linha.Pontos)));
        Assert.All(geral, linha => Assert.Equal(new VisaoDoRanking(EscopoDoRanking.Geral, PeriodoDoRanking.Historico, null, null), linha.Visao));
    }

    [Fact]
    public void Empate_em_pontos_coloca_na_frente_quem_parou_de_pontuar_antes()
    {
        var ana = Aluno();
        var bruno = Aluno();

        var linhas = Classificacao.Historico(
            [ana, bruno],
            [Parcela(ana, 30, diasDepois: 1), Parcela(ana, 30, diasDepois: 9), Parcela(bruno, 60, diasDepois: 5)]);

        var geral = Da(linhas, EscopoDoRanking.Geral);
        Assert.Equal([bruno.Id, ana.Id], geral.Select(linha => linha.AlunoId));
        Assert.Equal(_inicio.AddDays(5), geral[0].UltimaConcessaoEm);
        Assert.Equal(_inicio.AddDays(9), geral[1].UltimaConcessaoEm);
    }

    [Fact]
    public void Empate_em_pontos_e_instante_e_decidido_pelo_id_do_aluno()
    {
        var ana = Aluno();
        var bruno = Aluno();
        var ordemPorId = new[] { ana, bruno }.OrderBy(aluno => aluno.Id).Select(aluno => aluno.Id);

        var linhas = Classificacao.Historico([bruno, ana], [Parcela(bruno, 30), Parcela(ana, 30)]);

        Assert.Equal(ordemPorId, Da(linhas, EscopoDoRanking.Geral).Select(linha => linha.AlunoId));
        Assert.Equal([1, 2], Da(linhas, EscopoDoRanking.Geral).Select(linha => linha.Posicao));
    }

    [Fact]
    public void Aluno_sem_pontos_na_visao_nao_tem_linha()
    {
        var ana = Aluno();
        var semPremio = Aluno();
        var soDoOutroSemestre = Aluno();

        var linhas = Classificacao.Semestre(
            _semestre2,
            [ana, semPremio, soDoOutroSemestre],
            [Parcela(ana, 30), Parcela(soDoOutroSemestre, 50, semestre: _semestre1)]);

        Assert.Equal([ana.Id], Da(linhas, EscopoDoRanking.Geral).Select(linha => linha.AlunoId));
    }

    [Fact]
    public void Semestre_conta_so_as_parcelas_dele_e_o_historico_conta_todas()
    {
        var ana = Aluno();
        var parcelas = new[]
        {
            Parcela(ana, 30, semestre: _semestre1),
            Parcela(ana, 40, semestre: _semestre2),
            Parcela(ana, 50) with { SemestreLetivoId = null },
        };

        var doSemestre = Da(Classificacao.Semestre(_semestre2, [ana], parcelas), EscopoDoRanking.Geral).Single();
        var historico = Da(Classificacao.Historico([ana], parcelas), EscopoDoRanking.Geral).Single();

        Assert.Equal(40, doSemestre.Pontos);
        Assert.Equal(new VisaoDoRanking(EscopoDoRanking.Geral, PeriodoDoRanking.Semestre, _semestre2, null), doSemestre.Visao);
        Assert.Equal(120, historico.Pontos);
    }

    [Fact]
    public void Desempate_do_semestre_olha_so_as_concessoes_que_compoem_a_pontuacao_do_semestre()
    {
        var ana = Aluno();
        var bruno = Aluno();

        var linhas = Classificacao.Semestre(
            _semestre2,
            [ana, bruno],
            [Parcela(ana, 30, diasDepois: 2), Parcela(ana, 30, diasDepois: 90, semestre: _semestre1), Parcela(bruno, 30, diasDepois: 5)]);

        var geral = Da(linhas, EscopoDoRanking.Geral);
        Assert.Equal([ana.Id, bruno.Id], geral.Select(linha => linha.AlunoId));
        Assert.Equal(_inicio.AddDays(2), geral[0].UltimaConcessaoEm);
    }

    [Fact]
    public void Ex_aluno_fica_so_no_historico()
    {
        var ana = Aluno();
        var exAluno = Aluno(exAluno: true);
        var parcelas = new[] { Parcela(ana, 30), Parcela(exAluno, 100) };

        var doSemestre = Classificacao.Semestre(_semestre2, [ana, exAluno], parcelas);
        var historico = Classificacao.Historico([ana, exAluno], parcelas);

        Assert.DoesNotContain(doSemestre, linha => linha.AlunoId == exAluno.Id);
        Assert.Equal([exAluno.Id, ana.Id], Da(historico, EscopoDoRanking.Geral).Select(linha => linha.AlunoId));
        Assert.Equal([exAluno.Id, ana.Id], Da(historico, EscopoDoRanking.Curso, _cc).Select(linha => linha.AlunoId));
    }

    [Fact]
    public void Escopo_curso_separa_pelo_curso_atual_com_posicao_propria()
    {
        var ana = Aluno(_cc);
        var bruno = Aluno(_adm);
        var carla = Aluno(_cc);

        var linhas = Classificacao.Semestre(
            _semestre2, [ana, bruno, carla], [Parcela(ana, 30), Parcela(bruno, 100), Parcela(carla, 50)]);

        Assert.Equal([(carla.Id, 1), (ana.Id, 2)], Da(linhas, EscopoDoRanking.Curso, _cc).Select(linha => (linha.AlunoId, linha.Posicao)));
        Assert.Equal([(bruno.Id, 1)], Da(linhas, EscopoDoRanking.Curso, _adm).Select(linha => (linha.AlunoId, linha.Posicao)));
        Assert.Equal([bruno.Id, carla.Id, ana.Id], Da(linhas, EscopoDoRanking.Geral).Select(linha => linha.AlunoId));
    }

    [Fact]
    public void Quem_troca_de_curso_leva_os_pontos_para_o_curso_novo()
    {
        var ana = Aluno(_cc);
        var parcelas = new[] { Parcela(ana, 30, semestre: _semestre1), Parcela(ana, 40) };
        var depoisDaTroca = ana with { CursoId = _adm };

        var historico = Classificacao.Historico([depoisDaTroca], parcelas);

        Assert.Empty(Da(historico, EscopoDoRanking.Curso, _cc));
        Assert.Equal(70, Da(historico, EscopoDoRanking.Curso, _adm).Single().Pontos);
    }

    [Fact]
    public void Parcela_de_quem_nao_esta_entre_os_alunos_fica_de_fora()
    {
        var ana = Aluno();
        var semConta = Aluno();

        var linhas = Classificacao.Historico([ana], [Parcela(ana, 30), Parcela(semConta, 100)]);

        Assert.Equal([ana.Id], Da(linhas, EscopoDoRanking.Geral).Select(linha => linha.AlunoId));
    }
}
