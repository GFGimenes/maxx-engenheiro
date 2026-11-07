using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Tests.Academico;
using Maxx.Domain.Tests.Catalogo;

namespace Maxx.Domain.Tests.Premios;

public class MedalhaTests
{
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);
    private static readonly Materia _bancoDeDados = new(_cc, "Banco de Dados");
    private static readonly Materia _redes = new(_cc, "Redes");
    private static readonly SemestreLetivo _semestre = Calendario2026.Segundo();
    private static readonly Turma _turma = new(_bancoDeDados, _semestre);
    private static readonly Turma _turmaDeRedes = new(_redes, _semestre);

    // 10/11/2026, 10h em São Paulo: dentro do bimestre 4.
    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);

    private static readonly Usuario _professor = Usuario.CriarPeloSeed(
        CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), "renataA", _agora.AddDays(-90));

    private static Usuario Aluna() => Usuario.CadastrarAluno(
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3),
        _cc,
        "Ana",
        "anaSouza",
        "hash",
        _agora.AddDays(-60));

    private static ResultadoDaConcessao Conceder(
        ItemMedalha item,
        Usuario aluno,
        IEnumerable<Medalha>? existentes = null,
        DateTimeOffset? agora = null,
        Turma? turma = null,
        string curso = "CC") =>
        Medalha.Conceder(
            item, 3, aluno, curso, turma ?? _turma, _semestre, _professor, existentes ?? [], agora ?? _agora);

    [Fact]
    public void Concessao_grava_turma_materia_bimestre_e_congela_tier_pontos_e_versao()
    {
        var ana = Aluna();

        var resultado = Conceder(CatalogoDeTeste.M01, ana);

        var medalha = Assert.IsType<Medalha>(resultado.Medalha);
        Assert.Null(resultado.Recusa);
        Assert.Equal(ana.Id, medalha.AlunoId);
        Assert.Equal("M01", medalha.ItemCodigo);
        Assert.Equal(3, medalha.VersaoDoItem);
        Assert.Equal(Tier.Bronze, medalha.Tier);
        Assert.Equal(30, medalha.Pontos);
        Assert.Equal(_turma.Id, medalha.TurmaId);
        Assert.Equal(_bancoDeDados.Id, medalha.MateriaId);
        Assert.Equal(_semestre.Id, medalha.SemestreLetivoId);
        Assert.Equal(_semestre.Bimestres[1].Id, medalha.BimestreId);
        Assert.Equal(1, medalha.Ocorrencia);
        Assert.Equal(_professor.Id, medalha.ConcedidaPorId);
        Assert.Equal(_agora, medalha.ConcedidaEm);
        Assert.True(medalha.EstaAtiva);
    }

    [Fact]
    public void Item_repetivel_ocupa_a_proxima_ocorrencia_ate_o_limite()
    {
        var ana = Aluna();
        var primeira = Conceder(CatalogoDeTeste.M01, ana).Medalha!;
        var segunda = Conceder(CatalogoDeTeste.M01, ana, [primeira]).Medalha!;

        var terceira = Conceder(CatalogoDeTeste.M01, ana, [primeira, segunda]);

        Assert.Equal(2, segunda.Ocorrencia);
        Assert.NotEqual(primeira.ChaveDeDeduplicacao, segunda.ChaveDeDeduplicacao);
        Assert.Null(terceira.Medalha);
        Assert.Equal(RecusaDaConcessao.LimiteAtingido, terceira.Recusa);
    }

    [Fact]
    public void Item_nao_repetivel_vale_uma_vez_por_materia_e_bimestre()
    {
        var ana = Aluna();
        var presenca = Conceder(CatalogoDeTeste.M03, ana).Medalha!;

        Assert.Equal(RecusaDaConcessao.LimiteAtingido, Conceder(CatalogoDeTeste.M03, ana, [presenca]).Recusa);
        Assert.DoesNotContain("ocorrencia", presenca.ChaveDeDeduplicacao, StringComparison.Ordinal);
    }

    [Fact]
    public void O_limite_e_por_materia()
    {
        var ana = Aluna();
        var primeira = Conceder(CatalogoDeTeste.M03, ana).Medalha!;

        var emRedes = Conceder(CatalogoDeTeste.M03, ana, [primeira], turma: _turmaDeRedes);

        Assert.Equal(_redes.Id, emRedes.Medalha!.MateriaId);
    }

    [Fact]
    public void O_limite_e_por_bimestre_no_item_de_bimestre()
    {
        var ana = Aluna();
        var noTerceiro = Conceder(CatalogoDeTeste.M03, ana, agora: Instante(9, 15)).Medalha!;

        var noQuarto = Conceder(CatalogoDeTeste.M03, ana, [noTerceiro]);

        Assert.Equal(_semestre.Bimestres[0].Id, noTerceiro.BimestreId);
        Assert.Equal(_semestre.Bimestres[1].Id, noQuarto.Medalha!.BimestreId);
    }

    [Fact]
    public void O_limite_e_por_semestre_no_item_de_semestre_e_o_bimestre_e_o_da_data()
    {
        var ana = Aluna();
        var noTerceiro = Conceder(CatalogoDeTeste.M10, ana, agora: Instante(9, 15)).Medalha!;

        var noQuarto = Conceder(CatalogoDeTeste.M10, ana, [noTerceiro]);

        Assert.Equal(_semestre.Bimestres[0].Id, noTerceiro.BimestreId);
        Assert.Equal(Tier.Prata, noTerceiro.Tier);
        Assert.Equal(RecusaDaConcessao.LimiteAtingido, noQuarto.Recusa);
        Assert.Contains($"semestre:{_semestre.Id}", noTerceiro.ChaveDeDeduplicacao, StringComparison.Ordinal);
    }

    [Fact]
    public void Medalha_anulada_libera_a_vaga()
    {
        var ana = Aluna();
        var primeira = Conceder(CatalogoDeTeste.M01, ana).Medalha!;
        var segunda = Conceder(CatalogoDeTeste.M01, ana, [primeira]).Medalha!;
        primeira.Anular(Guid.CreateVersion7(), _agora.AddHours(1));

        var nova = Conceder(CatalogoDeTeste.M01, ana, [primeira, segunda], agora: _agora.AddHours(2)).Medalha!;

        Assert.Equal(1, nova.Ocorrencia);
        Assert.Equal(primeira.ChaveDeDeduplicacao, nova.ChaveDeDeduplicacao);
    }

    [Fact]
    public void Anulacao_grava_quem_e_quando_e_vale_uma_vez()
    {
        var medalha = Conceder(CatalogoDeTeste.M01, Aluna()).Medalha!;
        var instituicao = Guid.CreateVersion7();
        var quando = _agora.AddDays(1);

        Assert.True(medalha.Anular(instituicao, quando));
        Assert.False(medalha.Anular(Guid.CreateVersion7(), quando.AddDays(1)));

        Assert.False(medalha.EstaAtiva);
        Assert.Equal(instituicao, medalha.AnuladaPorId);
        Assert.Equal(quando, medalha.AnuladaEm);
    }

    [Fact]
    public void Ex_aluno_nao_recebe_medalha()
    {
        var ana = Aluna();
        ana.MarcarExAluno(_agora.AddDays(-1));

        Assert.Equal(RecusaDaConcessao.ExAluno, Conceder(CatalogoDeTeste.M01, ana).Recusa);
    }

    [Fact]
    public void Data_do_semestre_entre_dois_bimestres_e_recusada()
    {
        // 27/09: depois do bimestre 3 (até 26/09) e antes do 4 (desde 28/09), dentro do semestre.
        var entreBimestres = Instante(9, 27);

        Assert.True(_semestre.Contem(FusoHorario.DataLocal(entreBimestres)));
        Assert.Equal(
            RecusaDaConcessao.ForaDeBimestre, Conceder(CatalogoDeTeste.M01, Aluna(), agora: entreBimestres).Recusa);
    }

    [Fact]
    public void Fora_do_semestre_da_turma_e_recusada()
    {
        Assert.Equal(
            RecusaDaConcessao.ForaDoSemestreDaTurma,
            Conceder(CatalogoDeTeste.M01, Aluna(), agora: Instante(12, 6)).Recusa);
    }

    [Fact]
    public void A_fronteira_do_bimestre_e_a_data_em_sao_paulo()
    {
        // 05/12 às 23h30 em São Paulo já é 06/12 em UTC: ainda vale o último dia do bimestre 4.
        var ultimoMinuto = new DateTimeOffset(2026, 12, 6, 2, 30, 0, TimeSpan.Zero);
        // 26/09 às 23h30 em São Paulo, último dia do bimestre 3.
        var fimDoTerceiro = new DateTimeOffset(2026, 9, 27, 2, 30, 0, TimeSpan.Zero);

        Assert.Equal(_semestre.Bimestres[1].Id, Conceder(CatalogoDeTeste.M01, Aluna(), agora: ultimoMinuto).Medalha!.BimestreId);
        Assert.Equal(_semestre.Bimestres[0].Id, Conceder(CatalogoDeTeste.M01, Aluna(), agora: fimDoTerceiro).Medalha!.BimestreId);
    }

    [Fact]
    public void Item_fora_da_vigencia_e_recusado()
    {
        var vencido = CatalogoDeTeste.M01 with { Vigencia = new Vigencia(null, new DateOnly(2026, 10, 31)) };
        var futuro = CatalogoDeTeste.M01 with { Vigencia = new Vigencia(new DateOnly(2026, 12, 1), null) };

        Assert.Equal(RecusaDaConcessao.ItemForaDeVigencia, Conceder(vencido, Aluna()).Recusa);
        Assert.Equal(RecusaDaConcessao.ItemForaDeVigencia, Conceder(futuro, Aluna()).Recusa);
    }

    [Fact]
    public void Item_restrito_a_outro_curso_e_recusado()
    {
        var deAdm = CatalogoDeTeste.M01 with { RestricaoCurso = "ADM" };

        Assert.Equal(RecusaDaConcessao.ItemDeOutroCurso, Conceder(deAdm, Aluna()).Recusa);
        Assert.NotNull(Conceder(deAdm, Aluna(), curso: "ADM").Medalha);
    }

    [Fact]
    public void Quem_concede_precisa_ser_professor()
    {
        var instituicao = Usuario.CriarPeloSeed(
            CadastroElegivel.Instituicao("secretaria@unieduk.com.br", "Secretaria"), "secretaria", _agora.AddDays(-90));

        Assert.Throws<DomainException>(() => Medalha.Conceder(
            CatalogoDeTeste.M01, 1, Aluna(), "CC", _turma, _semestre, instituicao, [], _agora));
    }

    [Fact]
    public void Quem_recebe_precisa_ser_aluno()
    {
        Assert.Throws<DomainException>(() => Medalha.Conceder(
            CatalogoDeTeste.M01, 1, _professor, "CC", _turma, _semestre, _professor, [], _agora));
    }

    [Fact]
    public void Medalhas_existentes_de_outro_aluno_ou_item_sao_erro_de_quem_chama()
    {
        var ana = Aluna();
        var deOutraAluna = Conceder(CatalogoDeTeste.M01, Aluna()).Medalha!;
        var deOutroItem = Conceder(CatalogoDeTeste.M03, ana).Medalha!;

        Assert.Throws<ArgumentException>(() => Conceder(CatalogoDeTeste.M01, ana, [deOutraAluna]));
        Assert.Throws<ArgumentException>(() => Conceder(CatalogoDeTeste.M01, ana, [deOutroItem]));
    }

    [Fact]
    public void Semestre_que_nao_e_o_da_turma_e_erro_de_quem_chama()
    {
        Assert.Throws<ArgumentException>(() => Medalha.Conceder(
            CatalogoDeTeste.M01, 1, Aluna(), "CC", _turma, Calendario2026.Primeiro(), _professor, [], _agora));
    }

    // Meio-dia em São Paulo do dia dado de 2026.
    private static DateTimeOffset Instante(int mes, int dia) => new(2026, mes, dia, 15, 0, 0, TimeSpan.Zero);
}
