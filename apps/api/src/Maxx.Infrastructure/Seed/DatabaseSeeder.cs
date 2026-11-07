using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.RaridadeDosItens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maxx.Infrastructure.Seed;

// `Maxx.Api seed <perfil>`. Idempotente: rodar de novo sem mudar os arquivos não muda o
// banco. Uma transação só, sob advisory lock: dois seeds ao mesmo tempo esperam um pelo
// outro, e um seed que falha não deixa nada gravado.
public sealed partial class DatabaseSeeder(
    MaxxDbContext db, IFilaDeTrabalhos fila, IOptions<SeedOptions> options, ILogger<DatabaseSeeder> logger, TimeProvider relogio)
{
    // Diferente da chave da migração, para os dois não se confundirem.
    public const long AdvisoryLockKey = 0x4D41_5858_0002;

    private string Dados => Path.GetFullPath(options.Value.DiretorioDeDados);

    public async Task SeedAsync(PerfilDeSeed perfil, CancellationToken cancellationToken)
    {
        switch (perfil)
        {
            case PerfilDeSeed.Base:
                await SeedBaseAsync(cancellationToken);
                break;
            case PerfilDeSeed.Demo:
                await SeedDemoAsync(cancellationToken);
                break;
            default:
                throw new SeedException($"Perfil {perfil} não existe.");
        }
    }

    public async Task<ResultadoDoSeedBase> SeedBaseAsync(CancellationToken cancellationToken)
    {
        // Tudo lido e validado antes de abrir a transação: arquivo inválido não toca no banco.
        var catalogo = CatalogoReader.Ler(Path.Combine(Dados, "catalogo"));
        if (!catalogo.Valido)
        {
            throw new SeedException(
                "Catálogo inválido, nada foi gravado:" + Environment.NewLine +
                string.Join(Environment.NewLine, catalogo.Erros.Select(erro => $"  {erro}")));
        }

        var calendario = CalendarioReader.Ler(Path.Combine(Dados, "calendario"));
        var termos = TermoDeUsoReader.Ler(Path.Combine(Dados, "termos"));

        await using var transacao = await AbrirComLockAsync(cancellationToken);

        var (familiasNovas, familiasAtualizadas) = await GravarFamiliasAsync(catalogo.Catalogo!, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var (itensNovos, itensAtualizados, itensInalterados) = await GravarItensAsync(catalogo.Catalogo!, cancellationToken);
        var (anosNovos, anosAtualizados) = await GravarCalendarioAsync(calendario, cancellationToken);
        var termosNovos = await GravarTermosAsync(termos, cancellationToken);
        await PedirRaridadeAsync(itensNovos + itensAtualizados > 0, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await transacao.CommitAsync(cancellationToken);

        var resultado = new ResultadoDoSeedBase(
            itensNovos, itensAtualizados, itensInalterados, familiasNovas, familiasAtualizadas, anosNovos, anosAtualizados, termosNovos);
        LogDone(logger, PerfilDeSeed.Base, resultado);
        return resultado;
    }

    // Item novo, item mudado ou item ainda sem raridade calculada: um pedido de recálculo ao
    // worker, para a tela nunca ficar sem raridade (produto.md 4.3). Só um: com um pedido
    // pendente, rodar de novo não acrescenta nada, e o seed segue idempotente.
    private async Task PedirRaridadeAsync(bool catalogoMudou, CancellationToken cancellationToken)
    {
        var pendente = await db.Set<Trabalho>()
            .AnyAsync(trabalho => trabalho.Tipo == RecalcularRaridade.Tipo && trabalho.Estado == EstadoDoTrabalho.Pendente, cancellationToken);
        if (pendente)
        {
            return;
        }

        var semRaridade = await db.Set<ItemCatalogoRow>()
            .AnyAsync(item => !db.Set<RaridadeDoItemRow>().Any(raridade => raridade.ItemCodigo == item.Codigo), cancellationToken);
        if (catalogoMudou || semRaridade)
        {
            fila.Enfileirar(new RecalcularRaridade());
        }
    }

    // Base e demo usam a mesma chave: um espera o outro.
    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> AbrirComLockAsync(
        CancellationToken cancellationToken)
    {
        var transacao = await db.Database.BeginTransactionAsync(cancellationToken);
        LogWaitingForLock(logger);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", cancellationToken);
        return transacao;
    }

    private async Task<(int Novas, int Atualizadas)> GravarFamiliasAsync(
        Domain.Catalogo.Catalogo catalogo, CancellationToken cancellationToken)
    {
        var existentes = await db.Set<FamiliaRow>().ToDictionaryAsync(familia => familia.Codigo, cancellationToken);
        FalharSeSumiu("Família", existentes.Keys, catalogo.Familias.Select(familia => familia.Codigo));

        var (novas, atualizadas) = (0, 0);
        foreach (var nova in catalogo.Familias.Select(CatalogoRowMapper.ParaRow))
        {
            if (!existentes.TryGetValue(nova.Codigo, out var atual))
            {
                db.Add(nova);
                novas++;
            }
            else if (!CatalogoRowMapper.MesmoConteudo(atual, nova))
            {
                (atual.Nome, atual.Descricao, atual.Passos) = (nova.Nome, nova.Descricao, nova.Passos);
                atualizadas++;
            }
        }

        return (novas, atualizadas);
    }

    // Pelo código do item. Conteúdo mudou: a versão sobe, e os prêmios já concedidos
    // continuam com a versão da época (ADR 0007).
    private async Task<(int Novos, int Atualizados, int Inalterados)> GravarItensAsync(
        Domain.Catalogo.Catalogo catalogo, CancellationToken cancellationToken)
    {
        var existentes = await db.Set<ItemCatalogoRow>().ToDictionaryAsync(item => item.Codigo, cancellationToken);
        FalharSeSumiu("Item", existentes.Keys, catalogo.Itens.Select(item => item.Codigo));

        var (novos, atualizados, inalterados) = (0, 0, 0);
        foreach (var novo in catalogo.Itens.Select(CatalogoRowMapper.ParaRow))
        {
            if (!existentes.TryGetValue(novo.Codigo, out var atual))
            {
                db.Add(novo);
                novos++;
            }
            else if (CatalogoRowMapper.MesmoConteudo(atual, novo))
            {
                inalterados++;
            }
            else
            {
                CatalogoRowMapper.CopiarConteudo(novo, atual);
                atual.Versao++;
                atualizados++;
                LogItemVersion(logger, atual.Codigo, atual.Versao);
            }
        }

        return (novos, atualizados, inalterados);
    }

    // Pelo ano. Ano que já existe recebe as datas corrigidas, com os mesmos ids.
    private async Task<(int Novos, int Atualizados)> GravarCalendarioAsync(
        Calendario calendario, CancellationToken cancellationToken)
    {
        var existentes = await db.Set<AnoLetivo>()
            .Include(ano => ano.Semestres).ThenInclude(semestre => semestre.Bimestres)
            .ToDictionaryAsync(ano => ano.Ano, cancellationToken);
        FalharSeSumiu(
            "Ano letivo",
            existentes.Keys.Select(ano => ano.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            calendario.Anos.Select(ano => ano.Ano.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var (novos, atualizados) = (0, 0);
        foreach (var ano in calendario.Anos)
        {
            if (!existentes.TryGetValue(ano.Ano, out var atual))
            {
                db.Add(ano);
                novos++;
            }
            else if (atual.AtualizarDatas(ano))
            {
                atualizados++;
            }
        }

        // O calendário inteiro, já com as datas novas, ainda precisa fechar: um ano
        // corrigido não pode passar a se sobrepor a outro que não estava no arquivo.
        try
        {
            _ = new Calendario(existentes.Values.Concat(calendario.Anos.Where(ano => !existentes.ContainsKey(ano.Ano))));
        }
        catch (Domain.DomainException ex)
        {
            throw new SeedException($"Calendário inválido depois da correção: {ex.Message}", ex);
        }

        return (novos, atualizados);
    }

    // Versão publicada não muda nem sai: alguém pode tê-la aceitado. Texto novo é arquivo novo,
    // com a versão seguinte.
    private async Task<int> GravarTermosAsync(IReadOnlyDictionary<int, string> termos, CancellationToken cancellationToken)
    {
        var existentes = await db.Set<TermoDeUso>().ToDictionaryAsync(termo => termo.Versao, cancellationToken);

        foreach (var (versao, atual) in existentes)
        {
            if (!termos.TryGetValue(versao, out var noArquivo))
            {
                throw new SeedException($"Termo de uso v{versao} no banco e ausente dos arquivos. Nada foi gravado.");
            }

            if (noArquivo != atual.Conteudo)
            {
                throw new SeedException(
                    $"O texto do termo de uso v{versao} mudou, e versão publicada não muda. Nada foi gravado. " +
                    "Publique o texto novo como a versão seguinte.");
            }
        }

        var novos = termos.Where(termo => !existentes.ContainsKey(termo.Key)).ToList();
        foreach (var (versao, conteudo) in novos)
        {
            db.Add(new TermoDeUso(versao, conteudo, relogio.GetUtcNow()));
        }

        return novos.Count;
    }

    // O arquivo é a fonte: o que está no banco e sumiu do arquivo é erro, não remoção.
    // Item se retira por vigencia.fim, porque prêmios apontam para ele.
    private static void FalharSeSumiu(string oQue, IEnumerable<string> noBanco, IEnumerable<string> noArquivo)
    {
        var sumiram = noBanco.Except(noArquivo, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (sumiram.Count > 0)
        {
            throw new SeedException(
                $"{oQue} no banco e ausente dos arquivos: {string.Join(", ", sumiram)}. Nada foi gravado. " +
                "Para retirar um item do catálogo, use vigencia.fim.");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Aguardando o lock do seed.")]
    private static partial void LogWaitingForLock(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Codigo} mudou: versão {Versao}.")]
    private static partial void LogItemVersion(ILogger logger, string codigo, int versao);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed {Perfil} concluído: {Resultado}.")]
    private static partial void LogDone(ILogger logger, PerfilDeSeed perfil, object resultado);
}
