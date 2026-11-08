using Maxx.Application.Autenticacao;
using Maxx.Application.Autorizacao;
using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Application.Notificacoes;
using Maxx.Application.Posts;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;

namespace Maxx.Application.PremiosParticipacao;

// Aprovação e reprovação da comprovação (fluxos/02-post-com-comprovacao.md). Decide o
// professor de uma turma do aluno no semestre vigente, conferido no banco, ou a instituição;
// vale quem chega primeiro, e o xmin do prêmio fecha a corrida. A decisão, quem decidiu e
// quando, e os trabalhos da fila gravam juntos.
public sealed class DecidirComprovacaoHandler(
    IUsuarioRepository usuarios,
    IEscopoDeTurma escopo,
    IPremioParticipacaoRepository premios,
    IPostRepository posts,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    // Aprovado, o prêmio pontua na hora: o recálculo de raridade e a notificação vão para a
    // fila, e o fim da janela de contestação fica agendado.
    public async Task AprovarAsync(Guid quemId, Guid comprovacaoId, CancellationToken cancellationToken)
    {
        var (quem, premio, aluno) = await CarregarAsync(quemId, comprovacaoId, cancellationToken);

        if (premio.Aprovar(aluno, quem, relogio.GetUtcNow()) is { } recusa)
        {
            throw Recusa(recusa, comprovacaoId);
        }

        fila.Enfileirar(new NotificarComprovacaoAprovada(premio.Id));
        fila.Enfileirar(new RecalcularRaridade());
        fila.Enfileirar(new TornarPremioDefinitivo(premio.Id), premio.FimDaJanela);
        await SalvarAsync(comprovacaoId, cancellationToken);
    }

    // Reprovado, a reivindicação se encerra: o post perde o vínculo e fica como post comum.
    // Reivindicado não pontuava, então nada muda no ranking nem na raridade.
    public async Task ReprovarAsync(Guid quemId, Guid comprovacaoId, string? motivo, CancellationToken cancellationToken)
    {
        var (quem, premio, _) = await CarregarAsync(quemId, comprovacaoId, cancellationToken);

        if (premio.Reprovar(quem, motivo, relogio.GetUtcNow()) is { } recusa)
        {
            throw Recusa(recusa, comprovacaoId);
        }

        foreach (var post in await posts.DoPremioParticipacaoAsync(premio.Id, cancellationToken))
        {
            // O post de reivindicação sempre tem foto: desvincular nunca o deixa vazio.
            post.DesvincularPremio();
        }

        fila.Enfileirar(new NotificarComprovacaoReprovada(premio.Id));
        await SalvarAsync(comprovacaoId, cancellationToken);
    }

    private async Task<(Usuario Quem, PremioParticipacao Premio, Usuario Aluno)> CarregarAsync(
        Guid quemId, Guid comprovacaoId, CancellationToken cancellationToken)
    {
        var quem = await usuarios.BuscarAsync(quemId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {quemId} não existe.");
        var comprovacao = await premios.ComprovacaoAsync(comprovacaoId, cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosDoPremioParticipacao.ComprovacaoNaoEncontrada, $"Comprovação {comprovacaoId} não existe.");
        var premio = await premios.BuscarAsync(comprovacao.PremioParticipacaoId, cancellationToken)
            ?? throw new InvalidOperationException($"A comprovação {comprovacaoId} não tem prêmio.");

        // A instituição decide qualquer comprovação; o professor, só a de aluno seu no
        // semestre vigente, conferido no banco (ADR 0006), nunca lido do token.
        if (!quem.Papeis.HasFlag(Papeis.Instituicao)
            && !await escopo.ProfessorEnsinaAlunoAsync(quemId, premio.AlunoId, cancellationToken))
        {
            throw new RecusaException(
                TipoDeRecusa.Proibido, CodigosDaMedalha.ForaDoEscopo, $"{quemId} não ensina o aluno da comprovação {comprovacaoId}.");
        }

        var aluno = await usuarios.BuscarAsync(premio.AlunoId, cancellationToken)
            ?? throw new InvalidOperationException($"O prêmio {premio.Id} não tem aluno.");
        return (quem, premio, aluno);
    }

    private async Task SalvarAsync(Guid comprovacaoId, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outro aprovador decidiu entre a leitura e a gravação: vale o dele.
            throw Recusa(RecusaDaDecisao.JaDecidida, comprovacaoId);
        }
    }

    private static RecusaException Recusa(RecusaDaDecisao recusa, Guid comprovacaoId) => recusa switch
    {
        RecusaDaDecisao.JaDecidida => new(
            TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.ComprovacaoJaDecidida, $"Comprovação {comprovacaoId} já decidida."),
        RecusaDaDecisao.ExAluno => new(
            TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.ExAluno, $"Comprovação {comprovacaoId} de ex-aluno."),
        RecusaDaDecisao.MotivoInvalido => new(
            TipoDeRecusa.Invalido, CodigosDoPremioParticipacao.MotivoInvalido, "Motivo vazio ou acima do limite."),
        _ => throw new ArgumentOutOfRangeException(nameof(recusa), recusa, null),
    };
}

