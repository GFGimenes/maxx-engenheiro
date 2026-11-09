namespace Maxx.Application.Fotos;

// Um envio autorizado: a URL que o app usa no PUT direto ao bucket, com o tipo e o tamanho
// exatos presos na assinatura. Qualquer outro content-type ou Content-Length é recusado
// pelo próprio bucket (ADR 0002).
public sealed record EnvioAssinado(string Chave, Uri Url, string TipoDeConteudo, long Tamanho, DateTimeOffset ExpiraEm);

// O bucket das fotos: Cloudflare R2 em produção, um emulador compatível com S3 no
// desenvolvimento e nos testes. A API nunca recebe nem lê os bytes de uma foto (ADR 0002):
// assina o envio, pergunta se o objeto chegou e remove o que ficou sem dono.
public interface IArmazenamentoDeFotos
{
    // Só assina, sem ir à rede. A chave tem de ser aleatória: a leitura é pública para
    // quem tem o link.
    EnvioAssinado AssinarEnvio(string chave, string tipoDeConteudo, long tamanho, DateTimeOffset expiraEm);

    // HEAD no objeto: o app diz que enviou, e isto confere.
    Task<bool> ExisteAsync(string chave, CancellationToken cancellationToken);

    // Idempotente: chave que não existe não é erro.
    Task RemoverAsync(IReadOnlyCollection<string> chaves, CancellationToken cancellationToken);

    // Grava um objeto direto pelo endereço interno. Só o seed demo usa, para as fotos do mundo
    // de demonstração (passo 12): nenhuma rota da API chama isto, e a foto de quem usa o app
    // continua indo direto do aparelho para o bucket.
    Task EnviarAsync(string chave, byte[] conteudo, string tipoDeConteudo, CancellationToken cancellationToken);

    // Onde o app lê a foto: a CDN em produção, o emulador no desenvolvimento.
    Uri UrlDeLeitura(string chave);
}
