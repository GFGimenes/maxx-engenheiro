using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Autenticacao;
using Maxx.Api.LimiteDeTaxa;
using Maxx.Application.Fotos;
using Maxx.Domain.Fotos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Fotos;

// O pedido de envio de foto (ADR 0002): a API valida e assina, e o app envia as duas
// derivadas direto ao bucket. Os bytes nunca passam por aqui.
internal static class UploadEndpoints
{
    public static IEndpointRouteBuilder MapUploadEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapPost("/uploads", PedirAsync)
            .WithName("PedirUpload")
            .WithTags("Fotos")
            .ComLimiteDeTaxa(Limite.UploadPorUsuario, contexto => contexto.HttpContext.User.FindFirstValue(MaxxClaims.Sub))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return v1;
    }

    private static async Task<Created<UploadResponse>> PedirAsync(
        PedidoDeUploadRequest pedido,
        ClaimsPrincipal principal,
        PedirUploadHandler handler,
        CancellationToken cancellationToken)
    {
        var finalidade = pedido.Finalidade == PedidoDeUploadRequest.FotoDePerfil ? FinalidadeDoUpload.FotoDePerfil : FinalidadeDoUpload.Post;
        var assinado = await handler.PedirAsync(
            UsuarioId(principal), finalidade, pedido.TipoDeConteudo, pedido.TamanhoDoFeed, pedido.TamanhoDaGrade, cancellationToken);
        return TypedResults.Created((string?)null, UploadResponse.De(assinado));
    }

    // O sub do token; o grupo /v1 já exigiu um usuário autenticado.
    private static Guid UsuarioId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(MaxxClaims.Sub), out var id)
            ? id
            : throw new InvalidOperationException("Token autenticado sem sub.");
}

// O tipo e os tamanhos exatos, em bytes, das duas derivadas que o app já gerou: é o que vai
// preso na assinatura. Os limites são do domínio (UploadPendente); aqui só a forma.
public sealed record PedidoDeUploadRequest(
    [property: Required, AllowedValues(PedidoDeUploadRequest.Post, PedidoDeUploadRequest.FotoDePerfil)] string Finalidade,
    [property: Required, StringLength(100)] string TipoDeConteudo,
    long TamanhoDoFeed,
    long TamanhoDaGrade)
{
    public const string Post = "post";
    public const string FotoDePerfil = "fotoDePerfil";
}

// Id para confirmar depois (no post ou na foto de perfil) e as duas URLs. O PUT de cada uma
// leva exatamente o content-type e o corpo do tamanho informados, antes de expiraEm.
public sealed record UploadResponse(Guid Id, DateTimeOffset ExpiraEm, EnvioResponse Feed, EnvioResponse Grade)
{
    public static UploadResponse De(UploadAssinado assinado) => new(
        assinado.Upload.Id, assinado.Upload.AssinaturaExpiraEm, EnvioResponse.De(assinado.Feed), EnvioResponse.De(assinado.Grade));
}

public sealed record EnvioResponse(string Url, string TipoDeConteudo, long Tamanho)
{
    public static EnvioResponse De(EnvioAssinado envio) => new(envio.Url.AbsoluteUri, envio.TipoDeConteudo, envio.Tamanho);
}
