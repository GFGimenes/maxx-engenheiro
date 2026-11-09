using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Autenticacao;
using Maxx.Api.Medalhas;
using Maxx.Api.Posts;
using Maxx.Api.PremiosParticipacao;
using Maxx.Application.Fotos;
using Maxx.Application.Instituicao;
using Maxx.Application.Medalhas;
using Maxx.Application.PremiosParticipacao;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Instituicao;

// A área da instituição (produto.md 10.2, passo 12): busca e ficha do aluno, e os atos
// sobre a linha dele. Só a instituição; o papel é conferido de novo no banco.
internal static class InstituicaoEndpoints
{
    public static IEndpointRouteBuilder MapInstituicaoEndpoints(this IEndpointRouteBuilder v1)
    {
        var alunos = v1.MapGroup("/instituicao/alunos").WithTags("Instituicao").RequireAuthorization(Politicas.Instituicao);

        alunos.MapGet("/", BuscarAsync)
            .WithName("BuscarAlunosDaBase")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        alunos.MapGet("/{cadastroId:guid}", FichaAsync)
            .WithName("GetFichaDoAluno")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        alunos.MapPut("/{cadastroId:guid}/email", CorrigirEmailAsync)
            .WithName("CorrigirEmailDoAluno")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        alunos.MapPost("/{cadastroId:guid}/ex-aluno", MarcarExAlunoAsync)
            .WithName("MarcarExAluno")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        alunos.MapPut("/{cadastroId:guid}/curso", TrocarDeCursoAsync)
            .WithName("TrocarCursoDoAluno")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // A concessão direta de prêmio de participação (passo 12, P2), com os eventos e as
        // matérias que ela oferece para o aluno.
        alunos.MapPost("/{cadastroId:guid}/premios-participacao", ConcederAsync)
            .WithName("ConcederPremioParticipacao")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        alunos.MapGet("/{cadastroId:guid}/eventos", EventosAsync)
            .WithName("GetEventosParaConcessao")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        alunos.MapGet("/{cadastroId:guid}/materias", MateriasAsync)
            .WithName("GetMateriasParaConcessao")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // A anulação pela área web: o mesmo ato do comando administrativo. Anular de novo não
        // muda nada e responde igual.
        v1.MapPost("/instituicao/medalhas/{medalhaId:guid}/anulacao", AnularAsync)
            .WithName("AnularMedalha")
            .WithTags("Instituicao")
            .RequireAuthorization(Politicas.Instituicao)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // A purga da demonstração por lote (D14 do passo 7). Mapeada sempre, com a flag da
        // demonstração ligada ou não: a purga é depois da avaliação, com a flag já desligada.
        var lotes = v1.MapGroup("/instituicao/demonstracao/lotes").WithTags("Instituicao").RequireAuthorization(Politicas.Instituicao);

        lotes.MapGet("/", LotesAsync)
            .WithName("GetLotesDaDemonstracao")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        lotes.MapPost("/{lote}/purga", PurgarAsync)
            .WithName("PurgarLoteDaDemonstracao")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return v1;
    }

