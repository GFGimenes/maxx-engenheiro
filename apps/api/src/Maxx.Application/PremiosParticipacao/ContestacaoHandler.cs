using Maxx.Application.Autenticacao;
using Maxx.Application.Autorizacao;
using Maxx.Application.Conquistas;
using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Application.Notificacoes;
using Maxx.Application.Posts;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;

namespace Maxx.Application.PremiosParticipacao;

// Contestação e decisão da instituição (fluxos/04-contestacao.md). Contestam o professor da
// turma, conferido no banco, ou a instituição; decide sempre a instituição. Cada ato grava
// quem e quando, e enfileira na mesma transação a notificação e o recálculo de raridade,
// porque todos mudam o que pontua.
public sealed class ContestacaoHandler(
    IUsuarioRepository usuarios,
    IEscopoDeTurma escopo,
    IPremioParticipacaoRepository premios,
    IPostRepository posts,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<Contestacao> ContestarAsync(Guid quemId, Guid premioId, string? motivo, CancellationToken cancellationToken)
    {
        var quem = await QuemAsync(quemId, cancellationToken);
        var premio = await premios.BuscarAsync(premioId, cancellationToken)
            ?? throw PremioNaoEncontrado(premioId);

        if (!quem.Papeis.HasFlag(Papeis.Instituicao)
            && !await escopo.ProfessorEnsinaAlunoAsync(quemId, premio.AlunoId, cancellationToken))
        {
            throw new RecusaException(TipoDeRecusa.Proibido, CodigosDaMedalha.ForaDoEscopo, $"{quemId} não ensina o aluno do prêmio {premioId}.");
        }

        var resultado = premio.Contestar(quem, motivo, relogio.GetUtcNow());
        if (resultado.Contestacao is not { } contestacao)
        {
            throw resultado.Recusa switch
            {
                RecusaDaContestacao.NaoContestavel => new RecusaException(
                    TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.PremioNaoContestavel, $"Prêmio {premioId} não aprovado."),
                RecusaDaContestacao.JaContestado => JaContestado(premioId),
                RecusaDaContestacao.ForaDaJanela => new RecusaException(
                    TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.ForaDaJanelaDeContestacao, $"Janela do prêmio {premioId} fechada."),
                RecusaDaContestacao.MotivoInvalido => MotivoInvalido(),
                _ => new InvalidOperationException($"Recusa sem tratamento: {resultado.Recusa}."),
            };
        }

        premios.Adicionar(contestacao);
        fila.Enfileirar(new NotificarContestacaoAberta(premio.Id));
        fila.Enfileirar(new RecalcularRaridade());
        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeUnicidadeException conflito) when (conflito.Unicidade == Unicidade.ContestacaoPorPremio)
        {
            throw JaContestado(premioId);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outro ato mudou o prêmio entre a leitura e a gravação: outra contestação, ou a
            // instituição removendo o post.
            throw JaContestado(premioId);
        }

        return contestacao;
    }

    // Confirmado: definitivo na hora, volta a pontuar, janela encerrada.
    public async Task ConfirmarAsync(Guid quemId, Guid contestacaoId, CancellationToken cancellationToken)
    {
        var (quem, contestacao, premio) = await CarregarAsync(quemId, contestacaoId, cancellationToken);

        if (premio.Confirmar(contestacao, quem, relogio.GetUtcNow()) is { } recusa)
        {
            throw Recusa(recusa, contestacaoId);
        }

        fila.Enfileirar(new NotificarDecisaoDeContestacao(premio.Id));
        fila.Enfileirar(ReavaliarConquistas.PeloPremio(premio.AlunoId, premio.Id));
        fila.Enfileirar(new RecalcularRaridade());
        await SalvarDecisaoAsync(contestacaoId, cancellationToken);
    }

    // Revogado, com motivo: sai da pontuação, fica no histórico, e o post perde o vínculo.
    public async Task RevogarAsync(Guid quemId, Guid contestacaoId, string? motivo, CancellationToken cancellationToken)
    {
        var (quem, contestacao, premio) = await CarregarAsync(quemId, contestacaoId, cancellationToken);

        if (premio.Revogar(quem, motivo, contestacao, relogio.GetUtcNow()) is { } recusa)
        {
            throw Recusa(recusa, contestacaoId);
        }

        await DesvincularPostsAsync(posts, premio, cancellationToken);
        fila.Enfileirar(new NotificarDecisaoDeContestacao(premio.Id));
        fila.Enfileirar(new RecalcularRaridade());
        await SalvarDecisaoAsync(contestacaoId, cancellationToken);
    }

    // O post do prêmio revogado ou reprovado fica como post comum; se era só o prêmio, sem
    // foto e sem legenda, sai, porque post vazio não existe (produto.md 4.7).
    internal static async Task DesvincularPostsAsync(IPostRepository posts, PremioParticipacao premio, CancellationToken cancellationToken)
    {
        foreach (var post in await posts.DoPremioParticipacaoAsync(premio.Id, cancellationToken))
        {
            if (post.FicariaVazioSemPremio)
            {
                posts.Apagar(post);
            }
            else
            {
                post.DesvincularPremio();
            }
        }
    }

    private async Task<(Usuario Quem, Contestacao Contestacao, PremioParticipacao Premio)> CarregarAsync(
        Guid quemId, Guid contestacaoId, CancellationToken cancellationToken)
    {
        var quem = await QuemAsync(quemId, cancellationToken);
        if (!quem.Papeis.HasFlag(Papeis.Instituicao))
        {
            throw new RecusaException(TipoDeRecusa.Proibido, CodigosDaMedalha.ForaDoEscopo, $"{quemId} não decide contestação.");
        }

        var contestacao = await premios.ContestacaoAsync(contestacaoId, cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosDoPremioParticipacao.ContestacaoNaoEncontrada, $"Contestação {contestacaoId} não existe.");
        var premio = await premios.BuscarAsync(contestacao.PremioParticipacaoId, cancellationToken)
            ?? throw new InvalidOperationException($"A contestação {contestacaoId} não tem prêmio.");
        return (quem, contestacao, premio);
    }

    private async Task<Usuario> QuemAsync(Guid quemId, CancellationToken cancellationToken) =>
        await usuarios.BuscarAsync(quemId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {quemId} não existe.");

    private async Task SalvarDecisaoAsync(Guid contestacaoId, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outra decisão gravou primeiro.
            throw Recusa(RecusaDaDecisao.JaDecidida, contestacaoId);
        }
    }

    private static RecusaException Recusa(RecusaDaDecisao recusa, Guid contestacaoId) => recusa switch
    {
        RecusaDaDecisao.JaDecidida => new(
            TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.ContestacaoJaDecidida, $"Contestação {contestacaoId} já decidida."),
        RecusaDaDecisao.MotivoInvalido => MotivoInvalido(),
        _ => throw new ArgumentOutOfRangeException(nameof(recusa), recusa, null),
    };

    private static RecusaException PremioNaoEncontrado(Guid premioId) =>
        new(TipoDeRecusa.NaoEncontrado, CodigosDoPremioParticipacao.PremioNaoEncontrado, $"Prêmio {premioId} não existe.");

    private static RecusaException JaContestado(Guid premioId) =>
        new(TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.PremioJaContestado, $"Prêmio {premioId} já contestado.");

    private static RecusaException MotivoInvalido() =>
        new(TipoDeRecusa.Invalido, CodigosDoPremioParticipacao.MotivoInvalido, "Motivo vazio ou acima do limite.");
}

