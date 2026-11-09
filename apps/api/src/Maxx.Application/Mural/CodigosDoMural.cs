namespace Maxx.Application.Mural;

// Os códigos de recusa do mural, como o cliente os recebe em ProblemDetails. Os que
// repetem o texto da medalha e do post (escopo, upload, foto) têm a mesma mensagem no app.
public static class CodigosDoMural
{
    // O papel não publica nesse escopo: aluno, ou faculdade e curso sem o papel de
    // instituição, ou turma sem o papel de professor (produto.md 2.1).
    public const string AvisoNaoPermitido = "aviso-nao-permitido";

    // Uma das turmas não é do professor, ou não é do semestre vigente.
    public const string ForaDoEscopo = "fora-do-escopo";

    // Faculdade com turma ou curso, ou curso e turma sem nenhum.
    public const string AlvosDoAvisoInvalidos = "alvos-do-aviso-invalidos";

    // Um dos cursos não existe.
    public const string CursoNaoEncontrado = "curso-nao-encontrado";

    // De 3 a 120 caracteres (produto.md 7).
    public const string TituloDoAvisoInvalido = "titulo-do-aviso-invalido";

    // De 1 a 2.000 caracteres (produto.md 7).
    public const string TextoDoAvisoInvalido = "texto-do-aviso-invalido";

    // O upload não existe, é de outra pessoa, não é de aviso ou já foi limpo pela fila.
    public const string UploadNaoEncontrado = "upload-nao-encontrado";

    // A foto já virou outro aviso.
    public const string UploadJaUsado = "upload-ja-usado";

    // O upload existe, mas as duas derivadas não chegaram ao bucket.
    public const string FotoNaoEnviada = "foto-nao-enviada";
}
