using Maxx.Application.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Maxx.Infrastructure.Fila;

// Uma linha só (id 1): o último instante em que o worker passou pelo laço. Sem histórico, sem
// crescer: o que importa é se ele está vivo agora.
public sealed class BatimentoDoWorker
{
    public const int IdUnico = 1;

    public int Id { get; set; } = IdUnico;

    public DateTimeOffset BatidoEm { get; set; }
}

internal sealed class BatimentoDoWorkerConfiguration : IEntityTypeConfiguration<BatimentoDoWorker>
{
    public void Configure(EntityTypeBuilder<BatimentoDoWorker> builder)
    {
        builder.ToTable("batimento_do_worker", table =>
            table.HasCheckConstraint("ck_batimento_do_worker_unico", $"id = {BatimentoDoWorker.IdUnico}"));
        builder.HasKey(batimento => batimento.Id);
        builder.Property(batimento => batimento.Id).ValueGeneratedNever();
    }
}

// Grava o batimento, no máximo uma vez por intervalo: o laço do worker passa aqui a cada
// trabalho, e uma rajada de mil trabalhos não vira mil escritas.
public sealed class RegistroDeBatimento(
    IServiceScopeFactory scopes, TimeProvider clock, IOptions<SaudeDoWorkerOptions> options)
{
    private DateTimeOffset? _ultimo;

    public async Task BaterSeDevidoAsync(CancellationToken cancellationToken)
    {
        var agora = clock.GetUtcNow();
        if (_ultimo is { } ultimo && agora - ultimo < options.Value.IntervaloDoBatimento)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO batimento_do_worker (id, batido_em) VALUES ({BatimentoDoWorker.IdUnico}, {agora})
            ON CONFLICT (id) DO UPDATE SET batido_em = excluded.batido_em
            """,
            cancellationToken);
        _ultimo = agora;
    }
}

// Uma consulta só, nos índices que já existem: a busca do worker (ix_trabalho_pendente) e a
// chave da linha do batimento. A contagem de falhos varre só os falhos, que são poucos.
internal sealed class ConsultaDaSaudeDoWorker(
    NpgsqlDataSource dataSource, TimeProvider clock, IOptions<SaudeDoWorkerOptions> options) : IConsultaDaSaudeDoWorker
{
    public async Task<SaudeDoWorker> ConsultarAsync(CancellationToken cancellationToken)
    {
        var limites = options.Value;
        var agora = clock.GetUtcNow();

        await using var comando = dataSource.CreateCommand(
            """
            SELECT
              (SELECT batido_em FROM batimento_do_worker WHERE id = 1),
              (SELECT min(greatest(disponivel_em, criado_em)) FROM trabalho
                WHERE estado = 'pendente' AND disponivel_em <= @agora),
              (SELECT count(*) FROM trabalho WHERE estado = 'pendente' AND disponivel_em <= @agora),
              (SELECT count(*) FROM trabalho WHERE estado = 'falho' AND disponivel_em > @desde)
            """);
        comando.Parameters.AddWithValue("agora", agora);
        comando.Parameters.AddWithValue("desde", agora - limites.JanelaDeFalhos);

        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
        await leitor.ReadAsync(cancellationToken);
        return SaudeDoWorker.Avaliar(
            agora,
            leitor.IsDBNull(0) ? null : leitor.GetFieldValue<DateTimeOffset>(0),
            leitor.IsDBNull(1) ? null : leitor.GetFieldValue<DateTimeOffset>(1),
            (int)leitor.GetInt64(2),
            (int)leitor.GetInt64(3),
            limites);
    }
}
