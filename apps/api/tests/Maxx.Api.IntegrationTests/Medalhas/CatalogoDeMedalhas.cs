using Maxx.Domain.Catalogo;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;

namespace Maxx.Api.IntegrationTests.Medalhas;

// Duas medalhas do catálogo, como linhas de item_catalogo, para os testes que concedem sem
// rodar o seed inteiro: M01, repetível com duas vagas no bimestre, e M03, uma vaga só.
internal static class CatalogoDeMedalhas
{
    public static ItemCatalogoRow M01() => CatalogoRowMapper.ParaRow(new ItemMedalha
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
        ChaveDeDeduplicacao =
        [
            CampoDeDeduplicacao.Aluno, CampoDeDeduplicacao.Item, CampoDeDeduplicacao.Materia,
            CampoDeDeduplicacao.Bimestre, CampoDeDeduplicacao.Ocorrencia,
        ],
    });

    public static ItemCatalogoRow M03() => CatalogoRowMapper.ParaRow(new ItemMedalha
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
    });
}
