namespace Maxx.Application.Posts;

// Os códigos de recusa do post, como o cliente os recebe em ProblemDetails.
public static class CodigosDoPost
{
    // Sem prêmio, sem foto e sem legenda (produto.md 6.1).
    public const string PostVazio = "post-vazio";

    public const string LegendaLonga = "legenda-longa";

    // Só aluno ativo publica: ex-aluno, professor e instituição, não.
    public const string PostNaoPermitido = "post-nao-permitido";

    // O upload não existe, é de outra pessoa, não é de post ou já foi limpo pela fila.
    public const string UploadNaoEncontrado = "upload-nao-encontrado";

    // A foto já virou outro post.
    public const string UploadJaUsado = "upload-ja-usado";

    // O upload existe, mas as duas derivadas não chegaram ao bucket.
    public const string FotoNaoEnviada = "foto-nao-enviada";

    // O prêmio não existe ou é de outra pessoa.
    public const string PremioNaoEncontrado = "premio-nao-encontrado";

    public const string PremioAnulado = "premio-anulado";

    // Um prêmio rende um post.
    public const string PremioJaPostado = "premio-ja-postado";

    public const string PostNaoEncontrado = "post-nao-encontrado";

    // Nem autor, nem instituição.
    public const string RemocaoNaoPermitida = "remocao-nao-permitida";
}
