using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Domain.Notificacoes;

namespace Maxx.Application.Notificacoes;

public interface INotificacaoRepository
{
    void Adicionar(Notificacao notificacao);

    Task<bool> ExisteAsync(Guid usuarioId, TipoDeNotificacao tipo, Guid referenciaId, CancellationToken cancellationToken);
}

// No worker: grava a notificação de medalha recebida (produto.md 8). Uma por medalha, mesmo
// que o trabalho rode de novo; nenhuma para medalha que já foi anulada ou apagada pela purga.
public sealed class NotificarMedalhaRecebidaHandler(
    IMedalhaRepository medalhas,
    INotificacaoRepository notificacoes,
    IUnitOfWork unitOfWork,
    TimeProvider relogio) : ITrabalhoHandler<NotificarMedalhaRecebida>
{
    public async Task ExecutarAsync(NotificarMedalhaRecebida carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);

        var medalha = await medalhas.BuscarAsync(carga.MedalhaId, cancellationToken);
        if (medalha is null || !medalha.EstaAtiva
            || await notificacoes.ExisteAsync(medalha.AlunoId, TipoDeNotificacao.MedalhaRecebida, medalha.Id, cancellationToken))
        {
            return;
        }

        notificacoes.Adicionar(Notificacao.MedalhaRecebida(medalha, relogio.GetUtcNow()));
        await unitOfWork.SalvarAsync(cancellationToken);
    }
}
