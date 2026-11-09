using Maxx.Application.Autenticacao;
using Maxx.Application.Conquistas;
using Maxx.Application.Fila;
using Maxx.Application.Posts;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;

namespace Maxx.Application.Medalhas;

// Anulação de medalha pela instituição (produto.md 4.8): ato administrativo, sem motivo
// público e sem averiguação. Na fase 1, por comando administrativo, até a área web do
// passo 12. Grava quem anulou e quando e enfileira o recálculo das conquistas que a medalha
// compunha e os de ranking e raridade, na mesma transação. O post da
// medalha perde o vínculo e fica como post comum; o que era só a medalha é apagado, porque
// post vazio não existe (decisão do passo 7).
public sealed class AnularMedalhaHandler(
    IUsuarioRepository usuarios,
    IMedalhaRepository medalhas,
    IPostRepository posts,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    // false: a medalha já estava anulada, e nada mudou.
    public async Task<bool> AnularAsync(Guid medalhaId, string emailDaInstituicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(emailDaInstituicao);

        var instituicao = await usuarios.BuscarPorEmailAsync(emailDaInstituicao.Trim(), cancellationToken);
        if (instituicao is null || !instituicao.Papeis.HasFlag(Papeis.Instituicao))
        {
            throw new RecusaException(
                TipoDeRecusa.Proibido, CodigosDaMedalha.SemContaDeInstituicao, "O e-mail não é de uma conta da instituição.");
        }

        try
        {
            return await TentarAsync(medalhaId, instituicao.Id, cancellationToken);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outra anulação da mesma medalha gravou primeiro. Relida, ela já está anulada.
            unitOfWork.Descartar();
            return await TentarAsync(medalhaId, instituicao.Id, cancellationToken);
        }
    }

    private async Task<bool> TentarAsync(Guid medalhaId, Guid instituicaoId, CancellationToken cancellationToken)
    {
        var medalha = await medalhas.BuscarAsync(medalhaId, cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosDaMedalha.MedalhaNaoEncontrada, $"Medalha {medalhaId} não existe.");

        if (!medalha.Anular(instituicaoId, relogio.GetUtcNow()))
        {
            return false;
        }

        // O removido pela instituição fica como está: some para todos de qualquer jeito, e
        // apagá-lo perderia quem removeu e quando.
        foreach (var post in await posts.DoPremioAsync(medalha.Id, cancellationToken))
        {
            if (post.EstaRemovido)
            {
                continue;
            }

            if (post.FicariaVazioSemPremio)
            {
                posts.Apagar(post);
            }
            else
            {
                post.DesvincularPremio();
            }
        }

        fila.Enfileirar(new RecalcularConquistasDaMedalha(medalha.Id));
        fila.PedirRecalculos();
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
