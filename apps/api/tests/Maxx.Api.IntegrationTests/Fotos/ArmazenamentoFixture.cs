using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Maxx.Api.IntegrationTests.Fotos;

// O mesmo emulador do compose (VersityGW, compatível com S3), com um bucket de leitura
// pública como o do R2 atrás da CDN. Só desenvolvimento e teste, nunca produção.
public sealed class ArmazenamentoFixture : IAsyncLifetime
{
    public const string Imagem = "versity/versitygw:v1.10.0";
    public const string Bucket = "maxx-fotos-teste";
    public const string Regiao = "auto";

    private const int _porta = 7070;
    private const string _chaveDeAcesso = "maxx-teste";
    private const string _segredo = "maxx-teste-segredo";

    private readonly IContainer _container = new ContainerBuilder(Imagem)
        .WithEnvironment("ROOT_ACCESS_KEY", _chaveDeAcesso)
        .WithEnvironment("ROOT_SECRET_KEY", _segredo)
        .WithEnvironment("VGW_REGION", Regiao)
        .WithCommand("posix", "/tmp")
        .WithPortBinding(_porta, assignRandomHostPort: true)

        // Sem credencial, a raiz responde 403: já é o gateway atendendo.
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(pedido => pedido
            .ForPort(_porta)
            .ForStatusCodeMatching(_ => true)))
        .Build();

    public Uri Endpoint => new($"http://{_container.Hostname}:{_container.GetMappedPublicPort(_porta)}/");

    public Uri UrlDeLeitura => new(Endpoint, $"{Bucket}/");

    // A configuração da API apontando para este emulador, no formato do IConfiguration.
    public IReadOnlyDictionary<string, string> Configuracao => new Dictionary<string, string>
    {
        ["Armazenamento:Bucket"] = Bucket,
        ["Armazenamento:Endpoint"] = Endpoint.ToString(),
        ["Armazenamento:UrlDeLeitura"] = UrlDeLeitura.ToString(),
        ["Armazenamento:Regiao"] = Regiao,
        ["Armazenamento:ChaveDeAcesso"] = _chaveDeAcesso,
        ["Armazenamento:Segredo"] = _segredo,
    };

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        using var s3 = new AmazonS3Client(
            new BasicAWSCredentials(_chaveDeAcesso, _segredo),
            new AmazonS3Config { ServiceURL = Endpoint.ToString(), AuthenticationRegion = Regiao, ForcePathStyle = true });

        // Com região fora da lista da AWS, como o "auto" do R2, a criação leva a região.
        await s3.PutBucketAsync(new PutBucketRequest { BucketName = Bucket, BucketRegionName = Regiao });
        await s3.PutBucketPolicyAsync(new PutBucketPolicyRequest { BucketName = Bucket, Policy = PoliticaDeLeituraPublica(Bucket) });
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    // Leitura anônima de objeto, sem listagem: quem não tem a chave não descobre nada
    // (ADR 0002). A mesma do compose.
    public static string PoliticaDeLeituraPublica(string bucket) =>
        $$"""{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{bucket}}/*"]}]}""";
}
