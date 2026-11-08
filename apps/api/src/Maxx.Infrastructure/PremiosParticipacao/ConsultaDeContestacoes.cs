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

internal sealed class ConsultaDeContestacoes(MaxxDbContext db) : IConsultaDeContestacoes
{
    public async Task<Pagina<ContestacaoNaFila>> AbertasAsync(
        PosicaoNoTempo? depoisDe, int limite, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var abertas = db.Set<Contestacao>().Where(contestacao => contestacao.Decisao == null);
        if (depoisDe is { } posicao)
        {
            abertas = abertas.Where(contestacao =>
                EF.Functions.GreaterThan(ValueTuple.Create(contestacao.ContestadaEm, contestacao.Id), ValueTuple.Create(posicao.Instante, posicao.Id)));
        }

        var linhas = await (
                from contestacao in abertas
                join premio in db.Set<PremioParticipacao>() on contestacao.PremioParticipacaoId equals premio.Id
                join quemContestou in db.Set<Usuario>() on contestacao.ContestadaPorId equals quemContestou.Id
                join item in db.Set<ItemCatalogoRow>() on premio.ItemCodigo equals item.Codigo
                join evento in db.Set<Evento>() on premio.EventoId equals evento.Id
                join aluno in db.Set<Usuario>() on premio.AlunoId equals aluno.Id
                join cadastro in db.Set<CadastroElegivel>() on aluno.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id into cursos
                from curso in cursos.DefaultIfEmpty()
                join comprovacao in db.Set<Comprovacao>() on premio.Id equals comprovacao.PremioParticipacaoId into comprovacoes
                from comprovacao in comprovacoes.DefaultIfEmpty()
                join post in db.Set<Post>() on comprovacao.PostId equals post.Id into postsDoPremio
                from post in postsDoPremio.DefaultIfEmpty()
                join materia in db.Set<Materia>() on premio.MateriaId equals materia.Id into materias
                from materia in materias.DefaultIfEmpty()
                orderby contestacao.ContestadaEm, contestacao.Id
                select new
                {
                    contestacao.Id,
                    PremioId = premio.Id,
                    PostId = post == null ? (Guid?)null : post.Id,
                    AlunoId = aluno.Id,
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
                    contestacao.Motivo,
                    QuemPrefixo = quemContestou.Prefixo,
                    QuemSufixo = quemContestou.Sufixo,
                    contestacao.ContestadaEm,
                    premio.AprovadoEm,
                })
            .Take(limite + 1)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var pagina = linhas.Take(limite).ToList();
        return new Pagina<ContestacaoNaFila>(
            [.. pagina.Select(linha => new ContestacaoNaFila(
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
                linha.Motivo,
                new Arroba(linha.QuemPrefixo, linha.QuemSufixo).ToString(),
                linha.ContestadaEm,
                linha.AprovadoEm!.Value,
                Contestacao.PassouDoPrazo(linha.ContestadaEm, agora)))],
            linhas.Count > limite ? CursorDeTempo.Codificar(new PosicaoNoTempo(pagina[^1].ContestadaEm, pagina[^1].Id)) : null);
    }

    public async Task<IReadOnlyList<ReivindicacaoDoAluno>> ReivindicacoesAsync(Guid alunoId, CancellationToken cancellationToken)
    {
        var linhas = await (
                from premio in db.Set<PremioParticipacao>()
                join item in db.Set<ItemCatalogoRow>() on premio.ItemCodigo equals item.Codigo
                join evento in db.Set<Evento>() on premio.EventoId equals evento.Id
                join comprovacao in db.Set<Comprovacao>() on premio.Id equals comprovacao.PremioParticipacaoId into comprovacoes
                from comprovacao in comprovacoes.DefaultIfEmpty()
                join materia in db.Set<Materia>() on premio.MateriaId equals materia.Id into materias
                from materia in materias.DefaultIfEmpty()
                where premio.AlunoId == alunoId
                    && (premio.Estado == EstadoDoPremioParticipacao.Reivindicado
                        || premio.Estado == EstadoDoPremioParticipacao.EmAveriguacao
                        || premio.Estado == EstadoDoPremioParticipacao.Reprovado
                        || premio.Estado == EstadoDoPremioParticipacao.Revogado)
                select new
                {
                    premio.Id,
                    PostId = comprovacao == null ? null : comprovacao.PostId,
                    premio.ItemCodigo,
                    NomeDoItem = item.Nome,
                    premio.Tier,
                    premio.Pontos,
                    Evento = evento.Nome,
                    Materia = materia == null ? null : materia.Nome,
                    premio.Estado,
                    premio.MotivoDaReprovacao,
                    premio.MotivoDaRevogacao,
                    premio.ReivindicadoEm,
                    premio.ContestadoEm,
                    premio.ReprovadoEm,
                    premio.RevogadoEm,
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. linhas
            .Select(linha => new ReivindicacaoDoAluno(
                linha.Id,
                linha.PostId,
                linha.ItemCodigo,
                linha.NomeDoItem,
                linha.Tier,
                linha.Pontos,
                linha.Evento,
                linha.Materia,
                linha.Estado,
                linha.MotivoDaReprovacao ?? linha.MotivoDaRevogacao,
                linha.Estado switch
                {
                    EstadoDoPremioParticipacao.EmAveriguacao => linha.ContestadoEm!.Value,
                    EstadoDoPremioParticipacao.Reprovado => linha.ReprovadoEm!.Value,
                    EstadoDoPremioParticipacao.Revogado => linha.RevogadoEm!.Value,
                    _ => linha.ReivindicadoEm,
                }))
            .OrderByDescending(reivindicacao => reivindicacao.Desde)];
    }
}
