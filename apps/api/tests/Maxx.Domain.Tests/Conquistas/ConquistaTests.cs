using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Tests.Catalogo;
using static Maxx.Domain.Tests.Conquistas.Fatos;

namespace Maxx.Domain.Tests.Conquistas;

public class ConquistaTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);

    private readonly Fatos _fatos = new();
    private readonly AvaliadorDeConquista _avaliador = new();

    private Avaliacao Avaliar(IEnumerable<FatoContavel> fatos) =>
        _avaliador.Avaliar(CatalogoDeTeste.C02, _fatos.InstanciaDoBimestre(4), fatos, Contexto());

    private Conquista Concedida(IReadOnlyList<FatoContavel> fatos) =>
        Conquista.Conceder(CatalogoDeTeste.C02, 2, Aluno, _fatos.InstanciaDoBimestre(4), Avaliar(fatos), _agora);

    [Fact]
    public void Concessao_congela_tier_pontos_e_versao_e_guarda_instancia_e_evidencias()
    {
        var medalhas = _fatos.Medalhas("M01", 4, 3).ToList();

        var conquista = Concedida(medalhas);

        Assert.Equal((Aluno, "C02", 2), (conquista.AlunoId, conquista.ItemCodigo, conquista.VersaoDoItem));
        Assert.Equal((Tier.Prata, 40), (conquista.Tier, conquista.Pontos));
        Assert.Equal(_fatos.InstanciaDoBimestre(4), conquista.Instancia);
        Assert.Equal(_agora, conquista.ConcedidaEm);
        Assert.True(conquista.EstaAtiva);
        Assert.Equal(medalhas.Select(fato => fato.Id).Order(), conquista.Evidencias.Select(evidencia => evidencia.FatoId).Order());
        Assert.All(conquista.Evidencias, evidencia =>
        {
            Assert.Equal(conquista.Id, evidencia.ConquistaId);
            Assert.Equal(OrigemDoFato.Medalha, evidencia.Origem);
            Assert.Null(evidencia.PremioParticipacaoId);
        });
    }

    [Fact]
    public void Evidencia_de_premio_de_participacao_aponta_so_para_o_premio()
    {
        var premios = _fatos.Premios("P01", "palestra", 3).ToList();
        var c05 = CatalogoDeTeste.C06 with
        {
            Codigo = "C05",
            Regra = new() { Forma = FormaDeContagem.Contagem, Fonte = FonteDeContagem.PremioParticipacao, Alvo = AlvoDeConquista.De(3) },
        };
        var instancia = _fatos.InstanciaDoSemestre(2);

        var conquista = Conquista.Conceder(c05, 1, Aluno, instancia, _avaliador.Avaliar(c05, instancia, premios, Contexto()), _agora);

        Assert.All(conquista.Evidencias, evidencia =>
        {
            Assert.Equal(OrigemDoFato.PremioParticipacao, evidencia.Origem);
            Assert.Null(evidencia.MedalhaId);
        });
    }

    [Fact]
    public void Conceder_fora_do_criterio_e_erro_de_quem_chama()
    {
        var abaixo = Avaliar(_fatos.Medalhas("M01", 4, 2));

        Assert.Throws<DomainException>(() =>
            Conquista.Conceder(CatalogoDeTeste.C02, 1, Aluno, _fatos.InstanciaDoBimestre(4), abaixo, _agora));
    }

    [Fact]
    public void Conceder_em_instancia_de_outro_periodo_e_erro()
    {
        var satisfeita = Avaliar(_fatos.Medalhas("M01", 4, 3));

        Assert.Throws<DomainException>(() =>
            Conquista.Conceder(CatalogoDeTeste.C02, 1, Aluno, _fatos.InstanciaDoSemestre(2), satisfeita, _agora));
    }

    [Fact]
    public void Recalculo_insatisfeito_remove_e_registra_quando()
    {
        var medalhas = _fatos.Medalhas("M01", 4, 3).ToList();
        var conquista = Concedida(medalhas);

        var removida = conquista.Recalcular(Avaliar(medalhas.Skip(1)), _agora.AddDays(3));

        Assert.True(removida);
        Assert.False(conquista.EstaAtiva);
        Assert.Equal(_agora.AddDays(3), conquista.RemovidaEm);
    }

    [Fact]
    public void Recalculo_ainda_satisfeito_mantem_e_troca_as_evidencias_pelo_conjunto_atual()
    {
        var medalhas = _fatos.Medalhas("M01", 4, 3).ToList();
        var conquista = Concedida(medalhas);
        var quarta = _fatos.Medalha("M01", 4);
        var atuais = medalhas.Skip(1).Append(quarta).ToList();

        var removida = conquista.Recalcular(Avaliar(atuais), _agora.AddDays(3));

        Assert.False(removida);
        Assert.True(conquista.EstaAtiva);
        Assert.Equal(atuais.Select(fato => fato.Id).Order(), conquista.Evidencias.Select(evidencia => evidencia.FatoId).Order());
    }

    [Fact]
    public void Recalcular_a_conquista_ja_removida_nao_muda_nada()
    {
        var medalhas = _fatos.Medalhas("M01", 4, 3).ToList();
        var conquista = Concedida(medalhas);
        conquista.Recalcular(Avaliar([]), _agora.AddDays(1));

        Assert.False(conquista.Recalcular(Avaliar([]), _agora.AddDays(2)));
        Assert.False(conquista.Recalcular(Avaliar(medalhas), _agora.AddDays(2)));
        Assert.Equal(_agora.AddDays(1), conquista.RemovidaEm);
    }
}
