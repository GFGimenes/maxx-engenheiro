using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Medalhas;
using Maxx.Api.Posts;
using Maxx.Application;
using Maxx.Application.Fotos;
using Maxx.Application.Mural;
using Maxx.Application.Perfil;
using Maxx.Application.Social;
using Maxx.Domain.Mural;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Mural;

// O aviso do mural (produto.md 7). Publicar é do professor (turmas suas) e da instituição
// (faculdade e cursos); quem decide o papel é o domínio, e o escopo de turma, o banco. Ler
// é de qualquer usuário logado, pelo mural dele: os vínculos do banco decidem o que ele vê.
internal static class AvisoEndpoints
{
    public const int AvisosPorPagina = 20;

    public static IEndpointRouteBuilder MapAvisoEndpoints(this IEndpointRouteBuilder v1)
    {
        var avisos = v1.MapGroup("/avisos").WithTags("Mural");

        avisos.MapPost("/", PublicarAsync)
            .WithName("PublicarAviso")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        avisos.MapGet("/{avisoId:guid}", AvisoAsync)
            .WithName("GetAviso")
            .Produces<AvisoResponse>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound);

        avisos.MapPut("/{avisoId:guid}/leitura", LerAsync)
            .WithName("LerAviso")
            .ProducesProblem(StatusCodes.Status404NotFound);

        var mural = v1.MapGroup("/mural").WithTags("Mural");

        mural.MapGet("/", MuralAsync)
            .WithName("GetMural")
            .Produces<PaginaDoMuralResponse>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesValidationProblem();

        mural.MapPost("/leitura", LerTodosAsync)
            .WithName("LerTodosOsAvisos");

        // Os avisos que o professor publicou, no perfil dele (produto.md 2.1): pela mesma
        // regra do mural, cada um vê só os que estão no mural dele.
        v1.MapGet("/perfis/{usuarioId:guid}/avisos", DoAutorAsync)
            .WithName("GetAvisosDoPerfil")
            .WithTags("Perfil")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return v1;
    }

    // O mural combinado, filtrado pelos vínculos de quem pede, com ETag: o app pergunta de
    // novo ao voltar à aba, e a resposta não muda na maior parte das vezes.
    private static async Task<IResult> MuralAsync(
        [AllowedValues(null, PublicarAvisoRequest.Faculdade, PublicarAvisoRequest.Curso, PublicarAvisoRequest.Turma)] string? escopo,
        string? cursor,
        HttpContext http,
        ClaimsPrincipal principal,
        MuralHandler handler,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var pagina = await handler.MuralAsync(
            PostEndpoints.UsuarioId(principal), Escopo(escopo), null, cursor, AvisosPorPagina, cancellationToken);
        return Perfil.PerfilEndpoints.ComEtag(http, PaginaDoMuralResponse.De(pagina, armazenamento));
    }

    private static async Task<Ok<PaginaDoMuralResponse>> DoAutorAsync(
        Guid usuarioId,
        string? cursor,
        ClaimsPrincipal principal,
        MuralHandler handler,
        IConsultaDePerfil perfis,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        // Perfil inexistente é 404, como na grade de posts do perfil.
        var leitor = PostEndpoints.UsuarioId(principal);
        _ = await perfis.PerfilAsync(usuarioId, leitor, cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosSociais.UsuarioNaoEncontrado, $"Usuário {usuarioId} sem perfil.");
        var pagina = await handler.MuralAsync(leitor, null, usuarioId, cursor, AvisosPorPagina, cancellationToken);
        return TypedResults.Ok(PaginaDoMuralResponse.De(pagina, armazenamento));
    }

    private static async Task<IResult> AvisoAsync(
        Guid avisoId,
        HttpContext http,
        ClaimsPrincipal principal,
        MuralHandler handler,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var aviso = await handler.AvisoAsync(PostEndpoints.UsuarioId(principal), avisoId, cancellationToken);
        return Perfil.PerfilEndpoints.ComEtag(http, AvisoResponse.De(aviso, armazenamento));
    }

    private static async Task<NoContent> LerAsync(
        Guid avisoId, ClaimsPrincipal principal, MuralHandler handler, CancellationToken cancellationToken)
    {
        await handler.LerAsync(PostEndpoints.UsuarioId(principal), avisoId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> LerTodosAsync(
        ClaimsPrincipal principal, MuralHandler handler, CancellationToken cancellationToken)
    {
        await handler.LerTodosAsync(PostEndpoints.UsuarioId(principal), cancellationToken);
        return TypedResults.NoContent();
    }

    private static EscopoDoAviso? Escopo(string? escopo) => escopo switch
    {
        PublicarAvisoRequest.Faculdade => EscopoDoAviso.Faculdade,
        PublicarAvisoRequest.Curso => EscopoDoAviso.Curso,
        PublicarAvisoRequest.Turma => EscopoDoAviso.Turma,
        _ => null,
    };

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

// O aviso no mural de quem pede. O autor da instituição o app mostra como "UniMax"; o do
// professor, pelo nome. As matérias das turmas no escopo turma, as siglas dos cursos no
// escopo curso. "lido" é deste leitor: é o que acende a bolinha e abre ou recolhe o cartão.
public sealed record AvisoResponse(
    Guid Id,
    string Escopo,
    string Titulo,
    string Texto,
    FotoDoPostResponse? Foto,
    DateTimeOffset PublicadoEm,
    AutorDoAvisoResponse Autor,
    IReadOnlyList<string> Materias,
    IReadOnlyList<string> Cursos,
    bool Lido)
{
    public static AvisoResponse De(AvisoLido aviso, IArmazenamentoDeFotos armazenamento)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        return new(
            aviso.Id,
            ProfessorEndpoints.Nome(aviso.Escopo),
            aviso.Titulo,
            aviso.Texto,
            aviso is { ChaveDaFotoDoFeed: { } feed, ChaveDaFotoDaGrade: { } grade }
                ? new FotoDoPostResponse(armazenamento.UrlDeLeitura(feed).AbsoluteUri, armazenamento.UrlDeLeitura(grade).AbsoluteUri)
                : null,
            aviso.PublicadoEm,
            new AutorDoAvisoResponse(aviso.Autor.Id, aviso.Autor.NomeDeExibicao, aviso.Autor.Instituicao),
            aviso.Materias,
            aviso.Cursos,
            aviso.Lido);
    }
}

public sealed record AutorDoAvisoResponse(Guid Id, string NomeDeExibicao, bool Instituicao);

public sealed record PaginaDoMuralResponse(IReadOnlyList<AvisoResponse> Itens, string? Proximo)
{
    public static PaginaDoMuralResponse De(Application.Paginacao.Pagina<AvisoLido> pagina, IArmazenamentoDeFotos armazenamento)
    {
        ArgumentNullException.ThrowIfNull(pagina);
        return new([.. pagina.Itens.Select(aviso => AvisoResponse.De(aviso, armazenamento))], pagina.Proximo);
    }
}
