using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Maxx.Api.Feed;
using Maxx.Api.Posts;
using Maxx.Api.Premios;
using Maxx.Application;
using Maxx.Application.Fotos;
using Maxx.Application.Paginacao;
using Maxx.Application.Perfil;
using Maxx.Application.Posts;
using Maxx.Application.Premios;
using Maxx.Application.Social;
using Maxx.Domain.Academico;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Maxx.Api.Perfil;

// Perfil, grade de posts, prêmios, busca e editar perfil (produto.md 6.4 e 6.5).
internal static class PerfilEndpoints
{
    // Seis linhas da grade de três colunas.
    public const int PostsPorPagina = 18;

    public const int AlunosPorPagina = 20;

    public static IEndpointRouteBuilder MapPerfilEndpoints(this IEndpointRouteBuilder v1)
    {
        var perfis = v1.MapGroup("/perfis/{usuarioId:guid}").WithTags("Perfil");

        perfis.MapGet("/", PerfilAsync)
            .WithName("GetPerfil")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound);

        perfis.MapGet("/posts", PostsAsync)
            .WithName("GetPostsDoPerfil")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        perfis.MapGet("/premios", PremiosAsync)
            .WithName("GetPremiosDoPerfil")
            .ProducesProblem(StatusCodes.Status404NotFound);

        var eu = v1.MapGroup("/eu/perfil").WithTags("Perfil");

        eu.MapGet("/", MeuPerfilAsync)
            .WithName("GetMeuPerfil")
            .Produces(StatusCodes.Status304NotModified);

        eu.MapPatch("/", EditarAsync)
            .WithName("EditarPerfil")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        eu.MapGet("/disponibilidade", DisponibilidadeAsync)
            .WithName("GetDisponibilidadeDoMeuPrefixo")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        v1.MapGet("/busca/alunos", BuscarAsync)
            .WithName("BuscarAlunos")
            .WithTags("Busca")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return v1;
    }

    private static async Task<IResult> PerfilAsync(
        Guid usuarioId,
        HttpContext http,
        ClaimsPrincipal principal,
        IConsultaDePerfil consulta,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var perfil = await consulta.PerfilAsync(usuarioId, PostEndpoints.UsuarioId(principal), cancellationToken)
            ?? throw NaoEncontrado(usuarioId);
        return ComEtag(http, PerfilResponse.De(perfil, armazenamento));
    }

    private static Task<IResult> MeuPerfilAsync(
        HttpContext http,
        ClaimsPrincipal principal,
        IConsultaDePerfil consulta,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken) =>
        PerfilAsync(PostEndpoints.UsuarioId(principal), http, principal, consulta, armazenamento, cancellationToken);

    private static async Task<Ok<PaginaDoFeedResponse>> PostsAsync(
        Guid usuarioId,
        string? cursor,
        ClaimsPrincipal principal,
        IConsultaDePerfil consulta,
        IConsultaDePosts posts,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var posicao = CursorDeTempo.Ler(cursor);
        var leitor = PostEndpoints.UsuarioId(principal);
        _ = await consulta.PerfilAsync(usuarioId, leitor, cancellationToken) ?? throw NaoEncontrado(usuarioId);

        var pagina = await consulta.PostsAsync(usuarioId, posicao, PostsPorPagina, cancellationToken);
        var lidos = await posts.PostsAsync(pagina.Itens, leitor, cancellationToken);
        return TypedResults.Ok(new PaginaDoFeedResponse(
            [.. pagina.Itens.Where(lidos.ContainsKey).Select(id => PostResponse.De(lidos[id], armazenamento))], pagina.Proximo));
    }

    // Só os prêmios obtidos: os faltantes e o progresso são do próprio aluno, em /v1/eu/premios.
    private static async Task<Ok<PremiosDoAlunoResponse>> PremiosAsync(
        Guid usuarioId, IConsultaDePremios consulta, CancellationToken cancellationToken)
    {
        var premios = await consulta.DoAlunoAsync(usuarioId, cancellationToken) ?? throw NaoEncontrado(usuarioId);
        return TypedResults.Ok(PremiosEndpoints.Mapear(premios, soObtidos: true));
    }

    private static async Task<IResult> EditarAsync(
        EditarPerfilRequest pedido,
        HttpContext http,
        ClaimsPrincipal principal,
        EditarPerfilHandler handler,
        IConsultaDePerfil consulta,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var eu = PostEndpoints.UsuarioId(principal);
        await handler.EditarAsync(eu, pedido.NomeDeExibicao, pedido.Prefixo, pedido.FotoUploadId, pedido.RemoverFoto ?? false, cancellationToken);
        var perfil = await consulta.PerfilAsync(eu, eu, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosSociais.UsuarioNaoEncontrado, "Conta sem perfil social.");
        return ComEtag(http, PerfilResponse.De(perfil, armazenamento), aceitar304: false);
    }

    private static async Task<Ok<DisponibilidadeDoPrefixoResponse>> DisponibilidadeAsync(
        [Required] string prefixo, ClaimsPrincipal principal, EditarPerfilHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(new DisponibilidadeDoPrefixoResponse(
            await handler.PrefixoDisponivelAsync(PostEndpoints.UsuarioId(principal), prefixo, cancellationToken)));

    private static async Task<Ok<PaginaDeAlunosResponse>> BuscarAsync(
        string? termo,
        string? curso,
        [Range(1, 8)] int? semestre,
        string? cursor,
        ClaimsPrincipal principal,
        BuscaDeAlunosHandler handler,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var pagina = await handler.BuscarAsync(PostEndpoints.UsuarioId(principal), termo, curso, semestre, cursor, AlunosPorPagina, cancellationToken);
        return TypedResults.Ok(new PaginaDeAlunosResponse(
            [.. pagina.Itens.Select(aluno => new AlunoEncontradoResponse(
                aluno.Id,
                aluno.NomeDeExibicao,
                aluno.Arroba,
                aluno.Curso,
                aluno.Semestre,
                aluno.ExAluno,
                PostResponse.Leitura(armazenamento, aluno.ChaveDaFotoDaGrade),
                aluno.EuSigo))],
            pagina.Proximo));
    }

    // ETag do conteúdo, com 304 quando o app já tem a versão (decisoes/README.md, Cache). A
    // resposta depende de quem pede (RA, "eu sigo"): private, nunca em cache compartilhado.
    private static IResult ComEtag<T>(HttpContext http, T corpo, bool aceitar304 = true)
    {
        var opcoes = http.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
        var etag = $"W/\"{Convert.ToBase64String(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(corpo, opcoes)))[..22]}\"";
        http.Response.Headers.ETag = etag;
        http.Response.Headers.CacheControl = "private, no-cache";

        return aceitar304 && http.Request.Headers.IfNoneMatch.Any(valor => valor == etag)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(corpo);
    }

    private static RecusaException NaoEncontrado(Guid usuarioId) =>
        new(TipoDeRecusa.NaoEncontrado, CodigosSociais.UsuarioNaoEncontrado, $"Usuário {usuarioId} sem perfil.");
}

