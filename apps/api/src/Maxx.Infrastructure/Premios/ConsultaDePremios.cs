using Maxx.Application.Premios;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Conquistas;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.RaridadeDosItens;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Premios;

internal sealed class ConsultaDePremios(MaxxDbContext db, TimeProvider relogio) : IConsultaDePremios
{
    // A reivindicação que deduplica por matéria (P17) pede a matéria no app.
    private static readonly string _tipoPremioParticipacao = NomeJson.De(TipoDeItem.PremioParticipacao);
    private static readonly string _materia = NomeJson.De(CampoDeDeduplicacao.Materia);
    private static readonly string _tipoConquista = NomeJson.De(TipoDeItem.Conquista);

    public async Task<PremiosDoAluno?> DoAlunoAsync(Guid alunoId, CancellationToken cancellationToken)
    {
        var aluno = await (
                from usuario in db.Set<Usuario>()
                join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id
                where usuario.Id == alunoId && usuario.Papeis == Papeis.Aluno
                select new { curso.Sigla, CadastroId = cadastro.Id })
            .SingleOrDefaultAsync(cancellationToken);
        if (aluno is null)
        {
            return null;
        }

        var medalhas = await (
                from medalha in db.Set<Medalha>()
                join materia in db.Set<Materia>() on medalha.MateriaId equals materia.Id
                join bimestre in db.Set<Bimestre>() on medalha.BimestreId equals bimestre.Id
                where medalha.AlunoId == alunoId && medalha.AnuladaEm == null
                orderby medalha.ConcedidaEm descending
                select new
                {
                    medalha.Id,
                    medalha.ItemCodigo,
                    medalha.Tier,
                    medalha.Pontos,
                    Materia = materia.Nome,
                    bimestre.Numero,
                    medalha.ConcedidaEm,
                    PostId = db.Set<Post>().Where(post => post.MedalhaId == medalha.Id && post.RemovidoEm == null).Select(post => (Guid?)post.Id).FirstOrDefault(),
                })
            .ToListAsync(cancellationToken);
        // O prêmio de participação conta como obtido quando pontua: válido ou definitivo
        // (produto.md 4.7). Obtido na aprovação; sem bimestre, e com matéria só na sala.
        var participacoes = await (
                from premio in db.Set<PremioParticipacao>()
                join materia in db.Set<Materia>() on premio.MateriaId equals materia.Id into materias
                from materia in materias.DefaultIfEmpty()
                where premio.AlunoId == alunoId
                    && (premio.Estado == EstadoDoPremioParticipacao.Valido || premio.Estado == EstadoDoPremioParticipacao.Definitivo)
                orderby premio.AprovadoEm descending
                select new
                {
                    premio.Id,
                    premio.ItemCodigo,
                    premio.Tier,
                    premio.Pontos,
                    Materia = materia == null ? null : materia.Nome,
                    premio.AprovadoEm,
                    PostId = db.Set<Post>().Where(post => post.PremioParticipacaoId == premio.Id && post.RemovidoEm == null).Select(post => (Guid?)post.Id).FirstOrDefault(),
                })
            .ToListAsync(cancellationToken);

        // A conquista não removida pontua e conta como obtida (produto.md 4.9: tudo soma), com a
        // instância do período em que foi obtida; repetível, aparece uma vez por instância.
        var calendario = new Calendario(await db.Set<AnoLetivo>().AsNoTracking()
            .Include(ano => ano.Semestres)
            .ThenInclude(semestre => semestre.Bimestres)
            .ToListAsync(cancellationToken));
        var conquistas = await db.Set<Conquista>().AsNoTracking()
            .Where(conquista => conquista.AlunoId == alunoId && conquista.RemovidaEm == null)
            .OrderByDescending(conquista => conquista.ConcedidaEm)
            .ToListAsync(cancellationToken);

        var obtidos = medalhas
            .Select(medalha => (medalha.ItemCodigo, Obtido: new PremioObtido(
                medalha.Id, medalha.Tier, medalha.Pontos, medalha.Materia, medalha.Numero, medalha.ConcedidaEm, medalha.PostId)))
            .Concat(participacoes.Select(premio => (premio.ItemCodigo, Obtido: new PremioObtido(
                premio.Id, premio.Tier, premio.Pontos, premio.Materia, null, premio.AprovadoEm!.Value, premio.PostId))))
            .Concat(conquistas.Select(conquista => (conquista.ItemCodigo, Obtido: new PremioObtido(
                conquista.Id, conquista.Tier, conquista.Pontos, null, null, conquista.ConcedidaEm, null, Obtida(conquista.Instancia, calendario)))))
            .ToLookup(par => par.ItemCodigo, par => par.Obtido);

        var raridades = await db.Set<RaridadeDoItemRow>().AsNoTracking()
            .ToDictionaryAsync(linha => linha.ItemCodigo, cancellationToken);
        var itens = await db.Set<ItemCatalogoRow>().AsNoTracking().OrderBy(item => item.Codigo).ToListAsync(cancellationToken);
        var familias = await db.Set<FamiliaRow>().AsNoTracking().OrderBy(familia => familia.Codigo).ToListAsync(cancellationToken);
        var progressos = await ProgressosAsync(
            alunoId, aluno.CadastroId, itens.Where(item => item.Tipo == _tipoConquista), conquistas, calendario, cancellationToken);

        return new PremiosDoAluno(
            medalhas.Sum(medalha => medalha.Pontos) + participacoes.Sum(premio => premio.Pontos) + conquistas.Sum(conquista => conquista.Pontos),
            [.. familias.Select(familia => new FamiliaDoCatalogo(familia.Codigo, familia.Nome, familia.Descricao, familia.Passos))],
            [.. itens
                .Where(item => item.RestricaoCurso is null
                    || string.Equals(item.RestricaoCurso, aluno.Sigla, StringComparison.OrdinalIgnoreCase)
                    || obtidos.Contains(item.Codigo))
                .Select(item => new ItemDoAluno(
                    item.Codigo,
                    Ler<TipoDeItem>(item.Tipo),
                    item.Nome,
                    item.Descricao,
                    Ler<Tier>(item.Tier),
                    item.Pontos,
                    item.FamiliaCodigo,
                    item.OrdemNaFamilia,
                    raridades.TryGetValue(item.Codigo, out var raridade)
                        ? new RaridadeAtual(Ler<Raridade>(raridade.Raridade), raridade.AlunosComItem, raridade.AlunosElegiveis)
                        : null,
                    [.. obtidos[item.Codigo]],
                    item.Tipo == _tipoPremioParticipacao && (item.ChaveDeDeduplicacao ?? []).Contains(_materia),
                    progressos.GetValueOrDefault(item.Codigo)))]);
    }

