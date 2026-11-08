using System.Buffers.Text;
using System.Text;
using Maxx.Application;
using Maxx.Application.Medalhas;
using Maxx.Application.Paginacao;
using Maxx.Application.Perfil;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Perfil;

internal sealed class ConsultaDePerfil(MaxxDbContext db, IConsultaDoProfessor professores) : IConsultaDePerfil
{
    public async Task<PerfilLido?> PerfilAsync(Guid usuarioId, Guid leitorId, CancellationToken cancellationToken)
    {
        var linha = await (
                from usuario in db.Set<Usuario>()
                join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id into cursos
                from curso in cursos.DefaultIfEmpty()
                where usuario.Id == usuarioId
                select new
                {
                    usuario.Id,
                    usuario.NomeDeExibicao,
                    usuario.Prefixo,
                    usuario.Sufixo,
                    usuario.Papeis,
                    usuario.ExAlunoDesde,
                    usuario.ChaveDaFotoDoFeed,
                    usuario.ChaveDaFotoDaGrade,
                    cadastro.Ra,
                    cadastro.Semestre,
                    Curso = curso == null ? null : curso.Sigla,
                    NomeDoCurso = curso == null ? null : curso.Nome,
                    Seguidores = db.Set<Seguimento>().Count(seguimento => seguimento.SeguidoId == usuario.Id),
                    Seguindo = db.Set<Seguimento>().Count(seguimento => seguimento.SeguidorId == usuario.Id),
                    EuSigo = db.Set<Seguimento>().Any(seguimento => seguimento.SeguidorId == leitorId && seguimento.SeguidoId == usuario.Id),
                    Pontos = db.Set<Medalha>().Where(medalha => medalha.AlunoId == usuario.Id && medalha.AnuladaEm == null).Sum(medalha => medalha.Pontos)
                        + db.Set<PremioParticipacao>()
                            .Where(premio => premio.AlunoId == usuario.Id
                                && (premio.Estado == EstadoDoPremioParticipacao.Valido || premio.Estado == EstadoDoPremioParticipacao.Definitivo))
                            .Sum(premio => premio.Pontos),
                    MedalhasConcedidas = db.Set<Medalha>().Count(medalha => medalha.ConcedidaPorId == usuario.Id && medalha.AnuladaEm == null),
                })
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (linha is null || !(linha.Papeis.HasFlag(Papeis.Aluno) || linha.Papeis.HasFlag(Papeis.Professor)))
        {
            return null;
        }

        var ehAluno = linha.Papeis == Papeis.Aluno;
        return new PerfilLido(
            linha.Id,
            linha.NomeDeExibicao,
            new Arroba(linha.Prefixo, linha.Sufixo).ToString(),
            linha.Papeis,
            linha.Curso,
            linha.NomeDoCurso,
            linha.Semestre,
            linha.ExAlunoDesde is not null,
            ehAluno && linha.Id == leitorId ? linha.Ra : null,
            ehAluno ? linha.Pontos : null,
            linha.Seguidores,
            linha.Seguindo,
            linha.EuSigo,
            linha.ChaveDaFotoDoFeed,
            linha.ChaveDaFotoDaGrade,
            ehAluno ? null : new ProfessorDoPerfil(await professores.TurmasVigentesAsync(linha.Id, cancellationToken), linha.MedalhasConcedidas));
    }

    public async Task<Pagina<Guid>> PostsAsync(Guid autorId, PosicaoNoTempo? depoisDe, int limite, CancellationToken cancellationToken)
    {
        var posts = db.Set<Post>().Where(post => post.AutorId == autorId && post.RemovidoEm == null);
        if (depoisDe is { } posicao)
        {
            // Anda pelo índice (autor_id, criado_em desc, id desc), com o desempate do banco.
            posts = posts.Where(post => EF.Functions.LessThan(ValueTuple.Create(post.CriadoEm, post.Id), ValueTuple.Create(posicao.Instante, posicao.Id)));
        }

        var linhas = await posts
            .OrderByDescending(post => post.CriadoEm)
            .ThenByDescending(post => post.Id)
            .Take(limite + 1)
            .Select(post => new { post.Id, post.CriadoEm })
            .ToListAsync(cancellationToken);

        var pagina = linhas.Take(limite).ToList();
        return new Pagina<Guid>(
            [.. pagina.Select(linha => linha.Id)],
            linhas.Count > limite ? CursorDeTempo.Codificar(new PosicaoNoTempo(pagina[^1].CriadoEm, pagina[^1].Id)) : null);
    }