// A remoção do post pela instituição resolve o prêmio no mesmo ato (decisão do passo 8): a
// instituição não pode depender da fila para tirar do ar uma foto imprópria. Pendente,
// reprova; válido, revoga; em averiguação, decide a contestação revogando. Sempre com o
// motivo de Motivo.RemovidoPelaInstituicao, e depois o post sai.
public static class RemocaoPelaInstituicao
{
    public static async Task ResolverAsync(
        PremioParticipacao premio,
        Usuario instituicao,
        IPremioParticipacaoRepository premios,
        IPostRepository posts,
        IFilaDeTrabalhos fila,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(premio);
        ArgumentNullException.ThrowIfNull(premios);
        ArgumentNullException.ThrowIfNull(fila);

        RecusaDaDecisao? recusa;
        switch (premio.Estado)
        {
            case EstadoDoPremioParticipacao.Reivindicado:
                recusa = premio.Reprovar(instituicao, Motivo.RemovidoPelaInstituicao, agora);
                fila.Enfileirar(new NotificarComprovacaoReprovada(premio.Id));
                break;
            case EstadoDoPremioParticipacao.Valido:
                recusa = premio.Revogar(instituicao, Motivo.RemovidoPelaInstituicao, null, agora);
                fila.Enfileirar(new NotificarPremioRevogado(premio.Id));
                fila.Enfileirar(new RecalcularRaridade());
                break;
            case EstadoDoPremioParticipacao.EmAveriguacao:
                var contestacao = await premios.ContestacaoDoPremioAsync(premio.Id, cancellationToken)
                    ?? throw new InvalidOperationException($"O prêmio {premio.Id} está em averiguação sem contestação.");
                recusa = premio.Revogar(instituicao, Motivo.RemovidoPelaInstituicao, contestacao, agora);
                fila.Enfileirar(new NotificarDecisaoDeContestacao(premio.Id));
                fila.Enfileirar(new RecalcularRaridade());
                break;
            default:
                return;
        }

        if (recusa is not null)
        {
            throw new InvalidOperationException($"A instituição não resolveu o prêmio {premio.Id}: {recusa}.");
        }

        await ContestacaoHandler.DesvincularPostsAsync(posts, premio, cancellationToken);
    }
}

