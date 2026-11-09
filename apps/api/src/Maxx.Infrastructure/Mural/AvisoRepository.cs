using Maxx.Application.Mural;
using Maxx.Domain.Academico;
using Maxx.Domain.Mural;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Mural;

internal sealed class AvisoRepository(MaxxDbContext db) : IAvisoRepository
{
    public void Adicionar(Aviso aviso) => db.Add(aviso);

    public Task<int> CursosExistentesAsync(IReadOnlyCollection<Guid> cursoIds, CancellationToken cancellationToken) =>
        db.Set<Curso>().CountAsync(curso => cursoIds.Contains(curso.Id), cancellationToken);
}

// A notificação de aviso novo, em lote: um INSERT ... SELECT só, na transação do trabalho.
// O índice único da notificação (usuário, tipo, referência) faz o ON CONFLICT: quem já tem
// a notificação fica como está, e o trabalho pode rodar de novo.
internal sealed class DistribuicaoDeAviso(MaxxDbContext db) : IDistribuicaoDeAviso
{
    private static readonly string _avisoNovo = NomeJson.De(Domain.Notificacoes.TipoDeNotificacao.AvisoNovo);
    private static readonly string _faculdade = NomeJson.De(EscopoDoAviso.Faculdade);
    private static readonly string _curso = NomeJson.De(EscopoDoAviso.Curso);
    private static readonly string _turma = NomeJson.De(EscopoDoAviso.Turma);

    // Papéis gravados como bits (Papeis): aluno 1, professor 2. O aluno nunca acumula papel.
    public Task<int> NotificarAsync(Guid avisoId, DateTimeOffset agora, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO notificacao (id, usuario_id, tipo, referencia_id, criada_em)
            SELECT uuidv7(), u.id, {_avisoNovo}, a.id, {agora}
            FROM aviso a
            JOIN usuario u ON u.id <> a.autor_id AND u.criado_em <= a.publicado_em
            WHERE a.id = {avisoId}
              AND (
                (u.papeis = {(int)Papeis.Aluno}
                  AND (u.ex_aluno_desde IS NULL OR u.ex_aluno_desde > a.publicado_em)
                  AND (
                    a.escopo = {_faculdade}
                    OR (a.escopo = {_curso} AND EXISTS (
                      SELECT 1 FROM cadastro_elegivel c
                      JOIN aviso_curso ac ON ac.curso_id = c.curso_id
                      WHERE c.id = u.cadastro_elegivel_id AND ac.aviso_id = a.id))
                    OR (a.escopo = {_turma} AND EXISTS (
                      SELECT 1 FROM matricula m
                      JOIN aviso_turma at ON at.turma_id = m.turma_id
                      WHERE m.aluno_id = u.cadastro_elegivel_id AND at.aviso_id = a.id))))
                OR ((u.papeis & {(int)Papeis.Professor}) <> 0 AND a.escopo = {_faculdade}))
            ON CONFLICT (usuario_id, tipo, referencia_id) DO NOTHING
            """,
            cancellationToken);
}