    // O progresso das conquistas na instância vigente de cada período (D5 do passo 9): o
    // worker grava a contagem; a leitura só lê. Sem linha gravada, nenhum fato chegou ainda:
    // zero, e o alvo do catálogo ou o número de matérias do período. Fora fica a conquista já
    // obtida na instância vigente, a que não tem instância vigente (bimestre e semestre no
    // recesso), a fora da vigência e a de alvo zero (sem matrícula no período).
    private async Task<Dictionary<string, ProgressoDaConquista>> ProgressosAsync(
        Guid alunoId,
        Guid cadastroId,
        IEnumerable<ItemCatalogoRow> conquistasDoCatalogo,
        IReadOnlyList<Conquista> obtidas,
        Calendario calendario,
        CancellationToken cancellationToken)
    {
        var agora = relogio.GetUtcNow();
        var hoje = FusoHorario.DataLocal(agora);
        var bimestre = calendario.BimestreEm(agora);
        var semestre = calendario.SemestreEm(agora);
        var ano = calendario.Anos.FirstOrDefault(candidato => candidato.Inicio <= hoje && hoje <= candidato.Fim);
        InstanciaDePeriodo? Vigente(Periodo periodo) => periodo switch
        {
            Periodo.Bimestre when bimestre is not null => new(periodo, bimestre.Id),
            Periodo.Semestre when semestre is not null => new(periodo, semestre.Id),
            Periodo.Ano when ano is not null => new(periodo, ano.Id),
            Periodo.Curso => new(periodo, alunoId),
            _ => null,
        };

        var gravados = (await db.Set<ProgressoDeConquistaRow>().AsNoTracking()
                .Where(linha => linha.AlunoId == alunoId)
                .ToListAsync(cancellationToken))
            .ToDictionary(linha => (linha.ItemCodigo, linha.TipoDePeriodo, linha.ReferenciaDoPeriodo));

        var progressos = new Dictionary<string, ProgressoDaConquista>(StringComparer.Ordinal);
        foreach (var item in conquistasDoCatalogo.Select(CatalogoRowMapper.ParaConquista))
        {
            if (Vigente(item.Periodo) is not { } instancia
                || obtidas.Any(obtida => obtida.ItemCodigo == item.Codigo && obtida.Instancia == instancia)
                || item.Vigencia is { Inicio: { } inicio } && hoje < inicio
                || item.Vigencia is { Fim: { } fim } && hoje > fim
                || item.Regra.Alvo is not { } alvoDoCatalogo)
            {
                continue;
            }

            var (contagem, alvo) = gravados.TryGetValue((item.Codigo, NomeJson.De(instancia.Tipo), instancia.Referencia), out var linha)
                ? (linha.Contagem, linha.Alvo)
                : (0, alvoDoCatalogo.Fixo ?? await MateriasDoPeriodoAsync(cadastroId, instancia.Semestres(calendario), cancellationToken));
            if (alvo > 0)
            {
                progressos[item.Codigo] = new ProgressoDaConquista(item.Periodo, contagem, alvo, alvoDoCatalogo.EhTodasAsMatriculasDoPeriodo);
            }
        }

        return progressos;
    }

