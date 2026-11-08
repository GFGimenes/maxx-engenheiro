namespace Maxx.Application.Fotos;

// Os códigos de recusa do envio de foto, como o cliente os recebe em ProblemDetails.
public static class CodigosDaFoto
{
    // Quem pede não pode enviar foto para aquilo: post de quem não é aluno ativo, foto de
    // perfil da instituição.
    public const string EnvioNaoPermitido = "envio-nao-permitido";

    // O app reencoda tudo em JPEG; outro tipo é cliente desatualizado ou adulterado.
    public const string TipoDeFotoNaoAceito = "tipo-de-foto-nao-aceito";

    // Uma das derivadas vazia ou acima do limite dela.
    public const string TamanhoDeFotoInvalido = "tamanho-de-foto-invalido";
}
