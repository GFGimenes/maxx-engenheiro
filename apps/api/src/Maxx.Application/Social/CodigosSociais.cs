namespace Maxx.Application.Social;

// Os códigos de recusa de curtida, comentário e seguir, como o cliente os recebe em
// ProblemDetails.
public static class CodigosSociais
{
    // A instituição não curte nem comenta (produto.md 2.1).
    public const string InteracaoNaoPermitida = "interacao-nao-permitida";

    public const string ComentarioVazio = "comentario-vazio";

    public const string ComentarioLongo = "comentario-longo";

    public const string ComentarioNaoEncontrado = "comentario-nao-encontrado";

    // Nem autor do comentário, nem instituição; o dono do post também não.
    public const string RemocaoDeComentarioNaoPermitida = "remocao-de-comentario-nao-permitida";

    public const string UsuarioNaoEncontrado = "usuario-nao-encontrado";

    // Seguir professor, a instituição ou a si mesmo; instituição seguindo; professor
    // seguindo quem não é aluno dele no semestre vigente.
    public const string SeguirNaoPermitido = "seguir-nao-permitido";
}
