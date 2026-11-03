using Maxx.Domain.Catalogo;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests;

// item_catalogo e familia no Postgres real, com os 46 itens de data/catalogo/.
public sealed class CatalogoPersistenciaTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Domain.Catalogo.Catalogo _catalogo = CatalogoReader.Ler(Repositorio.DiretorioDoCatalogo).Catalogo!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Os_46_itens_e_as_8_familias_vao_e_voltam_pelo_banco()
    {
        await using var factory = await CatalogoGravadoAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();

        var lidos = await db.Set<ItemCatalogoRow>().AsNoTracking().ToDictionaryAsync(item => item.Codigo, Ct);
        Assert.Equal(46, lidos.Count);
        Assert.Equal(8, await db.Set<FamiliaRow>().CountAsync(Ct));

        foreach (var item in _catalogo.Itens)
        {
            var esperado = CatalogoRowMapper.ParaRow(item);
            var lido = lidos[item.Codigo];

            Assert.Equal(1, lido.Versao);
            Assert.Equal(
                (esperado.Tipo, esperado.Tier, esperado.Pontos, esperado.Periodo, esperado.FamiliaCodigo, esperado.OrdemNaFamilia),
                (lido.Tipo, lido.Tier, lido.Pontos, lido.Periodo, lido.FamiliaCodigo, lido.OrdemNaFamilia));
            Assert.Equal(
                (esperado.Base, esperado.Repetivel, esperado.LimitePorPeriodo, esperado.TipoDeEvento, esperado.FormaDoEvento, esperado.ExigeFoto, esperado.Gatilho),
                (lido.Base, lido.Repetivel, lido.LimitePorPeriodo, lido.TipoDeEvento, lido.FormaDoEvento, lido.ExigeFoto, lido.Gatilho));
            Assert.Equal(esperado.ChaveDeDeduplicacao, lido.ChaveDeDeduplicacao);
        }
    }

    [Fact]
    public async Task Regra_volta_do_jsonb_igual_a_do_catalogo()
    {
        await using var factory = await CatalogoGravadoAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var lidas = await db.Set<ItemCatalogoRow>().AsNoTracking()
            .Where(item => item.Regra != null)
            .ToDictionaryAsync(item => item.Codigo, item => RegraDeConquistaJson.Ler(item.Regra!), Ct);

        foreach (var conquista in _catalogo.Itens.OfType<ItemConquista>())
        {
            var lida = lidas[conquista.Codigo];
            Assert.Equal(conquista.Regra.Forma, lida.Forma);
            Assert.Equal(conquista.Regra.Fonte, lida.Fonte);
            Assert.Equal(conquista.Regra.FiltroItens, lida.FiltroItens);
            Assert.Equal(conquista.Regra.AgruparPor, lida.AgruparPor);
            Assert.Equal(conquista.Regra.Alvo, lida.Alvo);
        }
    }

    [Fact]
    public async Task Regra_fica_consultavel_por_sql_no_formato_do_arquivo()
    {
        await using var factory = await CatalogoGravadoAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<Npgsql.NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand(
            "SELECT regra ->> 'alvo' FROM item_catalogo WHERE codigo = 'C03'");

        Assert.Equal("todasAsMatriculasDoPeriodo", await command.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task Codigo_repetido_e_recusado()
    {
        await using var factory = await CatalogoGravadoAsync();

        var repetido = CatalogoRowMapper.ParaRow(_catalogo.Itens.First(item => item.Codigo == "P01"));

        AssertUnicidade(await FalhaAoSalvarAsync(factory, repetido), "pk_item_catalogo");
    }

    [Fact]
    public async Task Ordem_repetida_na_familia_e_recusada()
    {
        await using var factory = await CatalogoGravadoAsync();

        var falha = await FalhaNoSqlAsync(
            factory, "UPDATE item_catalogo SET ordem_na_familia = 1 WHERE codigo = 'C02'");

        AssertUnicidade(falha, "ix_item_catalogo_familia_codigo_ordem_na_familia");
    }

    [Fact]
    public async Task Familia_inexistente_e_recusada()
    {
        await using var factory = await CatalogoGravadoAsync();

        var falha = await FalhaNoSqlAsync(
            factory, "UPDATE item_catalogo SET familia_codigo = 'nao-existe' WHERE codigo = 'C02'");

        AssertChaveEstrangeira(falha, "fk_item_catalogo_familia_familia_codigo");
    }

    // O validador já impede os casos abaixo; o banco recusa sozinho, por SQL direto.

    [Theory]
    [InlineData("M01", 31)]
    [InlineData("M10", 30)]
    [InlineData("C18", 151)]
    public async Task Pontos_fora_da_faixa_do_tier_sao_recusados_pelo_banco(string codigo, int pontos)
    {
        await using var factory = await CatalogoGravadoAsync();

        var falha = await FalhaNoSqlAsync(
            factory, $"UPDATE item_catalogo SET pontos = {pontos} WHERE codigo = '{codigo}'");

        AssertCheck(falha, "ck_item_catalogo_pontos_na_faixa");
    }

    [Theory]
    [InlineData("M01", "regra = '{}'")]
    [InlineData("P01", "repetivel = true")]
    [InlineData("C01", "chave_de_deduplicacao = '{aluno}'")]
    [InlineData("M02", "base = NULL")]
    [InlineData("C05", "regra = NULL")]
    public async Task Campo_de_outro_tipo_ou_campo_proprio_ausente_e_recusado_pelo_banco(string codigo, string atribuicao)
    {
        await using var factory = await CatalogoGravadoAsync();

        var falha = await FalhaNoSqlAsync(factory, $"UPDATE item_catalogo SET {atribuicao} WHERE codigo = '{codigo}'");

        AssertCheck(falha, "ck_item_catalogo_campos_por_tipo");
    }

    [Fact]
    public async Task Familia_com_ordem_sem_codigo_e_recusada_pelo_banco()
    {
        await using var factory = await CatalogoGravadoAsync();

        var falha = await FalhaNoSqlAsync(factory, "UPDATE item_catalogo SET familia_codigo = NULL WHERE codigo = 'C02'");

        AssertCheck(falha, "ck_item_catalogo_familia");
    }

    private async Task<MaxxApiFactory> CatalogoGravadoAsync()
    {
        var factory = await MigradoAsync(postgres);
        await SalvarAsync(
            factory,
            [.. _catalogo.Familias.Select(CatalogoRowMapper.ParaRow), .. _catalogo.Itens.Select(CatalogoRowMapper.ParaRow)]);
        return factory;
    }
}
