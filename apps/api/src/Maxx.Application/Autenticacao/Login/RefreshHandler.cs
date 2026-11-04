using Maxx.Domain.Identidade;

namespace Maxx.Application.Autenticacao.Login;

// A renovação: o refresh apresentado é trocado por um novo da mesma família, com um access
// novo. Reuso de um já trocado derruba a família (ADR 0006).
public sealed class RefreshHandler(
    IFamiliaDeSessaoRepository familias,
    IUsuarioRepository usuarios,
    ITokenGenerator tokens,
    IAccessTokenIssuer accessTokens,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<Renovacao> RenovarAsync(string refresh, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refresh);

        try
        {
            return await TentarAsync(refresh, cancellationToken);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outro pedido rotacionou este mesmo refresh entre a leitura e a gravação. Relido,
            // ele já aparece rotacionado, e o reuso é tratado como se tivesse chegado depois:
            // simultâneo ou em sequência, o resultado é o mesmo (decisoes/README.md).
            unitOfWork.Descartar();
            return await TentarAsync(refresh, cancellationToken);
        }
    }

    private async Task<Renovacao> TentarAsync(string refresh, CancellationToken cancellationToken)
    {
        var hash = tokens.Hash(refresh);
        var familia = await familias.BuscarPorRefreshAsync(hash, cancellationToken);
        if (familia is null)
        {
            return Renovacao.Recusada(RecusaDaRenovacao.Desconhecido);
        }

        var novo = tokens.Generate();
        switch (familia.Rotacionar(hash, novo.Hash, relogio.GetUtcNow()))
        {
            case ResultadoDoRefresh.Rotacionado:
                var usuario = await usuarios.BuscarAsync(familia.UsuarioId, cancellationToken)
                    ?? throw new InvalidOperationException($"A família {familia.Id} cita um usuário que não existe.");
                await unitOfWork.SalvarAsync(cancellationToken);

                var emitido = familia.Tokens.Single(token => token.Hash == novo.Hash);
                return Renovacao.Concedida(new Sessao(accessTokens.Issue(usuario), novo.Valor, emitido.ExpiraEm), familia.Id);

            case ResultadoDoRefresh.ReusoDetectado:
                await unitOfWork.SalvarAsync(cancellationToken);
                return Renovacao.Recusada(RecusaDaRenovacao.ReusoDetectado, familia.Id);

            case ResultadoDoRefresh.Expirado:
                return Renovacao.Recusada(RecusaDaRenovacao.Expirado, familia.Id);

            default:
                return Renovacao.Recusada(RecusaDaRenovacao.FamiliaRevogada, familia.Id);
        }
    }
}

// Sem exceção para a recusa: na web a borda ainda precisa apagar o cookie na resposta.
public sealed record Renovacao(Sessao? Sessao, RecusaDaRenovacao? Recusa, Guid? FamiliaId)
{
    public static Renovacao Concedida(Sessao sessao, Guid familiaId) => new(sessao, null, familiaId);

    public static Renovacao Recusada(RecusaDaRenovacao recusa, Guid? familiaId = null) => new(null, recusa, familiaId);
}

public enum RecusaDaRenovacao
{
    Desconhecido,
    Expirado,
    FamiliaRevogada,

    // Este pedido acabou de revogar a família.
    ReusoDetectado,
}
