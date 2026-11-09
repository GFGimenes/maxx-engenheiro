using System.Globalization;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Tests.Catalogo;
using static Maxx.Domain.Tests.Conquistas.Fatos;

namespace Maxx.Domain.Tests.Conquistas;

// O avaliador por forma e por fronteira de período (regras-conquista.md 8). O teste por
// item, contra o conquistas.json real, está em Maxx.Api.IntegrationTests, que lê o catálogo
// como o seeder lê.
public class AvaliadorDeConquistaTests
{
    private static readonly int[] _primeirosTresBimestres = [1, 2, 3];
    private static readonly int[] _quatroBimestres = [1, 2, 3, 4];

    private readonly Fatos _fatos = new();
    private readonly AvaliadorDeConquista _avaliador = new();

    // Três notas 10 no bimestre: contagem, medalha, filtro M01, alvo 3.
    private static ItemConquista C02 => CatalogoDeTeste.C02;

    // Presença total no bimestre: contagem distinta por matéria, alvo dinâmico.
    private static ItemConquista C03 => CatalogoDeTeste.C03;

    // Três tipos de evento no semestre: contagem distinta por tipo de evento, alvo 3.
    private static ItemConquista C06 => CatalogoDeTeste.C06;

    // Cinco notas 10 no semestre: a C02 no período de cima.
    private static ItemConquista C08 => C02 with
    {
        Codigo = "C08",
        Periodo = Periodo.Semestre,
        Regra = C02.Regra with { Alvo = AlvoDeConquista.De(5) },
    };

    // Três prêmios de participação no semestre: contagem, sem filtro.
    private static ItemConquista C05 => C02 with
    {
        Codigo = "C05",
        Periodo = Periodo.Semestre,
        Regra = new() { Forma = FormaDeContagem.Contagem, Fonte = FonteDeContagem.PremioParticipacao, Alvo = AlvoDeConquista.De(3) },
    };

    // Nota 10 nos quatro bimestres do ano: contagem distinta por bimestre, alvo fixo.
    private static ItemConquista C14 => C02 with
    {
        Codigo = "C14",
        Periodo = Periodo.Ano,
        Gatilho = Gatilho.FechamentoDePeriodo,
        Regra = C02.Regra with { Forma = FormaDeContagem.ContagemDistinta, AgruparPor = Agrupamento.Bimestre, Alvo = AlvoDeConquista.De(4) },
    };

    // Forma: contagem

    [Fact]
    public void Contagem_com_alvo_fixo_alcancado_satisfaz_e_guarda_as_evidencias()
    {
        var medalhas = _fatos.Medalhas("M01", 4, 3).ToList();

        var avaliacao = _avaliador.Avaliar(C02, _fatos.InstanciaDoBimestre(4), medalhas, Contexto());

        Assert.True(avaliacao.Satisfeita);
        Assert.Equal((3, 3), (avaliacao.Contagem, avaliacao.Alvo));
        Assert.Equal(medalhas.Select(fato => fato.Id).Order(), avaliacao.Evidencias.Select(fato => fato.Id).Order());
    }

    [Fact]
    public void Contagem_um_fato_abaixo_do_alvo_nao_satisfaz_e_mostra_o_progresso()
    {
        var avaliacao = _avaliador.Avaliar(C02, _fatos.InstanciaDoBimestre(4), _fatos.Medalhas("M01", 4, 2), Contexto());

        Assert.False(avaliacao.Satisfeita);
        Assert.Equal((2, 3), (avaliacao.Contagem, avaliacao.Alvo));
    }

    [Fact]
    public void Contagem_que_passa_do_alvo_satisfaz_com_todas_as_evidencias()
    {
        var avaliacao = _avaliador.Avaliar(C02, _fatos.InstanciaDoBimestre(4), _fatos.Medalhas("M01", 4, 4), Contexto());

        Assert.True(avaliacao.Satisfeita);
        Assert.Equal(4, avaliacao.Evidencias.Count);
    }