    public async Task<Pagina<AlunoEncontrado>> BuscarAlunosAsync(
        FiltroDaBusca filtro, string? cursor, int limite, Guid leitorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var depoisDe = CursorDaBusca.Ler(cursor);

        // O termo entra num LIKE: % e _ dele são texto, não curinga.
        var termo = filtro.Termo.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
        var curso = filtro.Curso ?? string.Empty;
        var semestre = filtro.Semestre ?? 0;
        var aluno = (int)Papeis.Aluno;
        var chaveDoCursor = depoisDe?.Chave ?? string.Empty;
        var idDoCursor = depoisDe?.Id ?? Guid.Empty;
        var comCursor = depoisDe is not null;

        // Nome sem acento e em minúsculas, pelo índice de trigramas da função maxx_sem_acento,
        // ou prefixo do arroba em minúsculas, pelo índice dele. A ordem é a do nome sem
        // acento, em COLLATE "C" na ordem e no cursor, para as duas comparações serem a mesma.
        var achados = await db.Database.SqlQuery<LinhaDaBusca>($"""
                SELECT u.id AS "Id", maxx_sem_acento(lower(u.nome_de_exibicao)) COLLATE "C" AS "Chave"
                FROM usuario u
                JOIN cadastro_elegivel c ON c.id = u.cadastro_elegivel_id
                LEFT JOIN curso k ON k.id = c.curso_id
                WHERE u.papeis = {aluno}
                  AND ({termo} = ''
                       OR maxx_sem_acento(lower(u.nome_de_exibicao)) LIKE '%' || maxx_sem_acento(lower({termo})) || '%'
                       OR lower(u.prefixo::text) LIKE '%' || lower({termo}) || '%')
                  AND ({curso} = '' OR lower(k.sigla) = lower({curso}))
                  AND ({semestre} = 0 OR c.semestre = {semestre})
                  AND (NOT {comCursor}
                       OR (maxx_sem_acento(lower(u.nome_de_exibicao)) COLLATE "C", u.id) > ({chaveDoCursor} COLLATE "C", {idDoCursor}))
                ORDER BY 2, 1
                LIMIT {limite + 1}
                """)
            .ToListAsync(cancellationToken);

        var pagina = achados.Take(limite).ToList();
        var ids = pagina.Select(linha => linha.Id).ToList();
        var detalhes = await (
                from usuario in db.Set<Usuario>()
                join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                join k in db.Set<Curso>() on cadastro.CursoId equals k.Id into cursos
                from k in cursos.DefaultIfEmpty()
                where ids.Contains(usuario.Id)
                select new
                {
                    usuario.Id,
                    usuario.NomeDeExibicao,
                    usuario.Prefixo,
                    usuario.Sufixo,
                    usuario.ExAlunoDesde,
                    usuario.ChaveDaFotoDaGrade,
                    cadastro.Semestre,
                    Curso = k == null ? null : k.Sigla,
                    EuSigo = db.Set<Seguimento>().Any(seguimento => seguimento.SeguidorId == leitorId && seguimento.SeguidoId == usuario.Id),
                })
            .AsNoTracking()
            .ToDictionaryAsync(linha => linha.Id, cancellationToken);

        return new Pagina<AlunoEncontrado>(
            [.. ids.Select(id => detalhes[id]).Select(linha => new AlunoEncontrado(
                linha.Id,
                linha.NomeDeExibicao,
                new Arroba(linha.Prefixo, linha.Sufixo).ToString(),
                linha.Curso,
                linha.Semestre,
                linha.ExAlunoDesde is not null,
                linha.ChaveDaFotoDaGrade,
                linha.EuSigo))],
            achados.Count > limite ? CursorDaBusca.Codificar(pagina[^1]) : null);
    }

    public async Task<IReadOnlyList<CursoDaBusca>> CursosAsync(CancellationToken cancellationToken) =>
        await db.Set<Curso>().OrderBy(curso => curso.Sigla).Select(curso => new CursoDaBusca(curso.Sigla, curso.Nome, curso.DuracaoEmSemestres)).ToListAsync(cancellationToken);

    public Task<bool> PrefixoDeOutraPessoaAsync(string prefixo, Guid usuarioId, CancellationToken cancellationToken) =>
        db.Set<PrefixoReservado>().AnyAsync(reservado => reservado.Prefixo == prefixo && reservado.UsuarioId != usuarioId, cancellationToken);

    private sealed class LinhaDaBusca
    {
        public Guid Id { get; set; }

        public string Chave { get; set; } = string.Empty;
    }

    // Opaco para o app: base64url de "b1|<id>|<nome sem acento>". O id vem antes porque tem
    // tamanho fixo; o nome pode ter qualquer caractere.
    private static class CursorDaBusca
    {
        public static string Codificar(LinhaDaBusca linha) =>
            Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"b1|{linha.Id:N}|{linha.Chave}"));

        public static LinhaDaBusca? Ler(string? cursor)
        {
            if (string.IsNullOrEmpty(cursor))
            {
                return null;
            }

            try
            {
                var texto = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor));
                if (texto.Length > 36 && texto.StartsWith("b1|", StringComparison.Ordinal) && texto[35] == '|'
                    && Guid.TryParseExact(texto[3..35], "N", out var id))
                {
                    return new LinhaDaBusca { Id = id, Chave = texto[36..] };
                }
            }
            catch (FormatException)
            {
            }

            throw new RecusaException(TipoDeRecusa.Invalido, CodigosDaPaginacao.CursorInvalido, "Cursor da busca malformado.");
        }
    }
}
