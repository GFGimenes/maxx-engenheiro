using Maxx.Application.Feed;
using Maxx.Application.Paginacao;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Feed;

// Os dois trechos do feed em SQL direto: a comparação de linha do cursor e a chave
// aleatória da semente ficam à vista. Post removido pela instituição não entra em nenhum.
internal sealed class ConsultaDoFeed(MaxxDbContext db) : IConsultaDoFeed
{
    public async Task<IReadOnlyList<PosicaoNoTempo>> SeguidosAsync(
        Guid leitorId, PosicaoNoTempo? depoisDe, int limite, CancellationToken cancellationToken)
    {
        // Os autores são o leitor e quem ele segue; para cada um, o índice
        // (autor_id, criado_em desc, id desc) entrega os posts já na ordem.
        var linhas = depoisDe is { } posicao
            ? db.Database.SqlQuery<LinhaNoTempo>($"""
                SELECT p.criado_em AS "Instante", p.id AS "Id"
                FROM post p
                WHERE p.removido_em IS NULL
                  AND p.autor_id IN (SELECT {leitorId} UNION ALL SELECT s.seguido_id FROM seguimento s WHERE s.seguidor_id = {leitorId})
                  AND (p.criado_em, p.id) < ({posicao.Instante}, {posicao.Id})
                ORDER BY p.criado_em DESC, p.id DESC
                LIMIT {limite}
                """)
            : db.Database.SqlQuery<LinhaNoTempo>($"""
                SELECT p.criado_em AS "Instante", p.id AS "Id"
                FROM post p
                WHERE p.removido_em IS NULL
                  AND p.autor_id IN (SELECT {leitorId} UNION ALL SELECT s.seguido_id FROM seguimento s WHERE s.seguidor_id = {leitorId})
                ORDER BY p.criado_em DESC, p.id DESC
                LIMIT {limite}
                """);

        return [.. (await linhas.ToListAsync(cancellationToken)).Select(linha => new PosicaoNoTempo(linha.Instante, linha.Id))];
    }

    public async Task<IReadOnlyList<PosicaoAleatoria>> RecomendadosAsync(
        Guid leitorId, string semente, PosicaoAleatoria? depoisDe, int limite, CancellationToken cancellationToken)
    {
        // md5(semente || id): uma ordem embaralhada que não muda enquanto a semente for a
        // mesma, e que muda inteira com outra semente. COLLATE "C" na ordem e no cursor: as
        // duas comparações têm de ser a mesma, seja qual for a colação do banco.
        var linhas = depoisDe is { } posicao
            ? db.Database.SqlQuery<LinhaAleatoria>($"""
                SELECT r.chave AS "Chave", r.id AS "Id"
                FROM (
                    SELECT p.id, md5({semente} || p.id::text) COLLATE "C" AS chave
                    FROM post p
                    WHERE p.removido_em IS NULL
                      AND p.autor_id <> {leitorId}
                      AND p.autor_id NOT IN (SELECT s.seguido_id FROM seguimento s WHERE s.seguidor_id = {leitorId})
                ) r
                WHERE (r.chave, r.id) > ({posicao.Chave} COLLATE "C", {posicao.Id})
                ORDER BY r.chave, r.id
                LIMIT {limite}
                """)
            : db.Database.SqlQuery<LinhaAleatoria>($"""
                SELECT r.chave AS "Chave", r.id AS "Id"
                FROM (
                    SELECT p.id, md5({semente} || p.id::text) COLLATE "C" AS chave
                    FROM post p
                    WHERE p.removido_em IS NULL
                      AND p.autor_id <> {leitorId}
                      AND p.autor_id NOT IN (SELECT s.seguido_id FROM seguimento s WHERE s.seguidor_id = {leitorId})
                ) r
                ORDER BY r.chave, r.id
                LIMIT {limite}
                """);

        return [.. (await linhas.ToListAsync(cancellationToken)).Select(linha => new PosicaoAleatoria(linha.Chave, linha.Id))];
    }

    private sealed class LinhaNoTempo
    {
        public DateTimeOffset Instante { get; set; }

        public Guid Id { get; set; }
    }

    private sealed class LinhaAleatoria
    {
        public string Chave { get; set; } = string.Empty;

        public Guid Id { get; set; }
    }
}