// Papel em minúsculas ("aluno" ou "professor"). RA só no próprio perfil do aluno; pontos só
// de aluno (a soma das medalhas ativas); a posição no ranking chega no passo 11.
public sealed record PerfilResponse(
    Guid Id,
    string NomeDeExibicao,
    string Arroba,
    string Papel,
    string? Curso,
    string? NomeDoCurso,
    int? Semestre,
    bool ExAluno,
    string? Ra,
    int? Pontos,
    int Seguidores,
    int Seguindo,
    bool EuSigo,
    FotoDoPostResponse? Foto,
    ProfessorDoPerfilResponse? Professor)
{
    public static PerfilResponse De(PerfilLido perfil, IArmazenamentoDeFotos armazenamento) => new(
        perfil.Id,
        perfil.NomeDeExibicao,
        perfil.Arroba,
        perfil.Papeis == Papeis.Aluno ? "aluno" : "professor",
        perfil.Curso,
        perfil.NomeDoCurso,
        perfil.Semestre,
        perfil.ExAluno,
        perfil.Ra,
        perfil.Pontos,
        perfil.Seguidores,
        perfil.Seguindo,
        perfil.EuSigo,
        perfil is { ChaveDaFotoDoFeed: { } feed, ChaveDaFotoDaGrade: { } grade }
            ? new FotoDoPostResponse(armazenamento.UrlDeLeitura(feed).AbsoluteUri, armazenamento.UrlDeLeitura(grade).AbsoluteUri)
            : null,
        perfil.Professor is { } professor
            ? new ProfessorDoPerfilResponse(
                [.. professor.Turmas.Select(turma => new TurmaDoPerfilResponse(turma.Materia, turma.Curso, turma.Semestre))],
                professor.MedalhasConcedidas)
            : null);
}

public sealed record TurmaDoPerfilResponse(string Materia, string Curso, string Semestre);

public sealed record ProfessorDoPerfilResponse(IReadOnlyList<TurmaDoPerfilResponse> Turmas, int MedalhasConcedidas);

// Cada campo é opcional; o que vier nulo não muda. Foto nova pelo id de um upload de foto de
// perfil já enviado ao bucket, ou removerFoto, nunca os dois.
public sealed record EditarPerfilRequest(
    [property: StringLength(240)] string? NomeDeExibicao,
    [property: StringLength(64)] string? Prefixo,
    Guid? FotoUploadId,
    bool? RemoverFoto);

public sealed record DisponibilidadeDoPrefixoResponse(bool Disponivel);

public sealed record AlunoEncontradoResponse(
    Guid Id, string NomeDeExibicao, string Arroba, string? Curso, int? Semestre, bool ExAluno, string? Foto, bool EuSigo);

public sealed record PaginaDeAlunosResponse(IReadOnlyList<AlunoEncontradoResponse> Itens, string? Proximo);
