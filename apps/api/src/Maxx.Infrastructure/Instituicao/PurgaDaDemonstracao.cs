using Maxx.Application.Fila;
using Maxx.Application.Fotos;
using Maxx.Application.Instituicao;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
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
//
// Desde o passo 14 (P1), o lote tem também professores e coordenações, e eles não são
// apagados: o que fizeram em contas fora do lote fica, com o autor. A conta deles é
// encerrada, sem chave, sessão, foto, vínculo de turma nem presença social; a linha da conta e
// a da base ficam, só como autoras da auditoria.
internal sealed class PurgaDaDemonstracaoRepository(MaxxDbContext db, IFilaDeTrabalhos fila) : IPurgaDaDemonstracao
{
    // Chaves por trabalho de remoção: cada um faz poucas chamadas ao bucket e cabe nos 60 s.
    internal const int FotosPorTrabalho = 200;

    private const int _aluno = (int)Papeis.Aluno;
    private const int _professor = (int)Papeis.Professor;

    // Sem parâmetro: tudo sai pela tabela temporária alvo, dos alunos do lote.
    private static readonly string[] _apagarAlunos =
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

    // Pela tabela temporária equipe, dos professores e das coordenações do lote. Ficam as
    // linhas que outras apontam (a conta, a base, o evento digitado na concessão direta, o
    // aviso e a foto dele, que o aviso guarda por conta própria).
    private static readonly string[] _encerrarEquipe =
    [
        "DELETE FROM familia_sessao WHERE usuario_id IN (SELECT usuario_id FROM equipe)",
        "DELETE FROM recuperacao_senha WHERE usuario_id IN (SELECT usuario_id FROM equipe)",
        "DELETE FROM notificacao WHERE usuario_id IN (SELECT usuario_id FROM equipe)",
        "DELETE FROM leitura_de_aviso WHERE usuario_id IN (SELECT usuario_id FROM equipe)",
        "DELETE FROM curtida WHERE usuario_id IN (SELECT usuario_id FROM equipe)",
        "DELETE FROM comentario WHERE autor_id IN (SELECT usuario_id FROM equipe)",
        "DELETE FROM seguimento WHERE seguidor_id IN (SELECT usuario_id FROM equipe) OR seguido_id IN (SELECT usuario_id FROM equipe)",
        "DELETE FROM upload_pendente WHERE usuario_id IN (SELECT usuario_id FROM equipe)",
        "DELETE FROM vinculo_professor WHERE professor_id IN (SELECT cadastro_id FROM equipe)",
    ];

    public async Task<IReadOnlyList<LoteDaDemonstracao>> LotesAsync(CancellationToken cancellationToken) =>
        [.. (await db.Set<Usuario>()
                .Where(usuario => usuario.LoteDaDemonstracao != null && usuario.EncerradaEm == null)
                .GroupBy(usuario => usuario.LoteDaDemonstracao!)
                .Select(grupo => new
                {
                    Lote = grupo.Key,
                    Alunos = grupo.Count(usuario => usuario.Papeis == Papeis.Aluno),
                    Professores = grupo.Count(usuario => usuario.Papeis == Papeis.Professor),
                    Coordenacoes = grupo.Count(usuario => usuario.Papeis == Papeis.Instituicao),
                    PrimeiroEm = grupo.Min(usuario => usuario.CriadoEm),
                    UltimoEm = grupo.Max(usuario => usuario.CriadoEm),
                })
                .ToListAsync(cancellationToken))
            .OrderByDescending(lote => lote.UltimoEm)
            .ThenBy(lote => lote.Lote, StringComparer.Ordinal)
            .Select(lote => new LoteDaDemonstracao(lote.Lote, lote.Alunos, lote.Professores, lote.Coordenacoes, lote.PrimeiroEm, lote.UltimoEm))];

