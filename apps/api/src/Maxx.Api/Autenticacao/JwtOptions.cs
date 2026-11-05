namespace Maxx.Api.Autenticacao;

// A chave HS256 vem só de variável de ambiente (Jwt__Chave), em base64, e é a mesma nas
// duas réplicas. Validade, emissor e audiência são decisão, não configuração
// (decisoes/README.md, Autenticação).
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public const string Emissor = "maxx-api";

    public const string Audiencia = "maxx";

    // HS256 pede chave de ao menos 256 bits.
    public const int TamanhoMinimoDaChaveEmBytes = 32;

    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(15);

    // Tolerância de relógio na validação. As réplicas estão no mesmo host.
    public static readonly TimeSpan Tolerancia = TimeSpan.FromSeconds(30);

    public string? Chave { get; set; }
}

// Os nomes das claims do access token.
public static class MaxxClaims
{
    public const string Sub = "sub";

    public const string Papel = "papel";
}
