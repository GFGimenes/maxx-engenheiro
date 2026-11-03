using Maxx.Domain.Catalogo;
using Maxx.Infrastructure.Persistence;

namespace Maxx.Infrastructure.Seed;

// Do item do domínio para a linha de item_catalogo. A versão fica de fora: quem decide
// é o seeder, comparando com o que já está no banco.
public static class CatalogoRowMapper
{
    public static ItemCatalogoRow ParaRow(ItemCatalogo item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var row = new ItemCatalogoRow
        {
            Codigo = item.Codigo,
            Tipo = NomeJson.De(item.Tipo),
            Nome = item.Nome,
            Descricao = item.Descricao,
            Tier = NomeJson.De(item.Tier),
            Pontos = item.Pontos,
            Periodo = "",
            FamiliaCodigo = item.Familia,
            OrdemNaFamilia = item.OrdemNaFamilia,
            RestricaoCurso = item.RestricaoCurso,
            VigenciaInicio = item.Vigencia.Inicio,
            VigenciaFim = item.Vigencia.Fim,
        };

        switch (item)
        {
            case ItemMedalha medalha:
                row.Periodo = NomeJson.De(medalha.PeriodoDeReferencia);
                row.ChaveDeDeduplicacao = Chave(medalha.ChaveDeDeduplicacao);
                row.Base = NomeJson.De(medalha.Base);
                row.Repetivel = medalha.Repetivel;
                row.LimitePorPeriodo = medalha.LimitePorPeriodo;
                break;
            case ItemPremioParticipacao premio:
                row.Periodo = NomeJson.De(premio.PeriodoDeReferencia);
                row.ChaveDeDeduplicacao = Chave(premio.ChaveDeDeduplicacao);
                row.TipoDeEvento = premio.TipoDeEvento;
                row.FormaDoEvento = NomeJson.De(premio.FormaDoEvento);
                row.ExigeFoto = premio.ExigeFoto;
                break;
            case ItemConquista conquista:
                row.Periodo = NomeJson.De(conquista.Periodo);
                row.Gatilho = NomeJson.De(conquista.Gatilho);
                row.Regra = RegraDeConquistaJson.Escrever(conquista.Regra);
                break;
        }

        return row;
    }

    public static FamiliaRow ParaRow(Familia familia)
    {
        ArgumentNullException.ThrowIfNull(familia);

        return new()
        {
            Codigo = familia.Codigo,
            Nome = familia.Nome,
            Descricao = familia.Descricao,
            Passos = familia.Passos,
        };
    }

    private static string[] Chave(IReadOnlyList<CampoDeDeduplicacao> chave) => [.. chave.Select(NomeJson.De)];
}
