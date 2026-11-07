using System.ComponentModel.DataAnnotations;
using Maxx.Api.LimiteDeTaxa;
using Maxx.Application.Autenticacao.Cadastro;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Autenticacao;

// O cadastro do aluno pelo app. RA e e-mail vão no corpo, nunca na URL: são dado pessoal
// e não podem parar em log de acesso.
internal static class CadastroEndpoints
{
    public static IEndpointRouteBuilder MapCadastroEndpoints(this IEndpointRouteBuilder auth)
    {
        var cadastro = auth.MapGroup("/cadastro").WithTags("Autenticacao");

        cadastro.MapPost("/sugestao", SugerirAsync)
            .WithName("SugerirArroba")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ComLimiteDeTaxa(Limite.ConsultaDeCadastroPorIp, contexto => LimiteDeTaxaExtensions.Ip(contexto.HttpContext));

        cadastro.MapPost("/disponibilidade", VerificarDisponibilidadeAsync)
            .WithName("VerificarDisponibilidadeDoPrefixo")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ComLimiteDeTaxa(Limite.ConsultaDeCadastroPorIp, contexto => LimiteDeTaxaExtensions.Ip(contexto.HttpContext));

        cadastro.MapPost("", CadastrarAsync)
            .WithName("Cadastrar")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ComLimiteDeTaxa(Limite.CadastroPorRa, contexto => LimiteDeTaxaExtensions.Do<CadastroRequest>(contexto, pedido => pedido.Ra));

        return auth;
    }

    private static async Task<Ok<SugestaoResponse>> SugerirAsync(
        IdentificacaoRequest pedido, CadastroHandler handler, CancellationToken cancellationToken)
    {
        var arroba = await handler.SugerirAsync(pedido.Ra, pedido.Email, cancellationToken);
        return TypedResults.Ok(new SugestaoResponse(arroba.Prefixo, arroba.Sufixo, arroba.ToString()));
    }

    private static async Task<Ok<DisponibilidadeResponse>> VerificarDisponibilidadeAsync(
        DisponibilidadeRequest pedido, CadastroHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(new DisponibilidadeResponse(
            await handler.PrefixoDisponivelAsync(pedido.Ra, pedido.Email, pedido.Prefixo, cancellationToken)));

    private static async Task<Created<SessaoResponse>> CadastrarAsync(
        CadastroRequest pedido, CadastroHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var sessao = await handler.CadastrarAsync(
            new NovoCadastro(pedido.Ra, pedido.Email, pedido.NomeDeExibicao, pedido.Prefixo, pedido.Senha, pedido.VersaoDoTermo), cancellationToken);
        return TypedResults.Created((string?)null, SessaoHttp.Escrever(httpContext, sessao));
    }
}

// Na borda só presença e teto de tamanho; a forma do prefixo, da senha e do nome é regra
// do domínio e volta com código próprio.
public sealed record IdentificacaoRequest(
    [property: Required, StringLength(32)] string Ra,
    [property: Required, StringLength(254)] string Email);

public sealed record DisponibilidadeRequest(
    [property: Required, StringLength(32)] string Ra,
    [property: Required, StringLength(254)] string Email,
    [property: Required(AllowEmptyStrings = true), StringLength(64)] string Prefixo);

public sealed record CadastroRequest(
    [property: Required, StringLength(32)] string Ra,
    [property: Required, StringLength(254)] string Email,
    [property: Required(AllowEmptyStrings = true), StringLength(240)] string NomeDeExibicao,
    [property: Required(AllowEmptyStrings = true), StringLength(64)] string Prefixo,
    [property: Required(AllowEmptyStrings = true), StringLength(512)] string Senha,
    [property: Range(1, int.MaxValue)] int VersaoDoTermo)
{
    public override string ToString() => $"CadastroRequest {{ Ra = {Ra}, Prefixo = {Prefixo} }}";
}

public sealed record SugestaoResponse(string Prefixo, string Sufixo, string Arroba);

public sealed record DisponibilidadeResponse(bool Disponivel);
