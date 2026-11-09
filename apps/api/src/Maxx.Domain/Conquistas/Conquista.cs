using Maxx.Domain.Catalogo;

namespace Maxx.Domain.Conquistas;

// A conquista concedida pelo sistema (produto.md 4.9): automática, por acúmulo dentro de uma
// instância do período. Congela tier, pontos e versão do item (ADR 0007). Não é contestável
// nem revogável: é recalculada. Se a anulação de uma medalha a deixa insatisfeita, ela é
// removida, e a remoção fica registrada; se o critério volta a ser satisfeito, nasce outra.
//
// Guarda as evidências: os prêmios que a compuseram. É o que responde ao aluno por que ele
// ganhou, e o que torna a anulação recalculável (regras-conquista.md 6).
public sealed class Conquista
{
    private readonly List<EvidenciaDaConquista> _evidencias;

    // Para o EF Core materializar do banco.
    private Conquista()
    {
        ItemCodigo = null!;
        _evidencias = [];
    }

    private Conquista(
        ItemConquista item,
        int versaoDoItem,
        Guid alunoId,
        InstanciaDePeriodo instancia,
        IEnumerable<FatoContavel> evidencias,
        DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        AlunoId = alunoId;
        ItemCodigo = item.Codigo;
        VersaoDoItem = versaoDoItem;
        Tier = item.Tier;
        Pontos = item.Pontos;
        TipoDePeriodo = instancia.Tipo;
        ReferenciaDoPeriodo = instancia.Referencia;
        ConcedidaEm = agora;
        _evidencias = [.. evidencias.Select(fato => new EvidenciaDaConquista(Id, fato))];
    }

    public Guid Id { get; private set; }

    public Guid AlunoId { get; private set; }

    public string ItemCodigo { get; private set; }

    public int VersaoDoItem { get; private set; }

    public Tier Tier { get; private set; }

    public int Pontos { get; private set; }

    // A instância do período: com o aluno e o item, única entre as conquistas não removidas.
    public Periodo TipoDePeriodo { get; private set; }

    public Guid ReferenciaDoPeriodo { get; private set; }

    public DateTimeOffset ConcedidaEm { get; private set; }

    // Quando o recálculo a removeu. Quem remove é sempre o sistema.
    public DateTimeOffset? RemovidaEm { get; private set; }

    public bool EstaAtiva => RemovidaEm is null;

    public InstanciaDePeriodo Instancia => new(TipoDePeriodo, ReferenciaDoPeriodo);

    public IReadOnlyList<EvidenciaDaConquista> Evidencias => _evidencias;

    // avaliacao: a da instância, feita pelo avaliador. Conceder fora do critério é erro de
    // quem chama, não recusa.
    public static Conquista Conceder(
        ItemConquista item,
        int versaoDoItem,
        Guid alunoId,
        InstanciaDePeriodo instancia,
        Avaliacao avaliacao,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(instancia);
        ArgumentNullException.ThrowIfNull(avaliacao);

        if (instancia.Tipo != item.Periodo)
        {
            throw new DomainException($"A conquista {item.Codigo} é de {item.Periodo}, não de {instancia.Tipo}.");
        }

        if (!avaliacao.Satisfeita)
        {
            throw new DomainException(
                $"A conquista {item.Codigo} não está satisfeita: {avaliacao.Contagem} de {avaliacao.Alvo}.");
        }

        return new Conquista(item, versaoDoItem, alunoId, instancia, avaliacao.Evidencias, agora);
    }

    // Recálculo depois da anulação: ainda satisfeita, as evidências passam a ser o conjunto
    // atual, para a próxima anulação achar a conquista pela medalha certa; insatisfeita, a
    // conquista é removida e os pontos saem. Devolve se a conquista foi removida agora.
    public bool Recalcular(Avaliacao avaliacao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);

        if (!EstaAtiva)
        {
            return false;
        }

        if (!avaliacao.Satisfeita)
        {
            RemovidaEm = agora;
            return true;
        }

        var atuais = avaliacao.Evidencias.Select(fato => (fato.Origem, fato.Id)).ToHashSet();
        _evidencias.RemoveAll(evidencia => !atuais.Contains((evidencia.Origem, evidencia.FatoId)));
        var guardadas = _evidencias.Select(evidencia => (evidencia.Origem, evidencia.FatoId)).ToHashSet();
        _evidencias.AddRange(avaliacao.Evidencias
            .Where(fato => !guardadas.Contains((fato.Origem, fato.Id)))
            .Select(fato => new EvidenciaDaConquista(Id, fato)));
        return false;
    }
}

// Um prêmio que compôs a conquista: uma medalha ou um prêmio de participação, nunca os dois
// e nunca outra conquista.
public sealed class EvidenciaDaConquista
{
    // Para o EF Core materializar do banco.
    private EvidenciaDaConquista()
    {
    }

    internal EvidenciaDaConquista(Guid conquistaId, FatoContavel fato)
    {
        Id = Guid.CreateVersion7();
        ConquistaId = conquistaId;
        MedalhaId = fato.Origem == OrigemDoFato.Medalha ? fato.Id : null;
        PremioParticipacaoId = fato.Origem == OrigemDoFato.PremioParticipacao ? fato.Id : null;
    }

    public Guid Id { get; private set; }

    public Guid ConquistaId { get; private set; }

    public Guid? MedalhaId { get; private set; }

    public Guid? PremioParticipacaoId { get; private set; }

    public OrigemDoFato Origem => MedalhaId is not null ? OrigemDoFato.Medalha : OrigemDoFato.PremioParticipacao;

    public Guid FatoId => MedalhaId ?? PremioParticipacaoId!.Value;
}
