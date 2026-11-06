using Maxx.Domain.Identidade;

namespace Maxx.Application.Autenticacao;

// O primeiro prefixo livre que o nome sugere (produto.md 2.3.1), perguntando ao banco em
// lotes. Usado no cadastro e na entrada da demonstração.
public sealed class SugestaoDeArroba(IUsuarioRepository usuarios)
{
    // Quantos candidatos de sugestão por consulta ao banco.
    private const int _loteDeSugestoes = 20;

    public Task<string> PrimeiroLivreAsync(string nomeCompleto, CancellationToken cancellationToken) =>
        PrimeiroLivreAsync(nomeCompleto, new HashSet<string>(), cancellationToken);

    // recusados: prefixos que a gravação já recusou neste pedido, pulados sem perguntar ao banco.
    public async Task<string> PrimeiroLivreAsync(
        string nomeCompleto, IReadOnlySet<string> recusados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recusados);

        var candidatos = PrefixoDeArroba.Sugestoes(nomeCompleto).Where(candidato => !recusados.Contains(candidato));
        foreach (var lote in candidatos.Chunk(_loteDeSugestoes))
        {
            var ocupados = await usuarios.PrefixosOcupadosAsync(lote, cancellationToken);
            var livre = lote.FirstOrDefault(candidato => !ocupados.Contains(candidato));
            if (livre is not null)
            {
                return livre;
            }
        }

        throw new InvalidOperationException($"Nenhuma sugestão de arroba livre para \"{nomeCompleto}\".");
    }
}