    [Fact]
    public void Contagem_ignora_item_fora_do_filtro()
    {
        var fatos = _fatos.Medalhas("M01", 4, 2).Append(_fatos.Medalha("M02", 4));

        var avaliacao = _avaliador.Avaliar(C02, _fatos.InstanciaDoBimestre(4), fatos, Contexto());

        Assert.Equal((2, false), (avaliacao.Contagem, avaliacao.Satisfeita));
    }

    [Fact]
    public void Contagem_ignora_outra_fonte()
    {
        var fatos = _fatos.Premios("P01", "palestra", 2).Append(_fatos.Medalha("M01", 4));

        var avaliacao = _avaliador.Avaliar(C05, _fatos.InstanciaDoSemestre(2), fatos, Contexto());

        Assert.Equal((2, false), (avaliacao.Contagem, avaliacao.Satisfeita));
    }

    [Fact]
    public void Contagem_da_fonte_ambos_conta_medalha_e_premio()
    {
        var ambos = C05 with { Regra = C05.Regra with { Fonte = FonteDeContagem.Ambos } };
        var fatos = _fatos.Premios("P01", "palestra", 2).Append(_fatos.Medalha("M01", 4));

        var avaliacao = _avaliador.Avaliar(ambos, _fatos.InstanciaDoSemestre(2), fatos, Contexto());

        Assert.Equal((3, true), (avaliacao.Contagem, avaliacao.Satisfeita));
    }

    [Fact]
    public void O_mesmo_fato_carregado_duas_vezes_conta_uma()
    {
        var medalha = _fatos.Medalha("M01", 4);

        var avaliacao = _avaliador.Avaliar(
            C02, _fatos.InstanciaDoBimestre(4), [medalha, medalha, medalha with { }], Contexto());

        Assert.Equal(1, avaliacao.Contagem);
    }

    // Forma: contagem distinta

    [Fact]
    public void Contagem_distinta_com_alvo_fixo_conta_valores_e_nao_fatos()
    {
        var tresPalestras = _fatos.Premios("P01", "palestra", 3).ToList();

        var umTipo = _avaliador.Avaliar(C06, _fatos.InstanciaDoSemestre(2), tresPalestras, Contexto());
        var tresTipos = _avaliador.Avaliar(
            C06,
            _fatos.InstanciaDoSemestre(2),
            [.. tresPalestras, _fatos.Premio("P02", "solidario"), _fatos.Premio("P04", "experimento")],
            Contexto());

        Assert.Equal((1, 3, false), (umTipo.Contagem, umTipo.Alvo, umTipo.Satisfeita));
        Assert.Equal((3, true), (tresTipos.Contagem, tresTipos.Satisfeita));
        Assert.Equal(5, tresTipos.Evidencias.Count);
    }

    [Fact]
    public void Contagem_distinta_por_bimestre_com_alvo_fixo()
    {
        var tresBimestres = _primeirosTresBimestres.Select(bimestre => _fatos.Medalha("M01", bimestre)).ToList();

        var abaixo = _avaliador.Avaliar(C14, _fatos.InstanciaDoAno, tresBimestres, Contexto());
        var alcancado = _avaliador.Avaliar(
            C14, _fatos.InstanciaDoAno, [.. tresBimestres, _fatos.Medalha("M01", 4)], Contexto());

        Assert.Equal((3, 4, false), (abaixo.Contagem, abaixo.Alvo, abaixo.Satisfeita));
        Assert.True(alcancado.Satisfeita);
    }

    [Fact]
    public void Contagem_distinta_com_alvo_dinamico_satisfaz_com_todas_as_materias_do_periodo()
    {
        var fatos = new[] { _fatos.Medalha("M03", 4, BancoDeDados), _fatos.Medalha("M03", 4, Redes) };

        var avaliacao = _avaliador.Avaliar(C03, _fatos.InstanciaDoBimestre(4), fatos, Contexto(BancoDeDados, Redes));

        Assert.Equal((2, 2, true), (avaliacao.Contagem, avaliacao.Alvo, avaliacao.Satisfeita));
    }

