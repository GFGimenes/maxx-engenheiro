using Maxx.Domain.Catalogo;

namespace Maxx.Domain.Tests.Catalogo;

// Um catálogo pequeno e válido, com um item de cada caso que o validador olha. Cada teste
// troca um item ou uma família e confere o erro.
internal static class CatalogoDeTeste
{
    private static readonly CampoDeDeduplicacao[] _chaveRepetivel =
    [
        CampoDeDeduplicacao.Aluno, CampoDeDeduplicacao.Item, CampoDeDeduplicacao.Materia,
        CampoDeDeduplicacao.Bimestre, CampoDeDeduplicacao.Ocorrencia,
    ];

    public static ItemMedalha M01 { get; } = new()
    {
        Codigo = "M01",
        Nome = "Nota 10 em prova",
        Descricao = "Tirar 10 em uma prova da matéria.",
        Base = BaseDaMedalha.Nota,
        Tier = Tier.Bronze,
        Pontos = 30,
        PeriodoDeReferencia = Periodo.Bimestre,
        Repetivel = true,
        LimitePorPeriodo = 2,
        ChaveDeDeduplicacao = _chaveRepetivel,
        Familia = "nota-prova",
        OrdemNaFamilia = 1,
    };

    public static ItemMedalha M03 { get; } = new()
    {
        Codigo = "M03",
        Nome = "Presença integral no bimestre",
        Descricao = "Não faltar a nenhuma aula da matéria no bimestre.",
        Base = BaseDaMedalha.Presenca,
        Tier = Tier.Bronze,
        Pontos = 25,
        PeriodoDeReferencia = Periodo.Bimestre,
        Repetivel = false,
        LimitePorPeriodo = 1,
        ChaveDeDeduplicacao =
        [
            CampoDeDeduplicacao.Aluno, CampoDeDeduplicacao.Item, CampoDeDeduplicacao.Materia, CampoDeDeduplicacao.Bimestre,
        ],
    };

    public static ItemMedalha M10 { get; } = new()
    {
        Codigo = "M10",
        Nome = "Média final 10",
        Descricao = "Fechar a matéria com média 10.",
        Base = BaseDaMedalha.Nota,
        Tier = Tier.Prata,
        Pontos = 50,
        PeriodoDeReferencia = Periodo.Semestre,
        Repetivel = false,
        LimitePorPeriodo = 1,
        ChaveDeDeduplicacao =
        [
            CampoDeDeduplicacao.Aluno, CampoDeDeduplicacao.Item, CampoDeDeduplicacao.Materia, CampoDeDeduplicacao.Semestre,
        ],
    };

    public static ItemPremioParticipacao P01 { get; } = new()
    {
        Codigo = "P01",
        Nome = "Palestra obrigatória",
        Descricao = "Comparecer a uma palestra obrigatória do curso.",
        TipoDeEvento = "palestra",
        FormaDoEvento = FormaDoEvento.Pontual,
        Tier = Tier.Bronze,
        Pontos = 10,
        ExigeFoto = true,
        ChaveDeDeduplicacao = [CampoDeDeduplicacao.Aluno, CampoDeDeduplicacao.Evento],
    };

    // Fora do catálogo de Valido(): só os testes de evento usam.
    public static ItemPremioParticipacao P06 { get; } = P01 with
    {
        Codigo = "P06",
        Nome = "Semana acadêmica",
        Descricao = "Participar da semana acadêmica do curso do começo ao fim.",
        TipoDeEvento = "semanaAcademica",
        FormaDoEvento = FormaDoEvento.Periodo,
        Pontos = 30,
    };

    public static ItemPremioParticipacao P17 { get; } = P01 with
    {
        Codigo = "P17",
        Nome = "Apresentação de trabalho em sala",
        Descricao = "Apresentar um trabalho em sala, na frente da turma.",
        TipoDeEvento = "apresentacaoSala",
        Pontos = 20,
        ChaveDeDeduplicacao =
        [
            CampoDeDeduplicacao.Aluno, CampoDeDeduplicacao.Item, CampoDeDeduplicacao.DataDoEvento, CampoDeDeduplicacao.Materia,
        ],
    };

    public static ItemConquista C02 { get; } = new()
    {
        Codigo = "C02",
        Nome = "Três notas 10 no bimestre",
        Descricao = "Receber três medalhas de nota 10 em prova no mesmo bimestre.",
        Tier = Tier.Prata,
        Pontos = 40,
        Periodo = Periodo.Bimestre,
        Gatilho = Gatilho.PorFato,
        Regra = new()
        {
            Forma = FormaDeContagem.Contagem,
            Fonte = FonteDeContagem.Medalha,
            FiltroItens = ["M01"],
            Alvo = AlvoDeConquista.De(3),
        },
        Familia = "nota-prova",
        OrdemNaFamilia = 2,
    };

    public static ItemConquista C03 { get; } = new()
    {
        Codigo = "C03",
        Nome = "Presença total no bimestre",
        Descricao = "Ter presença integral em todas as matérias do bimestre.",
        Tier = Tier.Prata,
        Pontos = 55,
        Periodo = Periodo.Bimestre,
        Gatilho = Gatilho.FechamentoDePeriodo,
        Regra = new()
        {
            Forma = FormaDeContagem.ContagemDistinta,
            Fonte = FonteDeContagem.Medalha,
            FiltroItens = ["M03"],
            AgruparPor = Agrupamento.Materia,
            Alvo = AlvoDeConquista.TodasAsMatriculasDoPeriodo,
        },
    };

    public static ItemConquista C06 { get; } = new()
    {
        Codigo = "C06",
        Nome = "Três tipos de evento no semestre",
        Descricao = "Prêmios de participação de três tipos de evento diferentes no semestre.",
        Tier = Tier.Prata,
        Pontos = 50,
        Periodo = Periodo.Semestre,
        Gatilho = Gatilho.PorFato,
        Regra = new()
        {
            Forma = FormaDeContagem.ContagemDistinta,
            Fonte = FonteDeContagem.PremioParticipacao,
            AgruparPor = Agrupamento.TipoDeEvento,
            Alvo = AlvoDeConquista.De(3),
        },
    };

    public static Familia NotaProva { get; } = new()
    {
        Codigo = "nota-prova",
        Nome = "Nota 10 em prova",
        Descricao = "De uma prova nota 10 até três no bimestre.",
        Passos = 2,
    };

    public static Domain.Catalogo.Catalogo Valido() => new([M01, M03, M10, P01, C02, C03, C06], [NotaProva]);

    // Troca o item de mesmo código, ou acrescenta se não houver.
    public static Domain.Catalogo.Catalogo Com(ItemCatalogo item)
    {
        var catalogo = Valido();
        var itens = catalogo.Itens.Where(existente => existente.Codigo != item.Codigo).Append(item).ToList();
        return catalogo with { Itens = itens };
    }
}
