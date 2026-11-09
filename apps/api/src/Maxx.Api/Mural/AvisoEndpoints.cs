using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Medalhas;
using Maxx.Api.Posts;
using Maxx.Application;
using Maxx.Application.Fotos;
using Maxx.Application.Mural;
using Maxx.Domain.Mural;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Mural;

// O aviso do mural (produto.md 7). Publicar é do professor (turmas suas) e da instituição
// (faculdade e cursos); quem decide o papel é o domínio, e o escopo de turma, o banco.
// A leitura do mural chega na etapa 2 do passo 10.
internal static class AvisoEndpoints
{
    public static IEndpointRouteBuilder MapAvisoEndpoints(this IEndpointRouteBuilder v1)
    {
        var avisos = v1.MapGroup("/avisos").WithTags("Mural");

        avisos.MapPost("/", PublicarAsync)
            .WithName("PublicarAviso")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return v1;
    }

    private static async Task<Created<AvisoPublicadoResponse>> PublicarAsync(
        PublicarAvisoRequest pedido,
        ClaimsPrincipal principal,
        PublicarAvisoHandler handler,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var escopo = pedido.Escopo switch
        {
            PublicarAvisoRequest.Faculdade => EscopoDoAviso.Faculdade,
            PublicarAvisoRequest.Curso => EscopoDoAviso.Curso,
            _ => EscopoDoAviso.Turma,
        };
        var turmas = pedido.TurmaIds ?? [];
        var cursos = pedido.CursoIds ?? [];

        // Cada escopo lê a sua lista. Mandar a do outro escopo é pedido confuso, e o aviso
        // não pode nascer com um alvo que ninguém confere.
        if ((escopo != EscopoDoAviso.Turma && turmas.Count > 0) || (escopo != EscopoDoAviso.Curso && cursos.Count > 0))
        {
            throw new RecusaException(TipoDeRecusa.Invalido, CodigosDoMural.AlvosDoAvisoInvalidos, "Alvo de outro escopo.");
        }

        var aviso = await handler.PublicarAsync(
            PostEndpoints.UsuarioId(principal),
            escopo,
            escopo == EscopoDoAviso.Turma ? turmas : cursos,
            pedido.Titulo,
            pedido.Texto,
            pedido.UploadId,
            cancellationToken);
        return TypedResults.Created($"/api/v1/avisos/{aviso.Id}", AvisoPublicadoResponse.De(aviso, armazenamento));
    }
}

// O escopo e os alvos dele: as turmas no escopo turma, os cursos no escopo curso, nenhum
// na faculdade. Os limites de título e texto são em caracteres, contando um emoji como um, e
// do domínio: aqui só um teto bruto para o corpo.
public sealed record PublicarAvisoRequest(
    [property: Required, AllowedValues(PublicarAvisoRequest.Faculdade, PublicarAvisoRequest.Curso, PublicarAvisoRequest.Turma)]
    string Escopo,
    [property: StringLength(1000)] string? Titulo,
    [property: StringLength(10000)] string? Texto,
    [property: MaxLength(50)] IReadOnlyList<Guid>? TurmaIds = null,
    [property: MaxLength(50)] IReadOnlyList<Guid>? CursoIds = null,
    Guid? UploadId = null)
{
    public const string Faculdade = "faculdade";
    public const string Curso = "curso";
    public const string Turma = "turma";
}

// O aviso como acabou de ser gravado. A foto são as duas derivadas, lidas direto da CDN.
public sealed record AvisoPublicadoResponse(
    Guid Id,
    string Escopo,
    string Titulo,
    string Texto,
    FotoDoPostResponse? Foto,
    DateTimeOffset PublicadoEm,
    IReadOnlyList<Guid> TurmaIds,
    IReadOnlyList<Guid> CursoIds)
{
    public static AvisoPublicadoResponse De(Aviso aviso, IArmazenamentoDeFotos armazenamento) => new(
        aviso.Id,
        ProfessorEndpoints.Nome(aviso.Escopo),
        aviso.Titulo,
        aviso.Texto,
        aviso is { ChaveDaFotoDoFeed: { } feed, ChaveDaFotoDaGrade: { } grade }
            ? new FotoDoPostResponse(armazenamento.UrlDeLeitura(feed).AbsoluteUri, armazenamento.UrlDeLeitura(grade).AbsoluteUri)
            : null,
        aviso.PublicadoEm,
        [.. aviso.Turmas.Select(turma => turma.TurmaId)],
        [.. aviso.Cursos.Select(curso => curso.CursoId)]);
}
