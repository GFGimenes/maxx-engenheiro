using Maxx.Domain.Identidade;

namespace Maxx.Application.Autenticacao.Termos;

// O termo de uso vigente, e a conferência do que o aluno disse ter aceitado.
public sealed class TermoDeUsoHandler(ITermoDeUsoRepository termos)
{
    public async Task<TermoDeUso> VigenteAsync(CancellationToken cancellationToken) =>
        await termos.VigenteAsync(cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDeRecusa.TermoIndisponivel, "Nenhum termo de uso publicado.");

    // O aluno aceita o texto que leu. Se a versão mudou enquanto ele lia, a conta não nasce
    // com o aceite de um texto que já não vale.
    public async Task<TermoDeUso> ConferirAceiteAsync(int versaoAceita, CancellationToken cancellationToken)
    {
        var vigente = await termos.VigenteAsync(cancellationToken);
        return vigente?.Versao == versaoAceita
            ? vigente
            : throw new RecusaException(
                TipoDeRecusa.Conflito, CodigosDeRecusa.TermoDesatualizado, $"Aceite da versão {versaoAceita}, vigente {vigente?.Versao}.");
    }
}