    [Fact]
    public void Contagem_distinta_com_alvo_dinamico_uma_materia_abaixo_nao_satisfaz()
    {
        var fatos = new[] { _fatos.Medalha("M03", 4, BancoDeDados), _fatos.Medalha("M03", 4, Redes) };

        var avaliacao = _avaliador.Avaliar(
            C03, _fatos.InstanciaDoBimestre(4), fatos, Contexto(BancoDeDados, Redes, Algoritmos));

        Assert.Equal((2, 3, false), (avaliacao.Contagem, avaliacao.Alvo, avaliacao.Satisfeita));
    }

    [Fact]
    public void Contagem_distinta_com_alvo_dinamico_ignora_materia_sem_matricula_no_periodo()
    {
        var fatos = new[] { _fatos.Medalha("M03", 4, BancoDeDados), _fatos.Medalha("M03", 4, Redes) };

        var avaliacao = _avaliador.Avaliar(C03, _fatos.InstanciaDoBimestre(4), fatos, Contexto(BancoDeDados, Algoritmos));

        Assert.Equal((1, 2, false), (avaliacao.Contagem, avaliacao.Alvo, avaliacao.Satisfeita));
        Assert.Equal(BancoDeDados, Assert.Single(avaliacao.Evidencias).MateriaId);
    }

    [Fact]
    public void Alvo_dinamico_sem_matricula_no_periodo_nunca_satisfaz()
    {
        var avaliacao = _avaliador.Avaliar(
            C03, _fatos.InstanciaDoBimestre(4), [_fatos.Medalha("M03", 4)], SemMatriculas);

        Assert.Equal((0, 0, false), (avaliacao.Contagem, avaliacao.Alvo, avaliacao.Satisfeita));
    }

    [Fact]
    public void Alvo_dinamico_sem_as_materias_informadas_e_erro_de_quem_chama()
    {
        Assert.Throws<DomainException>(() =>
            _avaliador.Avaliar(C03, _fatos.InstanciaDoBimestre(4), [_fatos.Medalha("M03", 4)], Contexto()));
    }

    // Fronteira de período

    [Fact]
    public void Contagem_nao_atravessa_o_bimestre()
    {
        // Duas notas 10 no bimestre 3 e uma no 4: não fecha a C02 em nenhum dos dois.
        var fatos = _fatos.Medalhas("M01", 3, 2).Append(_fatos.Medalha("M01", 4)).ToList();

        var terceiro = _avaliador.Avaliar(C02, _fatos.InstanciaDoBimestre(3), fatos, Contexto());
        var quarto = _avaliador.Avaliar(C02, _fatos.InstanciaDoBimestre(4), fatos, Contexto());

        Assert.Equal((2, false), (terceiro.Contagem, terceiro.Satisfeita));
        Assert.Equal((1, false), (quarto.Contagem, quarto.Satisfeita));
    }

    [Fact]
    public void Contagem_atravessa_os_bimestres_dentro_do_semestre()
    {
        // As mesmas três, mais duas no bimestre 4: cinco no semestre fecham a C08.
        var fatos = _fatos.Medalhas("M01", 3, 2).Concat(_fatos.Medalhas("M01", 4, 3)).ToList();

        var avaliacao = _avaliador.Avaliar(C08, _fatos.InstanciaDoSemestre(2), fatos, Contexto());

        Assert.Equal((5, true), (avaliacao.Contagem, avaliacao.Satisfeita));
    }

    [Fact]
    public void Contagem_nao_atravessa_o_semestre()
    {
        var fatos = _fatos.Medalhas("M01", 2, 2).Concat(_fatos.Medalhas("M01", 3, 3)).ToList();

        var avaliacao = _avaliador.Avaliar(C08, _fatos.InstanciaDoSemestre(2), fatos, Contexto());

        Assert.Equal((3, false), (avaliacao.Contagem, avaliacao.Satisfeita));
    }

    [Fact]
    public void Periodo_curso_conta_a_vida_inteira_do_aluno()
    {
        var c16 = C02 with
        {
            Codigo = "C16",
            Periodo = Periodo.Curso,
            Regra = new() { Forma = FormaDeContagem.Contagem, Fonte = FonteDeContagem.Medalha, Alvo = AlvoDeConquista.De(4) },
        };
        var fatos = _quatroBimestres.Select(bimestre => _fatos.Medalha("M02", bimestre));

        var avaliacao = _avaliador.Avaliar(c16, InstanciaDoCurso, fatos, Contexto());

        Assert.Equal((4, true), (avaliacao.Contagem, avaliacao.Satisfeita));
    }

