using System.Reflection;
using System.Text.Json.Nodes;
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

    // O caminho de volta, para a medalha: a linha de item_catalogo como item do domínio.
    public static ItemMedalha ParaMedalha(ItemCatalogoRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Tipo != NomeJson.De(TipoDeItem.Medalha))
        {
            throw new InvalidOperationException($"O item {row.Codigo} é {row.Tipo}, não medalha.");
        }

        return new ItemMedalha
        {
            Codigo = row.Codigo,
            Nome = row.Nome,
            Descricao = row.Descricao,
            Tier = Ler<Tier>(row.Tier, row.Codigo),
            Pontos = row.Pontos,
            Familia = row.FamiliaCodigo,
            OrdemNaFamilia = row.OrdemNaFamilia,
            RestricaoCurso = row.RestricaoCurso,
            Vigencia = new Vigencia(row.VigenciaInicio, row.VigenciaFim),
            Base = Ler<BaseDaMedalha>(row.Base, row.Codigo),
            PeriodoDeReferencia = Ler<Periodo>(row.Periodo, row.Codigo),
            Repetivel = row.Repetivel ?? throw Incompleta(row.Codigo, "repetivel"),
            LimitePorPeriodo = row.LimitePorPeriodo ?? throw Incompleta(row.Codigo, "limitePorPeriodo"),
            ChaveDeDeduplicacao = [.. (row.ChaveDeDeduplicacao ?? throw Incompleta(row.Codigo, "chaveDeDeduplicacao"))
                .Select(campo => Ler<CampoDeDeduplicacao>(campo, row.Codigo))],
        };
    }

    private static T Ler<T>(string? texto, string codigo)
        where T : struct, Enum =>
        NomeJson.TentarLer<T>(texto, out var valor)
            ? valor
            : throw new InvalidOperationException($"O item {codigo} tem {typeof(T).Name} desconhecido: {texto}.");

    private static InvalidOperationException Incompleta(string codigo, string campo) =>
        new($"A medalha {codigo} está sem {campo} no banco.");

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

    // Todo campo da linha menos a chave e a versão. Por reflexão, para uma coluna nova
    // entrar na comparação sem ninguém lembrar de acrescentá-la aqui.
    private static readonly PropertyInfo[] _conteudo = [.. typeof(ItemCatalogoRow).GetProperties()
        .Where(propriedade => propriedade.Name is not (nameof(ItemCatalogoRow.Codigo) or nameof(ItemCatalogoRow.Versao)))];

    public static bool MesmoConteudo(ItemCatalogoRow atual, ItemCatalogoRow novo)
    {
        ArgumentNullException.ThrowIfNull(atual);
        ArgumentNullException.ThrowIfNull(novo);

        return _conteudo.All(propriedade => (propriedade.GetValue(atual), propriedade.GetValue(novo)) switch
        {
            (null, null) => true,
            (string[] a, string[] b) => a.SequenceEqual(b),

            // O jsonb volta do Postgres normalizado (ordem de chave, espaços): compara a árvore.
            (string a, string b) when propriedade.Name == nameof(ItemCatalogoRow.Regra) =>
                JsonNode.DeepEquals(JsonNode.Parse(a), JsonNode.Parse(b)),
            (var a, var b) => Equals(a, b),
        });
    }

    public static void CopiarConteudo(ItemCatalogoRow origem, ItemCatalogoRow destino)
    {
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(destino);

        foreach (var propriedade in _conteudo)
        {
            propriedade.SetValue(destino, propriedade.GetValue(origem));
        }
    }

    public static bool MesmoConteudo(FamiliaRow atual, FamiliaRow nova)
    {
        ArgumentNullException.ThrowIfNull(atual);
        ArgumentNullException.ThrowIfNull(nova);

        return (atual.Nome, atual.Descricao, atual.Passos) == (nova.Nome, nova.Descricao, nova.Passos);
    }
}
