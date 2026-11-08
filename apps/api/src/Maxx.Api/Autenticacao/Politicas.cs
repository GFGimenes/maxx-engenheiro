using Microsoft.AspNetCore.Authorization;

namespace Maxx.Api.Autenticacao;

// As policies por papel. O papel vem do token, que o tirou do cadastro elegível; o escopo
// de turma do professor nunca: esse é conferido no banco (IEscopoDeTurma).
internal static class Politicas
{
    public const string Aluno = "aluno";

    public const string Professor = "professor";

    public const string Instituicao = "instituicao";

    // Quem decide comprovação: professor ou instituição. O escopo do professor é do banco.
    public const string Aprovador = "aprovador";

    public static void Configurar(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(Aluno, policy => policy.RequireAuthenticatedUser().RequireRole(Aluno));
        options.AddPolicy(Professor, policy => policy.RequireAuthenticatedUser().RequireRole(Professor));
        options.AddPolicy(Instituicao, policy => policy.RequireAuthenticatedUser().RequireRole(Instituicao));
        options.AddPolicy(Aprovador, policy => policy.RequireAuthenticatedUser().RequireRole(Professor, Instituicao));
    }
}
