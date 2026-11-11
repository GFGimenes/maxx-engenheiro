using Maxx.Application.Fila;
using Maxx.Application.Fotos;
using Maxx.Application.Instituicao;
using Maxx.Application.Ranking;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Instituicao;

// O SQL da purga de decisoes/README.md (passo 5), que crescia a cada passo, agora em código e
// numa transação só, com as tabelas que chegaram depois. A ordem respeita as chaves: quem
// segura outra linha sai antes dela. O que pendura no usuário por cascata (prefixos, aceites
// do termo, refresh, posições no ranking) sai com ele; as evidências, com a conquista; as
// curtidas e os comentários de outras pessoas nos posts do lote, com o post; os registros de
// correção de e-mail e troca de curso, com a linha da base.
internal sealed class PurgaDaDemonstracaoRepository(MaxxDbContext db, IFilaDeTrabalhos fila) : IPurgaDaDemonstracao
{
    // Chaves por trabalho de remoção: cada um faz poucas chamadas ao bucket e cabe nos 60 s.
    internal const int FotosPorTrabalho = 200;

    // Sem parâmetro: tudo sai pela tabela temporária alvo.
    private static readonly string[] _apagar =
    [
        "DELETE FROM familia_sessao WHERE usuario_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM recuperacao_senha WHERE usuario_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM notificacao WHERE usuario_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM leitura_de_aviso WHERE usuario_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM curtida WHERE usuario_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM comentario WHERE autor_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM seguimento WHERE seguidor_id IN (SELECT usuario_id FROM alvo) OR seguido_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM comprovacao WHERE aluno_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM post WHERE autor_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM progresso_de_conquista WHERE aluno_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM conquista WHERE aluno_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM contestacao WHERE premio_participacao_id IN (SELECT id FROM premio_participacao WHERE aluno_id IN (SELECT usuario_id FROM alvo))",
        "DELETE FROM premio_participacao WHERE aluno_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM evento WHERE criado_por_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM medalha WHERE aluno_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM upload_pendente WHERE usuario_id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM usuario WHERE id IN (SELECT usuario_id FROM alvo)",
        "DELETE FROM matricula WHERE aluno_id IN (SELECT cadastro_id FROM alvo)",
        "DELETE FROM cadastro_elegivel WHERE id IN (SELECT cadastro_id FROM alvo) AND demonstracao",
    ];

    public async Task<IReadOnlyList<LoteDaDemonstracao>> LotesAsync(CancellationToken cancellationToken) =>
        [.. (await db.Set<Usuario>()
                .Where(usuario => usuario.LoteDaDemonstracao != null)
                .GroupBy(usuario => usuario.LoteDaDemonstracao!)
                .Select(grupo => new
                {
                    Lote = grupo.Key,
                    Alunos = grupo.Count(),
                    PrimeiroEm = grupo.Min(usuario => usuario.CriadoEm),
                    UltimoEm = grupo.Max(usuario => usuario.CriadoEm),
                })
                .ToListAsync(cancellationToken))
            .OrderByDescending(lote => lote.UltimoEm)
            .ThenBy(lote => lote.Lote, StringComparer.Ordinal)
            .Select(lote => new LoteDaDemonstracao(lote.Lote, lote.Alunos, lote.PrimeiroEm, lote.UltimoEm))];

    public async Task<ResultadoDaPurga?> PurgarAsync(
        string lote, Guid purgadoPorId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

        // Trava as contas do lote: a segunda purga do mesmo lote, ao mesmo tempo, espera esta
        // terminar e já não acha ninguém.
        var alunos = await db.Database.SqlQuery<Guid>($"""
                SELECT id AS "Value" FROM usuario WHERE lote_da_demonstracao = {lote} FOR UPDATE
                """)
            .ToListAsync(cancellationToken);
        if (alunos.Count == 0)
        {
            return null;
        }

        await db.Database.ExecuteSqlAsync($"""
            CREATE TEMP TABLE alvo ON COMMIT DROP AS
              SELECT id AS usuario_id, cadastro_elegivel_id AS cadastro_id
              FROM usuario WHERE lote_da_demonstracao = {lote}
            """, cancellationToken);

        // As fotos antes de apagar quem as aponta. O upload confirmado repete as chaves do
        // post ou da foto de perfil; o Distinct junta.
        var fotos = await db.Database.SqlQuery<string>($"""
                SELECT chave AS "Value" FROM (
                  SELECT chave_da_foto_do_feed AS chave FROM post WHERE autor_id IN (SELECT usuario_id FROM alvo)
                  UNION SELECT chave_da_foto_da_grade FROM post WHERE autor_id IN (SELECT usuario_id FROM alvo)
                  UNION SELECT chave_da_foto_do_feed FROM usuario WHERE id IN (SELECT usuario_id FROM alvo)
                  UNION SELECT chave_da_foto_da_grade FROM usuario WHERE id IN (SELECT usuario_id FROM alvo)
                  UNION SELECT chave_do_feed FROM upload_pendente WHERE usuario_id IN (SELECT usuario_id FROM alvo)
                  UNION SELECT chave_da_grade FROM upload_pendente WHERE usuario_id IN (SELECT usuario_id FROM alvo)
                ) chaves
                WHERE chave IS NOT NULL
                ORDER BY 1
                """)
            .ToListAsync(cancellationToken);

        foreach (var comando in _apagar)
        {
            await db.Database.ExecuteSqlRawAsync(comando, cancellationToken);
        }

        db.Add(new PurgaDaDemonstracao(lote, alunos.Count, professores: 0, coordenacoes: 0, fotos.Count, purgadoPorId, agora));
        foreach (var parte in fotos.Chunk(FotosPorTrabalho))
        {
            fila.Enfileirar(new RemoverFotos(parte));
        }

        // Pontos e alunos saem: mudam o ranking e o divisor da raridade.
        fila.PedirRecalculos();
        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        return new ResultadoDaPurga(lote, alunos.Count, fotos.Count);
    }
}
