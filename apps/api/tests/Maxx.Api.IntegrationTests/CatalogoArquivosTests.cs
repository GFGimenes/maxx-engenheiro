using System.Text.Json.Nodes;
using Maxx.Domain.Catalogo;
using Maxx.Infrastructure.Seed;

namespace Maxx.Api.IntegrationTests;

// A validação do catálogo na CI (ADR 0004): lê os arquivos de data/catalogo/ como o
// seeder lê, e falha mostrando o código do item. Sem banco.
public sealed class CatalogoArquivosTests : IDisposable
{
    private static readonly string _diretorioReal = Repositorio.DiretorioDoCatalogo;

    private readonly string _copia = Path.Combine(Path.GetTempPath(), $"maxx-catalogo-{Guid.NewGuid():N}");

    public CatalogoArquivosTests()
    {
        Directory.CreateDirectory(_copia);
        foreach (var arquivo in Directory.GetFiles(_diretorioReal, "*.json"))
        {
            File.Copy(arquivo, Path.Combine(_copia, Path.GetFileName(arquivo)));
        }
    }

    public void Dispose() => Directory.Delete(_copia, recursive: true);

    [Fact]
    public void Catalogo_do_repositorio_e_valido()
    {
        var resultado = CatalogoReader.Ler(_diretorioReal);

        Assert.True(resultado.Valido, string.Join(Environment.NewLine, resultado.Erros));
    }

    [Fact]
    public void Catalogo_do_repositorio_tem_os_46_itens_e_as_8_familias()
    {
        var catalogo = CatalogoReader.Ler(_diretorioReal).Catalogo!;

        Assert.Equal(11, catalogo.Itens.Count(item => item.Tipo == TipoDeItem.Medalha));
        Assert.Equal(17, catalogo.Itens.Count(item => item.Tipo == TipoDeItem.PremioParticipacao));
        Assert.Equal(18, catalogo.Itens.Count(item => item.Tipo == TipoDeItem.Conquista));
        Assert.Equal(8, catalogo.Familias.Count);
    }

    [Fact]
    public void Regras_sao_lidas_com_alvo_fixo_e_dinamico()
    {
        var conquistas = CatalogoReader.Ler(_diretorioReal).Catalogo!.Itens.OfType<ItemConquista>()
            .ToDictionary(conquista => conquista.Codigo);

        Assert.Equal(3, conquistas["C02"].Regra.Alvo?.Fixo);
        Assert.Equal(["M01"], conquistas["C02"].Regra.FiltroItens);
        Assert.True(conquistas["C03"].Regra.Alvo?.EhTodasAsMatriculasDoPeriodo);
        Assert.Equal(Agrupamento.Materia, conquistas["C03"].Regra.AgruparPor);
        Assert.Equal(Gatilho.FechamentoDePeriodo, conquistas["C03"].Gatilho);
    }

    [Fact]
    public void Pontos_fora_da_faixa_falham_com_o_codigo_do_item()
    {
        Alterar(CatalogoReader.ArquivoDeMedalhas, "M01", item => item["pontos"] = 31);

        AssertErro("M01", "fora da faixa");
    }

    [Fact]
    public void Filtro_para_item_inexistente_falha_com_o_codigo_da_conquista()
    {
        Alterar(CatalogoReader.ArquivoDeConquistas, "C02", item => item["regra"]!["filtroItens"] = new JsonArray("M99"));

        AssertErro("C02", "M99, que não existe");
    }

    [Fact]
    public void Valor_de_enumeracao_desconhecido_falha_com_o_codigo_do_item()
    {
        Alterar(CatalogoReader.ArquivoDeConquistas, "C05", item => item["gatilho"] = "porfato");

        AssertErro("C05", "gatilho \"porfato\" desconhecido");
    }

    [Fact]
    public void Campo_ausente_falha_com_o_codigo_do_item()
    {
        Alterar(CatalogoReader.ArquivoDePremiosParticipacao, "P03", item => item.Remove("tier"));

        AssertErro("P03", "campo tier ausente");
    }

    [Fact]
    public void Campo_desconhecido_falha_com_o_nome_do_arquivo()
    {
        Alterar(CatalogoReader.ArquivoDeMedalhas, "M02", item => item["raridade"] = "rara");

        AssertErro(CatalogoReader.ArquivoDeMedalhas, "raridade");
    }

    [Fact]
    public void Item_no_arquivo_errado_falha()
    {
        Alterar(CatalogoReader.ArquivoDeMedalhas, "M04", item => item["tipo"] = "conquista");

        AssertErro("M04", "no arquivo medalhas.json");
    }

    [Fact]
    public void Arquivo_ausente_falha()
    {
        File.Delete(Path.Combine(_copia, CatalogoReader.ArquivoDeFamilias));

        AssertErro(CatalogoReader.ArquivoDeFamilias, "não encontrado");
    }

    private void Alterar(string arquivo, string codigo, Action<JsonObject> alteracao)
    {
        var caminho = Path.Combine(_copia, arquivo);
        var raiz = JsonNode.Parse(File.ReadAllText(caminho))!;
        var item = raiz["itens"]!.AsArray().Single(item => (string?)item!["codigo"] == codigo)!.AsObject();
        alteracao(item);
        File.WriteAllText(caminho, raiz.ToJsonString());
    }

    private void AssertErro(string referencia, string trecho)
    {
        var resultado = CatalogoReader.Ler(_copia);

        Assert.False(resultado.Valido);
        Assert.Contains(
            resultado.Erros,
            erro => erro.Referencia == referencia && erro.Mensagem.Contains(trecho, StringComparison.Ordinal));
    }

}
