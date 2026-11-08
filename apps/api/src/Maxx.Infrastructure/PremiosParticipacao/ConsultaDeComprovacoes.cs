using Maxx.Application.Paginacao;
using Maxx.Application.Posts;
using Maxx.Application.PremiosParticipacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.PremiosParticipacao;

internal sealed class ConsultaDeComprovacoes(MaxxDbContext db) : IConsultaDeComprovacoes
{
    public async Task<Pagina<ComprovacaoNaFila>> PendentesAsync(
        EscopoDaFila escopo, Guid? eventoId, PosicaoNoTempo? depoisDe, int limite, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var comprovacoes = Pendentes(escopo, agora);
        if (eventoId is { } filtro)
        {
            comprovacoes = comprovacoes.Where(comprovacao => comprovacao.EventoId == filtro);
        }

        if (depoisDe is { } posicao)
        {
            // Comparação de linha do Postgres, (criada_em, id) > (instante, id), pelo índice.
            comprovacoes = comprovacoes.Where(comprovacao =>
                EF.Functions.GreaterThan(ValueTuple.Create(comprovacao.CriadaEm, comprovacao.Id), ValueTuple.Create(posicao.Instante, posicao.Id)));
        }

        // Um a mais que o limite: se ele vier, existe página seguinte.
        var linhas = await (
                from comprovacao in comprovacoes
                join premio in db.Set<PremioParticipacao>() on comprovacao.PremioParticipacaoId equals premio.Id
                join item in db.Set<ItemCatalogoRow>() on premio.ItemCodigo equals item.Codigo
                join evento in db.Set<Evento>() on comprovacao.EventoId equals evento.Id
                join aluno in db.Set<Usuario>() on comprovacao.AlunoId equals aluno.Id
                join cadastro in db.Set<CadastroElegivel>() on aluno.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id into cursos
                from curso in cursos.DefaultIfEmpty()
                join post in db.Set<Post>() on comprovacao.PostId equals post.Id into postsDaComprovacao
                from post in postsDaComprovacao.DefaultIfEmpty()
                join materia in db.Set<Materia>() on premio.MateriaId equals materia.Id into materias
                from materia in materias.DefaultIfEmpty()
                orderby comprovacao.CriadaEm, comprovacao.Id
                select new
                {
                    comprovacao.Id,
                    PremioId = premio.Id,
                    comprovacao.PostId,
                    comprovacao.CriadaEm,
                    AlunoId = aluno.Id,
                    CadastroId = cadastro.Id,
                    aluno.NomeDeExibicao,
                    aluno.Prefixo,
                    aluno.Sufixo,
                    FotoDoAluno = aluno.ChaveDaFotoDaGrade,
                    Curso = curso == null ? null : curso.Sigla,
                    cadastro.Semestre,
                    premio.ItemCodigo,
                    NomeDoItem = item.Nome,
                    premio.Tier,
                    premio.Pontos,
                    EventoId = evento.Id,
                    EventoNome = evento.Nome,
                    evento.Inicio,
                    evento.Fim,
                    evento.Origem,
                    Materia = materia == null ? null : materia.Nome,
                    ChaveDoFeed = post == null ? null : post.ChaveDaFotoDoFeed,
                    ChaveDaGrade = post == null ? null : post.ChaveDaFotoDaGrade,
                    Legenda = post == null ? null : post.Legenda,
                })
            .Take(limite + 1)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var pagina = linhas.Take(limite).ToList();
        var materiasPorAluno = await MateriasAsync(escopo, [.. pagina.Select(linha => linha.CadastroId).Distinct()], agora, cancellationToken);

        return new Pagina<ComprovacaoNaFila>(
            [.. pagina.Select(linha => new ComprovacaoNaFila(
                linha.Id,
                linha.PremioId,
                linha.PostId,
                new AutorDoPost(linha.AlunoId, linha.NomeDeExibicao, new Arroba(linha.Prefixo, linha.Sufixo).ToString(), linha.Curso, linha.Semestre, linha.FotoDoAluno),
                linha.ItemCodigo,
                linha.NomeDoItem,
                linha.Tier,
                linha.Pontos,
                new EventoDaComprovacao(linha.EventoId, linha.EventoNome, linha.Inicio, linha.Fim, linha.Origem == OrigemDoEvento.TextoLivre),
                linha.Materia,
                linha.ChaveDoFeed,
                linha.ChaveDaGrade,
                linha.Legenda,
                linha.CriadaEm,
                Comprovacao.PassouDoPrazo(linha.CriadaEm, agora),
                materiasPorAluno.GetValueOrDefault(linha.CadastroId, [])))],
            linhas.Count > limite ? CursorDeTempo.Codificar(new PosicaoNoTempo(pagina[^1].CriadaEm, pagina[^1].Id)) : null);
    }