    [Fact]
    public void Instancia_de_outro_periodo_e_erro_de_quem_chama()
    {
        Assert.Throws<DomainException>(() => _avaliador.Avaliar(C02, _fatos.InstanciaDoSemestre(2), [], Contexto()));
    }

    // Aplicação: curso e vigência

    [Fact]
    public void Item_restrito_a_outro_curso_nao_se_aplica()
    {
        var deAdm = C02 with { RestricaoCurso = "ADM" };

        var avaliacao = _avaliador.Avaliar(deAdm, _fatos.InstanciaDoBimestre(4), _fatos.Medalhas("M01", 4, 3), Contexto());

        Assert.False(avaliacao.Aplicavel);
        Assert.False(avaliacao.Satisfeita);
    }

    [Fact]
    public void Item_restrito_ao_curso_do_aluno_se_aplica()
    {
        var deCc = C02 with { RestricaoCurso = "cc" };

        var avaliacao = _avaliador.Avaliar(deCc, _fatos.InstanciaDoBimestre(4), _fatos.Medalhas("M01", 4, 3), Contexto());

        Assert.True(avaliacao.Satisfeita);
    }

    [Theory]
    [InlineData("2026-12-06", null, false)]
    [InlineData(null, "2026-12-04", false)]
    [InlineData("2026-12-05", "2026-12-05", true)]
    public void Vigencia_e_conferida_na_data_de_referencia(string? inicio, string? fim, bool aplicavel)
    {
        var item = C02 with
        {
            Vigencia = new(inicio is null ? null : DateOnly.Parse(inicio, CultureInfo.InvariantCulture), fim is null ? null : DateOnly.Parse(fim, CultureInfo.InvariantCulture)),
        };

        var avaliacao = _avaliador.Avaliar(item, _fatos.InstanciaDoBimestre(4), _fatos.Medalhas("M01", 4, 3), Contexto());

        Assert.Equal(aplicavel, avaliacao.Aplicavel);
    }

    // Escape para regra em C#

    [Fact]
    public void Regra_customizada_delega_a_implementacao_registrada_com_os_fatos_da_instancia()
    {
        var regra = new RegraDeTeste();
        var avaliador = new AvaliadorDeConquista([regra]);
        var customizada = C02 with { Regra = new() { Forma = FormaDeContagem.Customizada, Implementacao = "de-teste" } };
        var fatos = _fatos.Medalhas("M01", 4, 2).Append(_fatos.Medalha("M01", 3));

        var avaliacao = avaliador.Avaliar(customizada, _fatos.InstanciaDoBimestre(4), fatos, Contexto());

        Assert.True(avaliacao.Satisfeita);
        Assert.Equal(2, regra.FatosRecebidos);
    }

    [Fact]
    public void Regra_customizada_sem_implementacao_registrada_e_erro()
    {
        var customizada = C02 with { Regra = new() { Forma = FormaDeContagem.Customizada, Implementacao = "ausente" } };

        Assert.Throws<DomainException>(() => _avaliador.Avaliar(customizada, _fatos.InstanciaDoBimestre(4), [], Contexto()));
    }

    [Fact]
    public void Regra_customizada_registrada_duas_vezes_e_erro()
    {
        Assert.Throws<DomainException>(() => new AvaliadorDeConquista([new RegraDeTeste(), new RegraDeTeste()]));
    }

    private sealed class RegraDeTeste : IRegraCustomizada
    {
        public string Nome => "de-teste";

        public int FatosRecebidos { get; private set; }

        public Avaliacao Avaliar(
            ItemConquista conquista,
            InstanciaDePeriodo instancia,
            IReadOnlyList<FatoContavel> fatosDaInstancia,
            ContextoDaAvaliacao contexto)
        {
            FatosRecebidos = fatosDaInstancia.Count;
            return Avaliacao.De(1, 1, fatosDaInstancia);
        }
    }
}