    public async Task<ResultadoDaPurga?> PurgarAsync(
        string lote, Guid purgadoPorId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

        // Trava as contas ativas do lote: a segunda purga do mesmo lote, ao mesmo tempo, espera
        // esta terminar e já não acha ninguém, porque os alunos sumiram e a equipe está
        // encerrada.
        var contas = await db.Database.SqlQuery<int>($"""
                SELECT papeis AS "Value" FROM usuario
                WHERE lote_da_demonstracao = {lote} AND encerrada_em IS NULL
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);
        if (contas.Count == 0)
        {
            return null;
        }

        await db.Database.ExecuteSqlAsync($"""
            CREATE TEMP TABLE alvo ON COMMIT DROP AS
              SELECT id AS usuario_id, cadastro_elegivel_id AS cadastro_id
              FROM usuario WHERE lote_da_demonstracao = {lote} AND papeis = {_aluno}
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            CREATE TEMP TABLE equipe ON COMMIT DROP AS
              SELECT id AS usuario_id, cadastro_elegivel_id AS cadastro_id
              FROM usuario WHERE lote_da_demonstracao = {lote} AND papeis <> {_aluno} AND encerrada_em IS NULL
            """, cancellationToken);

        // As fotos antes de apagar quem as aponta. O upload confirmado repete as chaves do
        // post ou da foto de perfil; o Distinct junta. Da equipe, saem a foto de perfil e os
        // uploads; a foto de um aviso, que fica, não.
        var fotos = await db.Database.SqlQuery<string>($"""
                SELECT chave AS "Value" FROM (
                  SELECT chave_da_foto_do_feed AS chave FROM post WHERE autor_id IN (SELECT usuario_id FROM alvo)
                  UNION SELECT chave_da_foto_da_grade FROM post WHERE autor_id IN (SELECT usuario_id FROM alvo)
                  UNION SELECT chave_da_foto_do_feed FROM usuario WHERE id IN (SELECT usuario_id FROM alvo)
                  UNION SELECT chave_da_foto_da_grade FROM usuario WHERE id IN (SELECT usuario_id FROM alvo)
                  UNION SELECT chave_do_feed FROM upload_pendente WHERE usuario_id IN (SELECT usuario_id FROM alvo)
                  UNION SELECT chave_da_grade FROM upload_pendente WHERE usuario_id IN (SELECT usuario_id FROM alvo)
                  UNION SELECT chave_da_foto_do_feed FROM usuario WHERE id IN (SELECT usuario_id FROM equipe)
                  UNION SELECT chave_da_foto_da_grade FROM usuario WHERE id IN (SELECT usuario_id FROM equipe)
                  UNION SELECT chave_do_feed FROM upload_pendente WHERE usuario_id IN (SELECT usuario_id FROM equipe)
                  UNION SELECT chave_da_grade FROM upload_pendente WHERE usuario_id IN (SELECT usuario_id FROM equipe)
                ) chaves
                WHERE chave IS NOT NULL
                  AND chave NOT IN (
                    SELECT chave_da_foto_do_feed FROM aviso WHERE chave_da_foto_do_feed IS NOT NULL
                    UNION SELECT chave_da_foto_da_grade FROM aviso WHERE chave_da_foto_da_grade IS NOT NULL)
                ORDER BY 1
                """)
            .ToListAsync(cancellationToken);

        foreach (var comando in _apagarAlunos.Concat(_encerrarEquipe))
        {
            await db.Database.ExecuteSqlRawAsync(comando, cancellationToken);
        }

        await db.Database.ExecuteSqlAsync($"""
            UPDATE usuario
            SET encerrada_em = {agora}, hash_da_chave_do_dispositivo = NULL,
                chave_da_foto_do_feed = NULL, chave_da_foto_da_grade = NULL
            WHERE id IN (SELECT usuario_id FROM equipe)
            """, cancellationToken);

        var alunos = contas.Count(papeis => papeis == _aluno);
        var professores = contas.Count(papeis => papeis == _professor);
        var coordenacoes = contas.Count - alunos - professores;
        db.Add(new PurgaDaDemonstracao(lote, alunos, professores, coordenacoes, fotos.Count, purgadoPorId, agora));
        foreach (var parte in fotos.Chunk(FotosPorTrabalho))
        {
            fila.Enfileirar(new RemoverFotos(parte));
        }

        // Pontos e alunos saem: mudam o ranking e o divisor da raridade.
        fila.PedirRecalculos();
        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        return new ResultadoDaPurga(lote, alunos, professores, coordenacoes, fotos.Count);
    }
}