    public async Task<IReadOnlyList<EventoNaFila>> EventosAsync(EscopoDaFila escopo, DateTimeOffset agora, CancellationToken cancellationToken) =>
        await (
                from comprovacao in Pendentes(escopo, agora)
                join evento in db.Set<Evento>() on comprovacao.EventoId equals evento.Id
                group comprovacao by new { evento.Id, evento.Nome, evento.Inicio } into porEvento
                orderby porEvento.Key.Inicio descending, porEvento.Key.Nome
                select new EventoNaFila(porEvento.Key.Id, porEvento.Key.Nome, porEvento.Key.Inicio, porEvento.Count()))
            .ToListAsync(cancellationToken);

    // As comprovações de prêmio ainda reivindicado, no escopo de quem lê.
    private IQueryable<Comprovacao> Pendentes(EscopoDaFila escopo, DateTimeOffset agora)
    {
        var pendentes =
            from comprovacao in db.Set<Comprovacao>()
            join premio in db.Set<PremioParticipacao>() on comprovacao.PremioParticipacaoId equals premio.Id
            where premio.Estado == EstadoDoPremioParticipacao.Reivindicado
            select comprovacao;

        if (escopo.Instituicao)
        {
            return pendentes;
        }

        var alunos = AlunosDoProfessor(escopo.UsuarioId, agora);
        return
            from comprovacao in pendentes
            join aluno in db.Set<Usuario>() on comprovacao.AlunoId equals aluno.Id
            where alunos.Contains(aluno.CadastroElegivelId)
            select comprovacao;
    }

    // Os cadastros dos alunos matriculados em turma do professor no semestre vigente.
    private IQueryable<Guid> AlunosDoProfessor(Guid professorId, DateTimeOffset agora)
    {
        var semestres = SemestreVigente.Ids(db, FusoHorario.DataLocal(agora));
        return
            from professor in db.Set<Usuario>()
            join vinculo in db.Set<VinculoProfessor>() on professor.CadastroElegivelId equals vinculo.ProfessorId
            join turma in db.Set<Turma>() on vinculo.TurmaId equals turma.Id
            join matricula in db.Set<Matricula>() on turma.Id equals matricula.TurmaId
            where professor.Id == professorId && semestres.Contains(turma.SemestreLetivoId)
            select matricula.AlunoId;
    }

    // As matérias que cada aluno cursa no semestre vigente; para o professor, só as que ele
    // também leciona (D13). Em ordem alfabética.
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> MateriasAsync(
        EscopoDaFila escopo, IReadOnlyCollection<Guid> cadastros, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        if (cadastros.Count == 0)
        {
            return [];
        }

        var semestres = SemestreVigente.Ids(db, FusoHorario.DataLocal(agora));
        var turmasDoProfessor =
            from professor in db.Set<Usuario>()
            join vinculo in db.Set<VinculoProfessor>() on professor.CadastroElegivelId equals vinculo.ProfessorId
            where professor.Id == escopo.UsuarioId
            select vinculo.TurmaId;

        var linhas = await (
                from matricula in db.Set<Matricula>()
                join turma in db.Set<Turma>() on matricula.TurmaId equals turma.Id
                join materia in db.Set<Materia>() on turma.MateriaId equals materia.Id
                where cadastros.Contains(matricula.AlunoId)
                    && semestres.Contains(turma.SemestreLetivoId)
                    && (escopo.Instituicao || turmasDoProfessor.Contains(turma.Id))
                select new { matricula.AlunoId, materia.Nome })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return linhas.GroupBy(linha => linha.AlunoId).ToDictionary(
            grupo => grupo.Key,
            grupo => (IReadOnlyList<string>)[.. grupo.Select(linha => linha.Nome).Distinct().Order(StringComparer.Ordinal)]);
    }
}
