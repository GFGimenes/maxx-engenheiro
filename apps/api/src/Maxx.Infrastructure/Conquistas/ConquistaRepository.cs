using Maxx.Application.Conquistas;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Conquistas;

// Uma linha de progresso_de_conquista: a contagem e o alvo de uma conquista numa instância,
// da última avaliação do worker.
public sealed class ProgressoDeConquistaRow
{
    public Guid AlunoId { get; set; }

    public required string ItemCodigo { get; set; }

    public required string TipoDePeriodo { get; set; }

    public Guid ReferenciaDoPeriodo { get; set; }

    public int Contagem { get; set; }

    public int Alvo { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }
}

internal sealed class ConquistaRepository(MaxxDbContext db) : IConquistaRepository
{
    // Primeira chave da trava consultiva por aluno: separa esta trava de qualquer outra que
    // use a mesma função no banco.
    private const int _classeDaTrava = 9;

    private static readonly string _tipoConquista = NomeJson.De(TipoDeItem.Conquista);
    private static readonly string _tipoPremioParticipacao = NomeJson.De(TipoDeItem.PremioParticipacao);

    // Só vale dentro de uma transação, e o worker roda cada trabalho dentro de uma: a trava
    // sai no commit ou no rollback do trabalho.
    public async Task TravarAlunoAsync(Guid alunoId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A trava de conquistas do aluno só existe dentro de uma transação.");
        }

        var chave = alunoId.ToString();
        await db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({_classeDaTrava}, hashtext({chave}))", cancellationToken);
    }

    public async Task<AlunoDasConquistas?> AlunoAsync(Guid alunoId, CancellationToken cancellationToken) =>
        await (
                from usuario in db.Set<Usuario>()
                join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id
                where usuario.Id == alunoId && usuario.Papeis == Papeis.Aluno
                select new AlunoDasConquistas(usuario.Id, curso.Sigla))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ConquistaDoCatalogo>> CatalogoAsync(CancellationToken cancellationToken)
    {
        var linhas = await db.Set<ItemCatalogoRow>().AsNoTracking()
            .Where(item => item.Tipo == _tipoConquista)
            .OrderBy(item => item.Codigo)
            .ToListAsync(cancellationToken);
        return [.. linhas.Select(linha => new ConquistaDoCatalogo(CatalogoRowMapper.ParaConquista(linha), linha.Versao))];
    }

    public async Task<Calendario> CalendarioAsync(CancellationToken cancellationToken) =>
        new(await db.Set<AnoLetivo>().AsNoTracking()
            .Include(ano => ano.Semestres)
            .ThenInclude(semestre => semestre.Bimestres)
            .ToListAsync(cancellationToken));

    // As duas fontes passam pelas portas do domínio (FatoContavel.DaMedalha e DoPremio): é lá
    // que mora o que conta.
    public async Task<IReadOnlyList<FatoContavel>> FatosAsync(
        Guid alunoId, Calendario calendario, CancellationToken cancellationToken)
    {
        var medalhas = await db.Set<Medalha>().AsNoTracking()
            .Where(medalha => medalha.AlunoId == alunoId && medalha.AnuladaEm == null)
            .ToListAsync(cancellationToken);

        var premios = await (
                from premio in db.Set<PremioParticipacao>()
                join evento in db.Set<Evento>() on premio.EventoId equals evento.Id
                join item in db.Set<ItemCatalogoRow>() on premio.ItemCodigo equals item.Codigo
                where premio.AlunoId == alunoId
                    && premio.Estado == EstadoDoPremioParticipacao.Definitivo
                    && item.Tipo == _tipoPremioParticipacao
                select new { premio, evento, item })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return
        [
            .. medalhas.Select(medalha => FatoContavel.DaMedalha(medalha, calendario)).OfType<FatoContavel>(),
            .. premios
                .Select(linha => FatoContavel.DoPremio(
                    linha.premio, linha.evento, CatalogoRowMapper.ParaPremioParticipacao(linha.item), calendario))
                .OfType<FatoContavel>(),
        ];
    }

    public async Task<IReadOnlySet<Guid>> MateriasDoPeriodoAsync(
        Guid alunoId, IReadOnlyList<SemestreLetivo> semestres, CancellationToken cancellationToken)
    {
        var ids = semestres.Select(semestre => semestre.Id).ToList();
        var materias = await (
                from usuario in db.Set<Usuario>()
                join matricula in db.Set<Matricula>() on usuario.CadastroElegivelId equals matricula.AlunoId
                join turma in db.Set<Turma>() on matricula.TurmaId equals turma.Id
                where usuario.Id == alunoId && ids.Contains(turma.SemestreLetivoId)
                select turma.MateriaId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return materias.ToHashSet();
    }

    public async Task<IReadOnlyList<Conquista>> AtivasAsync(Guid alunoId, CancellationToken cancellationToken) =>
        await db.Set<Conquista>()
            .Include(conquista => conquista.Evidencias)
            .Where(conquista => conquista.AlunoId == alunoId && conquista.RemovidaEm == null)
            .ToListAsync(cancellationToken);

    public void Adicionar(Conquista conquista) => db.Add(conquista);

    public async Task GravarProgressoAsync(
        Guid alunoId,
        string itemCodigo,
        InstanciaDePeriodo instancia,
        Avaliacao avaliacao,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instancia);
        ArgumentNullException.ThrowIfNull(avaliacao);

        var tipo = NomeJson.De(instancia.Tipo);
        var linha = await db.Set<ProgressoDeConquistaRow>().SingleOrDefaultAsync(
            progresso => progresso.AlunoId == alunoId
                && progresso.ItemCodigo == itemCodigo
                && progresso.TipoDePeriodo == tipo
                && progresso.ReferenciaDoPeriodo == instancia.Referencia,
            cancellationToken);
        if (linha is null)
        {
            linha = new ProgressoDeConquistaRow
            {
                AlunoId = alunoId,
                ItemCodigo = itemCodigo,
                TipoDePeriodo = tipo,
                ReferenciaDoPeriodo = instancia.Referencia,
            };
            db.Add(linha);
        }

        linha.Contagem = avaliacao.Contagem;
        linha.Alvo = avaliacao.Alvo;
        linha.AtualizadoEm = agora;
    }
}
