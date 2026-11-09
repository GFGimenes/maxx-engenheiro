namespace Maxx.Domain.Identidade;

// Os domínios de e-mail aceitos (produto.md 2.4), que vêm da configuração da API. O papel
// nunca vem do domínio: a lista só diz se o endereço é de um domínio da faculdade. A
// comparação é do domínio inteiro, sem maiúsculas: subdomínio de um domínio aceito não é
// aceito.
public sealed class DominiosDeEmailAceitos
{
    private readonly HashSet<string> _dominios;

    public DominiosDeEmailAceitos(IEnumerable<string> dominios)
    {
        ArgumentNullException.ThrowIfNull(dominios);
        _dominios = new HashSet<string>(StringComparer.Ordinal);
        foreach (var bruto in dominios)
        {
            var dominio = bruto?.Trim().TrimStart('@').ToLowerInvariant();
            if (string.IsNullOrEmpty(dominio) || dominio.Contains('@', StringComparison.Ordinal) || dominio.Any(char.IsWhiteSpace))
            {
                throw new DomainException($"Domínio de e-mail inválido na lista: '{bruto}'.");
            }

            _dominios.Add(dominio);
        }

        if (_dominios.Count == 0)
        {
            throw new DomainException("A lista de domínios de e-mail aceitos está vazia.");
        }
    }

    public IReadOnlyCollection<string> Dominios => _dominios;

    public bool Aceita(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var arroba = email.LastIndexOf('@');
        return arroba >= 0 && _dominios.Contains(email[(arroba + 1)..].Trim().ToLowerInvariant());
    }
}
