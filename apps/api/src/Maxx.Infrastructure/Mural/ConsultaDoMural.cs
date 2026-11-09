using Maxx.Application.Mural;
using Maxx.Application.Paginacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Mural;
using Maxx.Domain.Notificacoes;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Mural;

internal sealed class ConsultaDoMural(MaxxDbContext db) : IConsultaDoMural
{
    private static readonly string _avisoNovo = NomeJson.De(TipoDeNotificacao.AvisoNovo);

    public Task<LeitorDoMural?> LeitorAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        db.Set<Usuario>()
            .Where(usuario => usuario.Id == usuarioId)
            .Join(db.Set<CadastroElegivel>(), usuario => usuario.CadastroElegivelId, cadastro => cadastro.Id, (usuario, cadastro) => new
            {
                usuario.Id,
                CadastroId = cadastro.Id,
                cadastro.CursoId,
                usuario.Papeis,
            })
            .Select(linha => new LeitorDoMural(
                linha.Id, linha.CadastroId, linha.CursoId, (linha.Papeis & Papeis.Instituicao) == Papeis.Instituicao))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Pagina<AvisoLido>> MuralAsync(
        LeitorDoMural leitor,
        EscopoDoAviso? escopo,
        Guid? autorId,
        PosicaoNoTempo? depoisDe,
        int limite,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(leitor);

        var avisos = Visiveis(leitor);
        if (escopo is { } so)
        {
            avisos = avisos.Where(aviso => aviso.Escopo == so);
        }

        if (autorId is { } autor)
        {
            avisos = avisos.Where(aviso => aviso.AutorId == autor);
        }

        if (depoisDe is { } posicao)
        {
            // Anda pelo índice (publicado_em desc, id desc), com o desempate do banco.
            avisos = avisos.Where(aviso => EF.Functions.LessThan(
                ValueTuple.Create(aviso.PublicadoEm, aviso.Id), ValueTuple.Create(posicao.Instante, posicao.Id)));
        }

        var linhas = await Projetar(
                avisos.OrderByDescending(aviso => aviso.PublicadoEm).ThenByDescending(aviso => aviso.Id).Take(limite + 1), leitor)
            .ToListAsync(cancellationToken);

        var pagina = linhas.Take(limite).ToList();
        return new Pagina<AvisoLido>(
            pagina,
            linhas.Count > limite ? CursorDeTempo.Codificar(new PosicaoNoTempo(pagina[^1].PublicadoEm, pagina[^1].Id)) : null);
    }

    public Task<AvisoLido?> AvisoAsync(LeitorDoMural leitor, Guid avisoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(leitor);
        return Projetar(Visiveis(leitor).Where(aviso => aviso.Id == avisoId), leitor).SingleOrDefaultAsync(cancellationToken);
    }

    // A leitura e a notificação do aviso juntas, numa transação: quem leu o aviso não fica
    // com a notificação dele acesa no sininho.
    public async Task<int> RegistrarLeituraAsync(
        LeitorDoMural leitor, IReadOnlyCollection<Guid>? avisoIds, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(leitor);

        var avisos = Visiveis(leitor);
        if (avisoIds is not null)
        {
            avisos = avisos.Where(aviso => avisoIds.Contains(aviso.Id));
        }

        var naoLidos = await avisos
            .Where(aviso => !db.Set<LeituraDeAviso>().Any(leitura => leitura.AvisoId == aviso.Id && leitura.UsuarioId == leitor.Id))
            .Select(aviso => aviso.Id)
            .ToArrayAsync(cancellationToken);
        if (naoLidos.Length == 0)
        {
            return 0;
        }

        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);
        var gravadas = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO leitura_de_aviso (usuario_id, aviso_id, lida_em)
            SELECT {leitor.Id}, id, {agora} FROM unnest({naoLidos}) AS id
            ON CONFLICT (usuario_id, aviso_id) DO NOTHING
            """,
            cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE notificacao SET lida_em = {agora}
            WHERE usuario_id = {leitor.Id} AND tipo = {_avisoNovo} AND referencia_id = ANY({naoLidos}) AND lida_em IS NULL
            """,
            cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return gravadas;
    }

    // A regra do mural (IConsultaDoMural): a instituição vê tudo; os outros, a faculdade, o
    // que publicaram, o curso atual e as turmas em que têm matrícula.
    private IQueryable<Aviso> Visiveis(LeitorDoMural leitor)
    {
        var avisos = db.Set<Aviso>().AsNoTracking();
        if (leitor.Instituicao)
        {
            return avisos;
        }

        var curso = leitor.CursoId;
        var cadastro = leitor.CadastroId;
        return avisos.Where(aviso =>
            aviso.Escopo == EscopoDoAviso.Faculdade
            || aviso.AutorId == leitor.Id
            || (aviso.Escopo == EscopoDoAviso.Curso
                && db.Set<AvisoCurso>().Any(alvo => alvo.AvisoId == aviso.Id && alvo.CursoId == curso))
            || (aviso.Escopo == EscopoDoAviso.Turma
                && db.Set<AvisoTurma>().Any(alvo => alvo.AvisoId == aviso.Id
                    && db.Set<Matricula>().Any(matricula => matricula.TurmaId == alvo.TurmaId && matricula.AlunoId == cadastro))));
    }

    private IQueryable<AvisoLido> Projetar(IQueryable<Aviso> avisos, LeitorDoMural leitor) =>
        avisos
            .Join(db.Set<Usuario>(), aviso => aviso.AutorId, usuario => usuario.Id, (aviso, autor) => new { aviso, autor })
            .Select(par => new AvisoLido(
                par.aviso.Id,
                par.aviso.Escopo,
                par.aviso.Titulo,
                par.aviso.Texto,
                par.aviso.ChaveDaFotoDoFeed,
                par.aviso.ChaveDaFotoDaGrade,
                par.aviso.PublicadoEm,
                new AutorDoAviso(
                    par.autor.Id,
                    par.autor.NomeDeExibicao,
                    par.aviso.Escopo != EscopoDoAviso.Turma && (par.autor.Papeis & Papeis.Instituicao) == Papeis.Instituicao),
                db.Set<AvisoTurma>()
                    .Where(alvo => alvo.AvisoId == par.aviso.Id)
                    .Join(db.Set<Turma>(), alvo => alvo.TurmaId, turma => turma.Id, (_, turma) => turma)
                    .Join(db.Set<Materia>(), turma => turma.MateriaId, materia => materia.Id, (_, materia) => materia.Nome)
                    .Distinct()
                    .OrderBy(nome => nome)
                    .ToList(),
                db.Set<AvisoCurso>()
                    .Where(alvo => alvo.AvisoId == par.aviso.Id)
                    .Join(db.Set<Curso>(), alvo => alvo.CursoId, curso => curso.Id, (_, curso) => curso.Sigla)
                    .OrderBy(sigla => sigla)
                    .ToList(),
                db.Set<LeituraDeAviso>().Any(leitura => leitura.AvisoId == par.aviso.Id && leitura.UsuarioId == leitor.Id)));
}
