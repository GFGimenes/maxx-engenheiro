using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Maxx.Application.Fotos;
using Microsoft.Extensions.Options;

namespace Maxx.Infrastructure.Fotos;

// O bucket pela API do S3, que o R2 e o emulador falam igual. Dois clientes: um assina
// com o endereço que o app alcança, sem ir à rede; o outro faz HEAD e remoção pelo
// endereço que a API alcança. Criados no primeiro uso: sem configuração, em
// desenvolvimento, a API sobe e só o que depende do bucket falha.
internal sealed class ArmazenamentoS3(IOptions<ArmazenamentoOptions> options) : IArmazenamentoDeFotos, IDisposable
{
    // Limite do DeleteObjects do S3.
    private const int _removidosPorPedido = 1000;

    private readonly Lazy<Clientes> _clientes = new(() => Clientes.Criar(options.Value));

    public EnvioAssinado AssinarEnvio(string chave, string tipoDeConteudo, long tamanho, DateTimeOffset expiraEm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDeConteudo);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tamanho);

        var clientes = _clientes.Value;
        var pedido = new GetPreSignedUrlRequest
        {
            BucketName = clientes.Bucket,
            Key = chave,
            Verb = HttpVerb.PUT,
            Expires = expiraEm.UtcDateTime,
            Protocol = clientes.ProtocoloPublico,
            ContentType = tipoDeConteudo,
        };

        // Entra nos cabeçalhos assinados (X-Amz-SignedHeaders): o bucket recusa um corpo
        // de outro tamanho. É o que prende o tamanho, porque o R2 não aceita o POST com
        // política de content-length-range.
        pedido.Headers.ContentLength = tamanho;

        var url = clientes.Assinatura.GetPreSignedURL(pedido);
        return new EnvioAssinado(chave, new Uri(url), tipoDeConteudo, tamanho, expiraEm);
    }

    public async Task EnviarAsync(string chave, byte[] conteudo, string tipoDeConteudo, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        ArgumentNullException.ThrowIfNull(conteudo);
        var clientes = _clientes.Value;
        using var corpo = new MemoryStream(conteudo, writable: false);
        await clientes.Operacao.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = clientes.Bucket,
                Key = chave,
                InputStream = corpo,
                ContentType = tipoDeConteudo,

                // O corpo assinado de uma vez, não em pedaços: o R2 recusa o
                // STREAMING-AWS4-HMAC-SHA256-PAYLOAD, que é o padrão do SDK e que o emulador
                // aceita. Provado só contra o R2 (VerificacaoDoR2Tests).
                UseChunkEncoding = false,
            },
            cancellationToken);
    }

    public async Task<bool> ExisteAsync(string chave, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        var clientes = _clientes.Value;
        try
        {
            await clientes.Operacao.GetObjectMetadataAsync(clientes.Bucket, chave, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task RemoverAsync(IReadOnlyCollection<string> chaves, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chaves);
        var clientes = _clientes.Value;

        foreach (var lote in chaves.Distinct(StringComparer.Ordinal).Chunk(_removidosPorPedido))
        {
            var resposta = await clientes.Operacao.DeleteObjectsAsync(
                new DeleteObjectsRequest
                {
                    BucketName = clientes.Bucket,
                    Objects = [.. lote.Select(chave => new KeyVersion { Key = chave })],
                    Quiet = true,
                },
                cancellationToken);

            // Chave que já não existia não aparece como erro no S3; o que aparece é falha
            // de verdade, e o trabalho da fila tenta de novo.
            if (resposta.DeleteErrors is { Count: > 0 } erros)
            {
                throw new InvalidOperationException(
                    $"O bucket recusou remover {erros.Count} objeto(s): {string.Join(", ", erros.Select(erro => erro.Code).Distinct())}.");
            }
        }
    }

    public Uri UrlDeLeitura(string chave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);
        return new Uri(_clientes.Value.BaseDeLeitura, string.Join('/', chave.Split('/').Select(Uri.EscapeDataString)));
    }

    public void Dispose()
    {
        if (_clientes.IsValueCreated)
        {
            _clientes.Value.Dispose();
        }
    }

    private sealed record Clientes(
        AmazonS3Client Assinatura, AmazonS3Client Operacao, string Bucket, Protocol ProtocoloPublico, Uri BaseDeLeitura) : IDisposable
    {
        public static Clientes Criar(ArmazenamentoOptions config)
        {
            if (config.Faltando() is { Count: > 0 } faltando)
            {
                throw new InvalidOperationException($"Armazenamento de fotos não configurado: defina {string.Join(", ", faltando)}.");
            }

            var publico = Endereco(string.IsNullOrWhiteSpace(config.EndpointPublico) ? config.Endpoint : config.EndpointPublico);
            var credenciais = new BasicAWSCredentials(config.ChaveDeAcesso, config.Segredo);
            return new Clientes(
                Cliente(credenciais, publico, config.Regiao),
                Cliente(credenciais, Endereco(config.Endpoint), config.Regiao),
                config.Bucket,
                publico.Scheme == Uri.UriSchemeHttp ? Protocol.HTTP : Protocol.HTTPS,
                Endereco(config.UrlDeLeitura));
        }

        public void Dispose()
        {
            Assinatura.Dispose();
            Operacao.Dispose();
        }

        private static AmazonS3Client Cliente(AWSCredentials credenciais, Uri endpoint, string regiao) =>
            new(credenciais, new AmazonS3Config
            {
                ServiceURL = endpoint.ToString(),
                AuthenticationRegion = regiao,

                // Bucket no caminho, não no subdomínio: o emulador não tem DNS por bucket, e o
                // R2 aceita os dois.
                ForcePathStyle = true,

                // Só quando a operação exige: os checksums que o SDK 4 manda por padrão não
                // são aceitos por todo serviço compatível, o R2 incluído.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });

        private static Uri Endereco(string texto) =>
            Uri.TryCreate(texto.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var url) && url.Scheme is "http" or "https"
                ? url
                : throw new InvalidOperationException($"Endereço do armazenamento de fotos inválido: {texto}.");
    }
}