    private async Task<int> MateriasDoPeriodoAsync(
        Guid cadastroId, IReadOnlyList<SemestreLetivo> semestres, CancellationToken cancellationToken)
    {
        var ids = semestres.Select(semestre => semestre.Id).ToList();
        return await (
                from matricula in db.Set<Matricula>()
                join turma in db.Set<Turma>() on matricula.TurmaId equals turma.Id
                where matricula.AlunoId == cadastroId && ids.Contains(turma.SemestreLetivoId)
                select turma.MateriaId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    // A instância em números, para o app escrever ("3º bimestre de 2026", "2026/2", "2026").
    private static InstanciaObtida Obtida(InstanciaDePeriodo instancia, Calendario calendario)
    {
        var referencia = instancia.Referencia;
        switch (instancia.Tipo)
        {
            case Periodo.Bimestre:
                foreach (var semestre in calendario.Anos.SelectMany(ano => ano.Semestres))
                {
                    if (semestre.Bimestres.FirstOrDefault(bimestre => bimestre.Id == referencia) is { } bimestre)
                    {
                        return new(Periodo.Bimestre, semestre.Ano, semestre.Numero, bimestre.Numero);
                    }
                }

                break;
            case Periodo.Semestre:
                if (calendario.Anos.SelectMany(ano => ano.Semestres).FirstOrDefault(semestre => semestre.Id == referencia) is { } doSemestre)
                {
                    return new(Periodo.Semestre, doSemestre.Ano, doSemestre.Numero, null);
                }

                break;
            case Periodo.Ano:
                if (calendario.Anos.FirstOrDefault(ano => ano.Id == referencia) is { } doAno)
                {
                    return new(Periodo.Ano, doAno.Ano, null, null);
                }

                break;
        }

        return new(instancia.Tipo, null, null, null);
    }

    private static T Ler<T>(string texto)
        where T : struct, Enum =>
        NomeJson.TentarLer<T>(texto, out var valor) ? valor : throw new InvalidOperationException($"{typeof(T).Name} desconhecido: {texto}.");
}
