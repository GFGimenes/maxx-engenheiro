using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Premios;

// Concessão humana do professor, sobre fato de nota ou presença (produto.md 4.8). Grava
// turma e matéria de quem concede e o bimestre da data; congela tier, pontos e versão do
// item (ADR 0007). Não é contestável nem revogável: a instituição pode anular, e a medalha
// anulada deixa de pontuar e libera a vaga no limite do período.
public sealed class Medalha
{
    // Para o EF Core materializar do banco.
    private Medalha()
    {
        ItemCodigo = null!;
        ChaveDeDeduplicacao = null!;
    }

    private Medalha(
        ItemMedalha item,
        int versaoDoItem,
        Usuario aluno,
        Turma turma,
        SemestreLetivo semestre,
        Bimestre bimestre,
        int ocorrencia,
        string chaveDeDeduplicacao,
        Usuario professor,
        DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        AlunoId = aluno.Id;
        ItemCodigo = item.Codigo;
        VersaoDoItem = versaoDoItem;
        Tier = item.Tier;
        Pontos = item.Pontos;
        TurmaId = turma.Id;
        MateriaId = turma.MateriaId;
        SemestreLetivoId = semestre.Id;
        BimestreId = bimestre.Id;
        Ocorrencia = ocorrencia;
        ChaveDeDeduplicacao = chaveDeDeduplicacao;
        ConcedidaPorId = professor.Id;
        ConcedidaEm = agora;
    }

    public Guid Id { get; private set; }

    // O usuário do aluno: só aluno com conta recebe medalha.
    public Guid AlunoId { get; private set; }

    public string ItemCodigo { get; private set; }

    public int VersaoDoItem { get; private set; }

    public Tier Tier { get; private set; }

    public int Pontos { get; private set; }

    public Guid TurmaId { get; private set; }

    public Guid MateriaId { get; private set; }

    public Guid SemestreLetivoId { get; private set; }

    // O bimestre da data da concessão, também nos itens de período semestre.
    public Guid BimestreId { get; private set; }

    // Qual das vagas do limite por período esta medalha ocupa, de 1 ao limite.
    public int Ocorrencia { get; private set; }

    // Os campos que o item declara em chaveDeDeduplicacao, com os valores desta concessão.
    // Única entre as medalhas ativas: é o que decide a corrida de duas concessões.
    public string ChaveDeDeduplicacao { get; private set; }

    public Guid ConcedidaPorId { get; private set; }

    public DateTimeOffset ConcedidaEm { get; private set; }

    public Guid? AnuladaPorId { get; private set; }

    public DateTimeOffset? AnuladaEm { get; private set; }

    public bool EstaAtiva => AnuladaEm is null;

