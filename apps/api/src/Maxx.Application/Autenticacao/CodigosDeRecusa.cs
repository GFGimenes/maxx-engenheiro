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

    // Usuário inexistente, conta ainda sem senha, senha errada, professor por RA: a mesma
    // resposta, para não dizer quem tem conta (ADR 0006).
    public const string CredenciaisInvalidas = "credenciais-invalidas";

    // Refresh ausente, desconhecido, expirado, de família revogada ou reusado: em todos os
    // casos o cliente volta ao login.
    public const string SessaoInvalida = "sessao-invalida";

    // Token de recuperação desconhecido, expirado, já usado ou invalidado por outro pedido
    // concluído: em todos os casos o cliente oferece pedir um link novo.
    public const string RecuperacaoInvalida = "recuperacao-invalida";

    // Demonstração: curso que não existe ou sem turma no semestre vigente.
    public const string CursoDeDemonstracaoIndisponivel = "curso-indisponivel";

    // Demonstração: muita gente com o mesmo nome entrando junto, e as tentativas acabaram.
    // O cliente oferece tentar de novo.
    public const string RegistroSimultaneo = "registro-simultaneo";

    // Demonstração: a chave guardada no dispositivo não abre conta nenhuma.
    public const string ChaveDoDispositivoInvalida = "chave-invalida";
}
