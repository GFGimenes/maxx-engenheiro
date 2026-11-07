using System.Text.Json.Nodes;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests;

// `seed base` contra o Postgres real. Os testes que mudam arquivo trabalham numa cópia
// de data/, nunca no repositório.
public sealed class SeedBaseTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly CopiaDeDados _copia = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Dados => _copia.Diretorio;

    public void Dispose() => _copia.Dispose();

    [Fact]
    public async Task Banco_vazio_recebe_os_46_itens_as_8_familias_e_o_calendario_de_2026()
    {
        await using var factory = await MigradoAsync(postgres);

        var resultado = await factory.SeedBaseAsync(Ct);

        Assert.Equal(new ResultadoDoSeedBase(46, 0, 0, 8, 0, 1, 0, 1), resultado);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        Assert.Equal(46, await db.Set<ItemCatalogoRow>().CountAsync(item => item.Versao == 1, Ct));

        var ano = await db.Set<AnoLetivo>()
            .Include(ano => ano.Semestres).ThenInclude(semestre => semestre.Bimestres)
            .SingleAsync(Ct);
        Assert.Equal(
            [
                (1, new DateOnly(2026, 1, 26), new DateOnly(2026, 4, 3)),
                (2, new DateOnly(2026, 4, 6), new DateOnly(2026, 6, 27)),
                (3, new DateOnly(2026, 7, 27), new DateOnly(2026, 9, 26)),
                (4, new DateOnly(2026, 9, 28), new DateOnly(2026, 12, 5)),
            ],
            ano.Bimestres.Select(bimestre => (bimestre.Numero, bimestre.Inicio, bimestre.Fim)));
    }

    [Fact]
    public async Task Rodar_de_novo_sem_mudar_os_arquivos_nao_muda_nada()
    {
        await using var factory = await MigradoAsync(postgres);
        await factory.SeedBaseAsync(Ct);
        var antes = await FotografiaAsync(factory);

        var resultado = await factory.SeedBaseAsync(Ct);

        Assert.Equal(new ResultadoDoSeedBase(0, 0, 46, 0, 0, 0, 0, 0), resultado);
        Assert.Equal(antes, await FotografiaAsync(factory));
    }

    [Fact]
    public async Task Mudar_os_pontos_de_um_item_sobe_a_versao_so_dele()
    {
        await using var factory = await MigradoAsync(postgres, Dados);
        await factory.SeedBaseAsync(Ct);

        AlterarItem("medalhas.json", "M02", item => item["pontos"] = 20);
        var resultado = await factory.SeedBaseAsync(Ct);

        Assert.Equal(new ResultadoDoSeedBase(0, 1, 45, 0, 0, 0, 0, 0), resultado);
        var versoes = await VersoesAsync(factory);
        Assert.Equal((2, 20), versoes["M02"]);
        Assert.All(versoes.Where(par => par.Key != "M02"), par => Assert.Equal(1, par.Value.Versao));
    }

    [Fact]
    public async Task Versao_nova_e_estavel_ao_rodar_de_novo()
    {
        await using var factory = await MigradoAsync(postgres, Dados);
        await factory.SeedBaseAsync(Ct);
        AlterarItem("medalhas.json", "M02", item => item["pontos"] = 20);
        await factory.SeedBaseAsync(Ct);

        await factory.SeedBaseAsync(Ct);

        Assert.Equal(2, (await VersoesAsync(factory))["M02"].Versao);
    }

    [Fact]
    public async Task Mudar_a_regra_de_uma_conquista_sobe_a_versao()
    {
        await using var factory = await MigradoAsync(postgres, Dados);
        await factory.SeedBaseAsync(Ct);

        AlterarItem("conquistas.json", "C02", item => item["regra"]!["alvo"] = 4);
        await factory.SeedBaseAsync(Ct);

        Assert.Equal(2, (await VersoesAsync(factory))["C02"].Versao);
    }

    [Fact]
    public async Task Item_que_sumiu_do_arquivo_faz_o_seed_falhar_sem_gravar_nada()
    {
        await using var factory = await MigradoAsync(postgres, Dados);
        await factory.SeedBaseAsync(Ct);

        // Na mesma rodada, uma mudança válida em outro item: também não pode entrar.
        AlterarItem("medalhas.json", "M02", item => item["pontos"] = 20);
        RemoverItem("premios-participacao.json", "P05");
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedBaseAsync(Ct));

        Assert.Contains("P05", falha.Message, StringComparison.Ordinal);
        Assert.Contains("vigencia.fim", falha.Message, StringComparison.Ordinal);
        var versoes = await VersoesAsync(factory);
        Assert.Equal((1, 25), versoes["M02"]);
        Assert.Contains("P05", versoes.Keys);
    }

    [Fact]
    public async Task Catalogo_invalido_nao_grava_nada()
    {
        await using var factory = await MigradoAsync(postgres, Dados);

        AlterarItem("medalhas.json", "M01", item => item["pontos"] = 31);
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedBaseAsync(Ct));

        Assert.Contains("M01", falha.Message, StringComparison.Ordinal);
        Assert.Equal(new Fotografia(0, 0, 0, ""), await FotografiaAsync(factory));
    }

    [Fact]
    public async Task Corrigir_uma_data_no_arquivo_atualiza_o_bimestre_com_o_mesmo_id()
    {
        await using var factory = await MigradoAsync(postgres, Dados);
        await factory.SeedBaseAsync(Ct);
        var idAntes = await IdDoBimestreAsync(factory, 4);

        AlterarCalendario(ano => ano["semestres"]![1]!["bimestres"]![1]!["fim"] = "2026-12-12");
        var resultado = await factory.SeedBaseAsync(Ct);

        Assert.Equal(1, resultado.AnosAtualizados);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var bimestre = await db.Set<Bimestre>().SingleAsync(bimestre => bimestre.Numero == 4, Ct);
        Assert.Equal(idAntes, bimestre.Id);
        Assert.Equal(new DateOnly(2026, 12, 12), bimestre.Fim);
    }

    [Fact]
    public async Task Calendario_invalido_nao_grava_nada()
    {
        await using var factory = await MigradoAsync(postgres, Dados);

        // Bimestre 2 começando antes de o 1 terminar.
        AlterarCalendario(ano => ano["semestres"]![0]!["bimestres"]![1]!["inicio"] = "2026-03-01");
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedBaseAsync(Ct));

        Assert.Contains("2026.json", falha.Message, StringComparison.Ordinal);
        Assert.Equal(new Fotografia(0, 0, 0, ""), await FotografiaAsync(factory));
    }

    [Fact]
    public async Task Dois_seeds_em_paralelo_gravam_cada_item_uma_vez()
    {
        await using var factory = await MigradoAsync(postgres);

        var resultados = await Task.WhenAll(factory.SeedBaseAsync(Ct), factory.SeedBaseAsync(Ct));

        // Um grava; o outro espera o lock e encontra tudo pronto.
        Assert.Equal([0, 46], resultados.Select(resultado => resultado.ItensNovos).Order());
        Assert.Equal(46, (await FotografiaAsync(factory)).Itens);
    }

    [Fact]
    public async Task Seed_espera_o_lock_de_outro_seed()
    {
        await using var factory = await MigradoAsync(postgres);
        await using var scope = factory.Services.CreateAsyncScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        await using var outro = await dataSource.OpenConnectionAsync(Ct);
        await ExecutarAsync(outro, $"SELECT pg_advisory_lock({DatabaseSeeder.AdvisoryLockKey})");

        var seed = factory.SeedBaseAsync(Ct);
        var terminou = await Task.WhenAny(seed, Task.Delay(TimeSpan.FromSeconds(2), Ct));
        Assert.NotSame(seed, terminou);
        Assert.Equal(0, (await FotografiaAsync(factory)).Itens);

        await ExecutarAsync(outro, $"SELECT pg_advisory_unlock({DatabaseSeeder.AdvisoryLockKey})");
        await seed.WaitAsync(TimeSpan.FromSeconds(30), Ct);

        Assert.Equal(46, (await FotografiaAsync(factory)).Itens);
    }

    // O que importa comparar entre duas rodadas: contagens, versões e datas.
    private sealed record Fotografia(int Itens, int Familias, int Anos, string Conteudo);

    private static async Task<Fotografia> FotografiaAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var itens = (await db.Set<ItemCatalogoRow>().AsNoTracking().OrderBy(item => item.Codigo).ToListAsync(Ct))
            .Select(item => $"{item.Codigo}:{item.Versao}:{item.Pontos}:{item.Nome}:{item.Regra}")
            .ToList();
        var bimestres = (await db.Set<Bimestre>().AsNoTracking().OrderBy(bimestre => bimestre.Numero).ToListAsync(Ct))
            .Select(bimestre => $"{bimestre.Id}:{bimestre.Inicio}:{bimestre.Fim}");

        return new(
            itens.Count,
            await db.Set<FamiliaRow>().CountAsync(Ct),
            await db.Set<AnoLetivo>().CountAsync(Ct),
            string.Join("|", itens.Concat(bimestres)));
    }

    private static async Task<Dictionary<string, (int Versao, int Pontos)>> VersoesAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        return await db.Set<ItemCatalogoRow>().AsNoTracking()
            .ToDictionaryAsync(item => item.Codigo, item => (item.Versao, item.Pontos), Ct);
    }

    [Fact]
    public async Task Versao_nova_do_termo_entra_e_passa_a_ser_a_vigente_sem_mexer_na_antiga()
    {
        await using var factory = await MigradoAsync(postgres, Dados);
        await factory.SeedBaseAsync(Ct);
        var primeira = await TermosAsync(factory);

        await File.WriteAllTextAsync(Path.Combine(Dados, "termos", "termo-de-uso.v2.md"), "Texto novo.\n", Ct);
        var resultado = await factory.SeedBaseAsync(Ct);

        Assert.Equal(new ResultadoDoSeedBase(0, 0, 46, 0, 0, 0, 0, 1), resultado);
        var termos = await TermosAsync(factory);
        Assert.Equal(primeira[1], termos[1]);
        Assert.Equal("Texto novo.", termos[2]);
    }

    [Fact]
    public async Task Texto_de_versao_publicada_nao_muda_e_nada_e_gravado()
    {
        await using var factory = await MigradoAsync(postgres, Dados);
        await factory.SeedBaseAsync(Ct);
        var antes = await TermosAsync(factory);

        await File.AppendAllTextAsync(ArquivoDoTermo, "Frase acrescentada.\n", Ct);
        await File.WriteAllTextAsync(Path.Combine(Dados, "termos", "termo-de-uso.v2.md"), "Texto novo.\n", Ct);
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedBaseAsync(Ct));

        Assert.Contains("v1", falha.Message, StringComparison.Ordinal);
        Assert.Equal(antes, await TermosAsync(factory));
    }

    [Fact]
    public async Task Versao_publicada_sem_arquivo_e_erro()
    {
        await using var factory = await MigradoAsync(postgres, Dados);
        await factory.SeedBaseAsync(Ct);

        File.Delete(ArquivoDoTermo);
        await File.WriteAllTextAsync(Path.Combine(Dados, "termos", "termo-de-uso.v2.md"), "Texto novo.\n", Ct);
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedBaseAsync(Ct));

        Assert.Contains("v1", falha.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fim_de_linha_do_checkout_nao_conta_como_mudanca_do_texto()
    {
        await using var factory = await MigradoAsync(postgres, Dados);
        await factory.SeedBaseAsync(Ct);

        var texto = await File.ReadAllTextAsync(ArquivoDoTermo, Ct);
        var outro = texto.Contains("\r\n", StringComparison.Ordinal)
            ? texto.ReplaceLineEndings("\n")
            : texto.ReplaceLineEndings("\r\n");
        await File.WriteAllTextAsync(ArquivoDoTermo, outro, Ct);

        Assert.Equal(new ResultadoDoSeedBase(0, 0, 46, 0, 0, 0, 0, 0), await factory.SeedBaseAsync(Ct));
    }

    [Theory]
    [InlineData("termo.md")]
    [InlineData("termo-de-uso.v0.md")]
    [InlineData("termo-de-uso.vX.md")]
    public async Task Arquivo_fora_do_padrao_e_recusado_antes_de_tocar_no_banco(string nome)
    {
        await using var factory = await MigradoAsync(postgres, Dados);

        await File.WriteAllTextAsync(Path.Combine(Dados, "termos", nome), "Texto.\n", Ct);
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedBaseAsync(Ct));

        Assert.Contains(nome, falha.Message, StringComparison.Ordinal);
        Assert.Empty(await TermosAsync(factory));
    }

    private string ArquivoDoTermo => Path.Combine(Dados, "termos", "termo-de-uso.v1.md");

    private static async Task<Dictionary<int, string>> TermosAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        return await db.Set<TermoDeUso>().ToDictionaryAsync(termo => termo.Versao, termo => termo.Conteudo, Ct);
    }

    private static async Task<Guid> IdDoBimestreAsync(MaxxApiFactory factory, int numero)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        return await db.Set<Bimestre>().Where(bimestre => bimestre.Numero == numero).Select(bimestre => bimestre.Id).SingleAsync(Ct);
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql)
    {
        await using var command = new NpgsqlCommand(sql, conexao);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private void AlterarItem(string arquivo, string codigo, Action<JsonObject> alteracao) =>
        _copia.AlterarItem(arquivo, codigo, alteracao);

    private void RemoverItem(string arquivo, string codigo) => _copia.RemoverItem(arquivo, codigo);

    private void AlterarCalendario(Action<JsonNode> alteracao) => _copia.Alterar("calendario/2026.json", alteracao);
}