// Os trabalhos que a decisão enfileira, na mesma transação.
public sealed record NotificarComprovacaoAprovada(Guid PremioId) : ICargaDeTrabalho
{
    public static string Tipo => "notificar-comprovacao-aprovada";
}

public sealed record NotificarComprovacaoReprovada(Guid PremioId) : ICargaDeTrabalho
{
    public static string Tipo => "notificar-comprovacao-reprovada";
}

// Agendado para o fim da janela de contestação (fluxos/04-contestacao.md).
public sealed record TornarPremioDefinitivo(Guid PremioId) : ICargaDeTrabalho
{
    public static string Tipo => "tornar-premio-definitivo";
}

// No worker: grava a notificação de comprovação decidida (produto.md 8). Uma por prêmio e
// tipo, mesmo que o trabalho rode de novo; nenhuma para o prêmio que a purga já levou.
public sealed class NotificarComprovacaoHandler(
    IPremioParticipacaoRepository premios,
    INotificacaoRepository notificacoes,
    IUnitOfWork unitOfWork,
    TimeProvider relogio) : ITrabalhoHandler<NotificarComprovacaoAprovada>, ITrabalhoHandler<NotificarComprovacaoReprovada>
{
    public Task ExecutarAsync(NotificarComprovacaoAprovada carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);
        return NotificarAsync(carga.PremioId, TipoDeNotificacao.ComprovacaoAprovada, Notificacao.ComprovacaoAprovada, cancellationToken);
    }

    public Task ExecutarAsync(NotificarComprovacaoReprovada carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);
        return NotificarAsync(carga.PremioId, TipoDeNotificacao.ComprovacaoReprovada, Notificacao.ComprovacaoReprovada, cancellationToken);
    }

    private async Task NotificarAsync(
        Guid premioId,
        TipoDeNotificacao tipo,
        Func<PremioParticipacao, DateTimeOffset, Notificacao> criar,
        CancellationToken cancellationToken)
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

// No worker, no fim da janela: o prêmio ainda válido fica definitivo (produto.md 4.7). O
// contestado, decidido ou apagado pela purga fica como está. Se o trabalho chegar antes do
// fim da janela, volta para a fila no instante certo, em vez de se perder.
public sealed class TornarPremioDefinitivoHandler(
    IPremioParticipacaoRepository premios,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio) : ITrabalhoHandler<TornarPremioDefinitivo>
{
    public async Task ExecutarAsync(TornarPremioDefinitivo carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);

        var premio = await premios.BuscarAsync(carga.PremioId, cancellationToken);
        if (premio is not { Estado: EstadoDoPremioParticipacao.Valido })
        {
            return;
        }

        if (!premio.TornarDefinitivoSeAJanelaFechou(relogio.GetUtcNow()))
        {
            fila.Enfileirar(carga, premio.FimDaJanela);
        }

        await unitOfWork.SalvarAsync(cancellationToken);
    }
}
