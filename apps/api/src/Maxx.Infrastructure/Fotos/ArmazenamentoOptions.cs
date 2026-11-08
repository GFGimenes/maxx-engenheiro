namespace Maxx.Infrastructure.Fotos;

// O bucket das fotos (ADR 0002). Em produção, Cloudflare R2; no compose e nos testes, o
// VersityGW, compatível com S3. Chave e segredo só por variável de ambiente.
public sealed class ArmazenamentoOptions
{
    public const string Section = "Armazenamento";

    public string Bucket { get; set; } = string.Empty;

    // Por onde a API fala com o bucket (HEAD e remoção). No R2,
    // https://<conta>.r2.cloudflarestorage.com; no compose, o nome do serviço na rede
    // interna.
    public string Endpoint { get; set; } = string.Empty;

    // O endereço que vai na URL assinada, e portanto o que o app alcança. Vazio, é o
    // Endpoint, como no R2. No compose difere: a API alcança o emulador pela rede interna,
    // e o navegador, pela porta publicada na máquina.
    public string? EndpointPublico { get; set; }

    // Base da leitura pública: o domínio da CDN em produção. Termina em "/".
    public string UrlDeLeitura { get; set; } = string.Empty;

    // "auto" no R2.
    public string Regiao { get; set; } = "auto";

    public string ChaveDeAcesso { get; set; } = string.Empty;

    public string Segredo { get; set; } = string.Empty;

    // Os campos obrigatórios que faltam, pelo nome da variável de ambiente.
    public IReadOnlyList<string> Faltando()
    {
        var faltando = new List<string>();
        Exigir(Bucket, nameof(Bucket));
        Exigir(Endpoint, nameof(Endpoint));
        Exigir(UrlDeLeitura, nameof(UrlDeLeitura));
        Exigir(Regiao, nameof(Regiao));
        Exigir(ChaveDeAcesso, nameof(ChaveDeAcesso));
        Exigir(Segredo, nameof(Segredo));
        return faltando;

        void Exigir(string? valor, string nome)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                faltando.Add($"{Section}__{nome}");
            }
        }
    }
}
