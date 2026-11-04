namespace Maxx.Application.Autenticacao;

// Os códigos de recusa da autenticação, como o cliente os recebe em ProblemDetails.
public static class CodigosDeRecusa
{
    public const string CadastroNaoEncontrado = "cadastro-nao-encontrado";

    // O RA já tem conta: o cliente leva à recuperação de senha (produto.md 2.5).
    public const string ContaExistente = "conta-existente";

    public const string PrefixoInvalido = "prefixo-invalido";

    public const string PrefixoIndisponivel = "prefixo-indisponivel";

    public const string SenhaForaDaPolitica = "senha-fora-da-politica";

    public const string NomeDeExibicaoInvalido = "nome-de-exibicao-invalido";
}
