using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Premios;

// A contestação de um prêmio de participação (produto.md 4.7, fluxos/04-contestacao.md):
// questionamento com motivo, dentro da janela de 15 dias, uma por prêmio. Contestam o
// professor da turma (inclusive quem aprovou) ou a instituição; decide sempre a instituição,
// confirmando ou revogando. Nasce de PremioParticipacao.Contestar.
public sealed class Contestacao
{
    // Para o EF Core materializar do banco.
    private Contestacao() => Motivo = null!;

    internal Contestacao(PremioParticipacao premio, Usuario quem, string motivo, DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        PremioParticipacaoId = premio.Id;
        ContestadaPorId = quem.Id;
        ContestadaEm = agora;
        Motivo = motivo;
    }

    public Guid Id { get; private set; }

    public Guid PremioParticipacaoId { get; private set; }

    public Guid ContestadaPorId { get; private set; }

    public DateTimeOffset ContestadaEm { get; private set; }

    public string Motivo { get; private set; }

    // A decisão da instituição: quem, quando e qual. O motivo da revogação fica no prêmio.
    public Guid? DecididaPorId { get; private set; }

    public DateTimeOffset? DecididaEm { get; private set; }

    public DecisaoDaContestacao? Decisao { get; private set; }

    public bool EstaAberta => Decisao is null;

    // A fila de contestações marca o que passou de 7 dias, sem decidir nada (produto.md 12.6).
    public static bool PassouDoPrazo(DateTimeOffset contestadaEm, DateTimeOffset agora) => agora - contestadaEm > Comprovacao.PrazoDaMarca;

    internal void Decidir(Usuario instituicao, DecisaoDaContestacao decisao, DateTimeOffset agora)
    {
        if (!EstaAberta)
        {
            throw new DomainException($"A contestação {Id} já foi decidida.");
        }

        ExigirInstituicao(instituicao);
        DecididaPorId = instituicao.Id;
        DecididaEm = agora;
        Decisao = decisao;
    }

    internal static void ExigirInstituicao(Usuario quem)
    {
        if (!quem.Papeis.HasFlag(Papeis.Instituicao))
        {
            throw new DomainException($"Só a instituição decide contestação e revoga prêmio; {quem.Id} é {quem.Papeis}.");
        }
    }
}

public enum DecisaoDaContestacao
{
    Confirmada,
    Revogada,
}

public enum RecusaDaContestacao
{
    // Ainda não aprovado, ou reprovado: só o prêmio válido é contestável.
    NaoContestavel,

    // Uma contestação por prêmio.
    JaContestado,

    // A janela de 15 dias fechou, ou o prêmio já é definitivo ou revogado.
    ForaDaJanela,
    MotivoInvalido,
}

public sealed record ResultadoDaContestacao(Contestacao? Contestacao, RecusaDaContestacao? Recusa);
