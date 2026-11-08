using Maxx.Domain.Catalogo;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;

namespace Maxx.Api.IntegrationTests.PremiosParticipacao;

// Três prêmios de participação do catálogo, como no arquivo, para os testes que reivindicam
// sem rodar o seed inteiro: P01, de evento pontual; P06, de período; P17, a apresentação em
// sala, que deduplica por matéria.
internal static class CatalogoDeParticipacao
{
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
        Familia = null,
        ChaveDeDeduplicacao =
        [
            CampoDeDeduplicacao.Aluno, CampoDeDeduplicacao.Item, CampoDeDeduplicacao.DataDoEvento, CampoDeDeduplicacao.Materia,
        ],
    };

    public static IEnumerable<ItemCatalogoRow> Linhas() => [.. new[] { P01, P06, P17 }.Select(CatalogoRowMapper.ParaRow)];
}