    // existentes: as medalhas do aluno nesse item que quem chama carregou, ativas ou não;
    // as de outro período ou matéria são ignoradas aqui. siglaDoCurso é o curso atual do
    // aluno, para item restrito a curso.
    public static ResultadoDaConcessao Conceder(
        ItemMedalha item,
        int versaoDoItem,
        Usuario aluno,
        string siglaDoCurso,
        Turma turma,
        SemestreLetivo semestreDaTurma,
        Usuario professor,
        IEnumerable<Medalha> existentes,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(aluno);
        ArgumentNullException.ThrowIfNull(turma);
        ArgumentNullException.ThrowIfNull(semestreDaTurma);
        ArgumentNullException.ThrowIfNull(professor);
        ArgumentNullException.ThrowIfNull(existentes);

        if (semestreDaTurma.Id != turma.SemestreLetivoId)
        {
            throw new ArgumentException("O semestre não é o da turma.", nameof(semestreDaTurma));
        }

        var anteriores = existentes.ToList();
        if (anteriores.Any(medalha => medalha.AlunoId != aluno.Id || medalha.ItemCodigo != item.Codigo))
        {
            throw new ArgumentException("Há medalha de outro aluno ou de outro item.", nameof(existentes));
        }

        if (!professor.Papeis.HasFlag(Papeis.Professor))
        {
            throw new DomainException($"Só professor concede medalha; {professor.Id} é {professor.Papeis}.");
        }

        if (aluno.Papeis != Papeis.Aluno)
        {
            throw new DomainException($"Só aluno recebe medalha; {aluno.Id} é {aluno.Papeis}.");
        }

        if (aluno.EhExAluno)
        {
            return ResultadoDaConcessao.Recusada(RecusaDaConcessao.ExAluno);
        }

        var data = FusoHorario.DataLocal(agora);
        if (!semestreDaTurma.Contem(data))
        {
            return ResultadoDaConcessao.Recusada(RecusaDaConcessao.ForaDoSemestreDaTurma);
        }

        var bimestre = semestreDaTurma.Bimestres.FirstOrDefault(candidato => candidato.Contem(data));
        if (bimestre is null)
        {
            return ResultadoDaConcessao.Recusada(RecusaDaConcessao.ForaDeBimestre);
        }

        if (item.Vigencia is { Inicio: { } inicio } && data < inicio || item.Vigencia is { Fim: { } fim } && data > fim)
        {
            return ResultadoDaConcessao.Recusada(RecusaDaConcessao.ItemForaDeVigencia);
        }

        if (item.RestricaoCurso is { } restricao && !string.Equals(restricao, siglaDoCurso, StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoDaConcessao.Recusada(RecusaDaConcessao.ItemDeOutroCurso);
        }

        // As vagas do período: mesma matéria e mesma instância do período de referência.
        var periodo = item.PeriodoDeReferencia switch
        {
            Periodo.Bimestre => bimestre.Id,
            Periodo.Semestre => semestreDaTurma.Id,
            _ => throw new DomainException($"Medalha {item.Codigo} com período {item.PeriodoDeReferencia}: só bimestre ou semestre."),
        };
        var ocupadas = anteriores
            .Where(medalha => medalha.EstaAtiva && medalha.MateriaId == turma.MateriaId)
            .Where(medalha => (item.PeriodoDeReferencia == Periodo.Bimestre ? medalha.BimestreId : medalha.SemestreLetivoId) == periodo)
            .Select(medalha => medalha.Ocorrencia)
            .ToHashSet();
        var ocorrencia = Enumerable.Range(1, item.LimitePorPeriodo).FirstOrDefault(vaga => !ocupadas.Contains(vaga));
        if (ocorrencia == 0)
        {
            return ResultadoDaConcessao.Recusada(RecusaDaConcessao.LimiteAtingido);
        }

        var chave = Chave(item, aluno, turma, semestreDaTurma, bimestre, ocorrencia);
        return new ResultadoDaConcessao(
            new Medalha(item, versaoDoItem, aluno, turma, semestreDaTurma, bimestre, ocorrencia, chave, professor, agora),
            null);
    }

    // Ato administrativo da instituição: sem motivo público e sem averiguação. false: já
    // estava anulada, e nada muda.
    public bool Anular(Guid instituicaoId, DateTimeOffset agora)
    {
        if (!EstaAtiva)
        {
            return false;
        }

        AnuladaPorId = instituicaoId;
        AnuladaEm = agora;
        return true;
    }

    private static string Chave(
        ItemMedalha item, Usuario aluno, Turma turma, SemestreLetivo semestre, Bimestre bimestre, int ocorrencia) =>
        string.Join('|', item.ChaveDeDeduplicacao.Select(campo => campo switch
        {
            CampoDeDeduplicacao.Aluno => $"aluno:{aluno.Id}",
            CampoDeDeduplicacao.Item => $"item:{item.Codigo}",
            CampoDeDeduplicacao.Materia => $"materia:{turma.MateriaId}",
            CampoDeDeduplicacao.Bimestre => $"bimestre:{bimestre.Id}",
            CampoDeDeduplicacao.Semestre => $"semestre:{semestre.Id}",
            CampoDeDeduplicacao.Ocorrencia => $"ocorrencia:{ocorrencia}",
            _ => throw new DomainException($"Medalha {item.Codigo} não deduplica por {campo}."),
        }));
}

public enum RecusaDaConcessao
{
    ExAluno,
    ForaDoSemestreDaTurma,

    // Dentro do semestre, mas entre dois bimestres: a medalha não teria bimestre.
    ForaDeBimestre,
    ItemForaDeVigencia,
    ItemDeOutroCurso,
    LimiteAtingido,
}

public sealed record ResultadoDaConcessao(Medalha? Medalha, RecusaDaConcessao? Recusa)
{
    public static ResultadoDaConcessao Recusada(RecusaDaConcessao recusa) => new(null, recusa);
}
