namespace Maxx.Application.Perfil;

// Os códigos de recusa do perfil e da busca, como o cliente os recebe em ProblemDetails.
// Prefixo e nome de exibição usam os do cadastro (CodigosDeRecusa); o upload, os do post.
public static class CodigosDoPerfil
{
    // Busca sem termo de ao menos 2 caracteres e sem filtro (decisão D11).
    public const string BuscaSemCriterio = "busca-sem-criterio";

    // Pedido que troca a foto e a remove ao mesmo tempo.
    public const string FotoDePerfilAmbigua = "foto-de-perfil-ambigua";
}