    private static async Task<Ok<IReadOnlyList<LoteDaDemonstracaoResponse>>> LotesAsync(
        ClaimsPrincipal principal, PurgaDaDemonstracaoHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyList<LoteDaDemonstracaoResponse>>([.. (await handler.LotesAsync(PostEndpoints.UsuarioId(principal), cancellationToken))
            .Select(lote => new LoteDaDemonstracaoResponse(lote.Lote, lote.Alunos, lote.PrimeiroEm, lote.UltimoEm, lote.Vigente))]);

    private static async Task<Ok<PurgaResponse>> PurgarAsync(
        [StringLength(64)] string lote,
        PurgaRequest pedido,
        ClaimsPrincipal principal,
        PurgaDaDemonstracaoHandler handler,
        CancellationToken cancellationToken)
    {
        var resultado = await handler.PurgarAsync(PostEndpoints.UsuarioId(principal), lote, pedido.Confirmacao, cancellationToken);
        return TypedResults.Ok(new PurgaResponse(resultado.Lote, resultado.Alunos, resultado.Fotos));
    }

    private static async Task<Created<PremioConcedidoResponse>> ConcederAsync(
        Guid cadastroId,
        ConcessaoRequest pedido,
        ClaimsPrincipal principal,
        ConcessaoDiretaHandler handler,
        CancellationToken cancellationToken)
    {
        var premio = await handler.ConcederAsync(
            PostEndpoints.UsuarioId(principal),
            cadastroId,
            new PedidoDeConcessao(
                pedido.Item,
                pedido.EventoId,
                pedido.EventoDigitado is { } digitado ? new EventoDigitado(digitado.Nome, digitado.Inicio, digitado.Fim) : null,
                pedido.MateriaId),
            cancellationToken);
        return TypedResults.Created(
            (string?)null,
            new PremioConcedidoResponse(
                premio.Id, premio.AlunoId, premio.ItemCodigo, ProfessorEndpoints.Nome(premio.Tier), premio.Pontos, premio.AprovadoEm!.Value));
    }

    // item: o código do prêmio de participação; com ele, só os eventos que o aceitam.
    private static async Task<Ok<IReadOnlyList<EventoResponse>>> EventosAsync(
        Guid cadastroId,
        ClaimsPrincipal principal,
        ConcessaoDiretaHandler handler,
        CancellationToken cancellationToken,
        [StringLength(16)] string? item = null) =>
        TypedResults.Ok<IReadOnlyList<EventoResponse>>([.. (await handler.EventosAsync(PostEndpoints.UsuarioId(principal), cadastroId, item, cancellationToken))
            .Select(evento => new EventoResponse(evento.Id, evento.Nome, evento.Inicio, evento.Fim, evento.ItensAceitos))]);

    private static async Task<Ok<IReadOnlyList<MateriaResponse>>> MateriasAsync(
        Guid cadastroId, ClaimsPrincipal principal, ConcessaoDiretaHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyList<MateriaResponse>>([.. (await handler.MateriasAsync(PostEndpoints.UsuarioId(principal), cadastroId, cancellationToken))
            .Select(materia => new MateriaResponse(materia.Id, materia.Nome))]);

    private static async Task<NoContent> AnularAsync(
        Guid medalhaId, ClaimsPrincipal principal, AnularMedalhaHandler handler, CancellationToken cancellationToken)
    {
        await handler.AnularPelaAreaAsync(PostEndpoints.UsuarioId(principal), medalhaId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<AlunoDaBaseResponse>>> BuscarAsync(
        ClaimsPrincipal principal,
        AtosSobreOAlunoHandler handler,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken,
        [StringLength(200)] string? busca = null) =>
        TypedResults.Ok<IReadOnlyList<AlunoDaBaseResponse>>([.. (await handler.BuscarAsync(PostEndpoints.UsuarioId(principal), busca, cancellationToken))
            .Select(aluno => AlunoDaBaseResponse.De(aluno, armazenamento))]);

    private static async Task<Ok<FichaDoAlunoResponse>> FichaAsync(
        Guid cadastroId,
        ClaimsPrincipal principal,
        AtosSobreOAlunoHandler handler,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var ficha = await handler.FichaAsync(PostEndpoints.UsuarioId(principal), cadastroId, cancellationToken);
        return TypedResults.Ok(new FichaDoAlunoResponse(
            AlunoDaBaseResponse.De(ficha.Aluno, armazenamento),
            [.. ficha.Medalhas.Select(medalha => new MedalhaDaFichaResponse(
                medalha.Id,
                medalha.Item,
                medalha.NomeDoItem,
                ProfessorEndpoints.Nome(medalha.Tier),
                medalha.Pontos,
                medalha.Materia,
                medalha.Ano,
                medalha.Bimestre,
                medalha.ConcedidaPor,
                medalha.ConcedidaEm))]));
    }

    private static async Task<NoContent> CorrigirEmailAsync(
        Guid cadastroId, EmailRequest pedido, ClaimsPrincipal principal, AtosSobreOAlunoHandler handler, CancellationToken cancellationToken)
    {
        await handler.CorrigirEmailAsync(PostEndpoints.UsuarioId(principal), cadastroId, pedido.Email, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> MarcarExAlunoAsync(
        Guid cadastroId, ClaimsPrincipal principal, AtosSobreOAlunoHandler handler, CancellationToken cancellationToken)
    {
        await handler.MarcarExAlunoAsync(PostEndpoints.UsuarioId(principal), cadastroId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> TrocarDeCursoAsync(
        Guid cadastroId, TrocaDeCursoRequest pedido, ClaimsPrincipal principal, AtosSobreOAlunoHandler handler, CancellationToken cancellationToken)
    {
        await handler.TrocarDeCursoAsync(PostEndpoints.UsuarioId(principal), cadastroId, pedido.CursoId, pedido.Semestre, cancellationToken);
        return TypedResults.NoContent();
    }
}

// A forma do e-mail é do domínio; aqui, só um teto bruto para o corpo.
public sealed record EmailRequest([property: StringLength(320)] string? Email);

public sealed record TrocaDeCursoRequest(Guid CursoId, int Semestre);

// O nome do lote digitado de novo, igual: a purga não volta atrás.
public sealed record PurgaRequest([property: StringLength(64)] string? Confirmacao);

// Vigente: o lote que a configuração grava agora em cada aluno de demonstração novo.
public sealed record LoteDaDemonstracaoResponse(string Lote, int Alunos, DateTimeOffset PrimeiroEm, DateTimeOffset UltimoEm, bool Vigente);

// Fotos: os objetos do bucket (cada derivada conta uma) que foram para a fila de remoção.
public sealed record PurgaResponse(string Lote, int Alunos, int Fotos);

// O evento é o id de um semeado ou o digitado, nunca os dois; a matéria, só na apresentação
// em sala. Sem foto: a concessão direta não tem comprovação.
public sealed record ConcessaoRequest(
    [property: Required, StringLength(16)] string Item,
    Guid? EventoId,
    EventoDigitadoRequest? EventoDigitado,
    Guid? MateriaId);

// Tier em camelCase, como no catálogo. ConcedidoEm: o instante da aprovação, que é o do
// desempate no ranking.
public sealed record PremioConcedidoResponse(Guid Id, Guid AlunoId, string Item, string Tier, int Pontos, DateTimeOffset ConcedidoEm);

// Conta nula: está na base e ainda não se cadastrou. Demonstracao: linha criada pela
// demonstração, não pela faculdade.
public sealed record AlunoDaBaseResponse(
    Guid Id,
    string NomeCompleto,
    string Ra,
    string Email,
    Guid CursoId,
    string Curso,
    int Semestre,
    bool Demonstracao,
    ContaDoAlunoResponse? Conta)
{
    public static AlunoDaBaseResponse De(AlunoDaBase aluno, IArmazenamentoDeFotos armazenamento)
    {
        ArgumentNullException.ThrowIfNull(aluno);
        return new(
            aluno.Id,
            aluno.NomeCompleto,
            aluno.Ra,
            aluno.Email,
            aluno.CursoId,
            aluno.Curso,
            aluno.Semestre,
            aluno.Demonstracao,
            aluno.Conta is { } conta
                ? new ContaDoAlunoResponse(
                    conta.Id, conta.NomeDeExibicao, conta.Arroba, conta.ExAlunoDesde, PostResponse.Leitura(armazenamento, conta.ChaveDaFoto))
                : null);
    }
}

// ExAlunoDesde nulo: aluno ativo. Foto: a derivada da grade, para o avatar.
public sealed record ContaDoAlunoResponse(Guid Id, string NomeDeExibicao, string Arroba, DateTimeOffset? ExAlunoDesde, string? Foto);

public sealed record FichaDoAlunoResponse(AlunoDaBaseResponse Aluno, IReadOnlyList<MedalhaDaFichaResponse> Medalhas);

// Tier em camelCase, como no catálogo. Bimestre de 1 a 4 no ano.
public sealed record MedalhaDaFichaResponse(
    Guid Id,
    string Item,
    string NomeDoItem,
    string Tier,
    int Pontos,
    string Materia,
    int Ano,
    int Bimestre,
    string ConcedidaPor,
    DateTimeOffset ConcedidaEm);