public sealed record NotificarContestacaoAberta(Guid PremioId) : ICargaDeTrabalho
{
    public static string Tipo => "notificar-contestacao-aberta";
}

public sealed record NotificarDecisaoDeContestacao(Guid PremioId) : ICargaDeTrabalho
{
    public static string Tipo => "notificar-decisao-de-contestacao";
}

public sealed record NotificarPremioRevogado(Guid PremioId) : ICargaDeTrabalho
{
    public static string Tipo => "notificar-premio-revogado";
}

// No worker: as notificações da contestação e da revogação (produto.md 8), uma por prêmio e
// tipo. A de decisão só sai se a contestação estiver decidida; as outras, se o prêmio ainda
// existir.
public sealed class NotificarContestacaoHandler(
    IPremioParticipacaoRepository premios,
    INotificacaoRepository notificacoes,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
    : ITrabalhoHandler<NotificarContestacaoAberta>, ITrabalhoHandler<NotificarDecisaoDeContestacao>, ITrabalhoHandler<NotificarPremioRevogado>
{
    public Task ExecutarAsync(NotificarContestacaoAberta carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);
        return NotificarAsync(carga.PremioId, TipoDeNotificacao.ContestacaoAberta, Notificacao.ContestacaoAberta, cancellationToken);
    }

    public Task ExecutarAsync(NotificarDecisaoDeContestacao carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);
        return NotificarAsync(carga.PremioId, TipoDeNotificacao.DecisaoDeContestacao, Notificacao.DecisaoDeContestacao, cancellationToken);
    }

    public Task ExecutarAsync(NotificarPremioRevogado carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);
        return NotificarAsync(carga.PremioId, TipoDeNotificacao.PremioRevogado, Notificacao.PremioRevogado, cancellationToken);
    }

    private async Task NotificarAsync(
        Guid premioId, TipoDeNotificacao tipo, Func<PremioParticipacao, DateTimeOffset, Notificacao> criar, CancellationToken cancellationToken)
    {
        var premio = await premios.BuscarAsync(premioId, cancellationToken);
        if (premio is null || await notificacoes.ExisteAsync(premio.AlunoId, tipo, premio.Id, cancellationToken))
        {
            return;
        }

        notificacoes.Adicionar(criar(premio, relogio.GetUtcNow()));
        await unitOfWork.SalvarAsync(cancellationToken);
    }
}
