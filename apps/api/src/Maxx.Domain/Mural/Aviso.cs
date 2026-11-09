using Maxx.Domain.Academico;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Mural;

// O aviso do mural (produto.md 7): título, texto e uma foto opcional, somente leitura. Sem
// comentário, sem fixação, sem expiração, sem edição e sem remoção na fase 1. Três escopos:
// a instituição publica para a faculdade ou para um ou mais cursos; o professor, para uma
// ou mais turmas suas, na mesma publicação. Quem publicou e quando ficam no próprio aviso.
//
// Se a turma é do professor é pergunta ao banco (IEscopoDeTurma), feita antes por quem
// chama, como na concessão de medalha: o domínio confere o papel, a forma e os alvos.
public sealed class Aviso
{
    // Em caracteres, contando um emoji como um, como a legenda do post.
    public const int TamanhoMinimoDoTitulo = 3;
    public const int TamanhoMaximoDoTitulo = 120;
    public const int TamanhoMaximoDoTexto = 2000;

    private readonly List<AvisoTurma> _turmas = [];
    private readonly List<AvisoCurso> _cursos = [];

    // Para o EF Core materializar do banco.
    private Aviso()
    {
        Titulo = null!;
        Texto = null!;
    }

    private Aviso(Guid autorId, EscopoDoAviso escopo, string titulo, string texto, UploadPendente? upload, DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        AutorId = autorId;
        Escopo = escopo;
        Titulo = titulo;
        Texto = texto;
        ChaveDaFotoDoFeed = upload?.ChaveDoFeed;
        ChaveDaFotoDaGrade = upload?.ChaveDaGrade;
        PublicadoEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid AutorId { get; private set; }

    public EscopoDoAviso Escopo { get; private set; }

    public string Titulo { get; private set; }

    public string Texto { get; private set; }

    public string? ChaveDaFotoDoFeed { get; private set; }

    public string? ChaveDaFotoDaGrade { get; private set; }

    public DateTimeOffset PublicadoEm { get; private set; }

    // As turmas, no escopo turma; vazio nos outros.
    public IReadOnlyCollection<AvisoTurma> Turmas => _turmas.AsReadOnly();

    // Os cursos, no escopo curso; vazio nos outros.
    public IReadOnlyCollection<AvisoCurso> Cursos => _cursos.AsReadOnly();

    public bool TemFoto => ChaveDaFotoDoFeed is not null;

    // Recusa em vez de exceção: a borda responde com o código de cada caso. Os alvos são as
    // turmas (escopo turma) ou os cursos (escopo curso), já conferidos por quem chama; na
    // faculdade, nenhum. Repetidos contam uma vez. Publicado, o upload fica confirmado e sai
    // do alcance da limpeza.
    public static ResultadoDoAviso Publicar(
        Usuario autor,
        EscopoDoAviso escopo,
        IReadOnlyCollection<Guid> alvos,
        string? titulo,
        string? texto,
        UploadPendente? upload,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(autor);
        ArgumentNullException.ThrowIfNull(alvos);

        if (!PodePublicar(autor, escopo))
        {
            return Recusa(RecusaDoAviso.AutorNaoPublica);
        }

        var distintos = alvos.Distinct().ToList();
        if (escopo == EscopoDoAviso.Faculdade ? distintos.Count > 0 : distintos.Count == 0)
        {
            return Recusa(RecusaDoAviso.AlvosInvalidos);
        }

        titulo = titulo?.Trim() ?? string.Empty;
        var tamanhoDoTitulo = titulo.EnumerateRunes().Count();
        if (tamanhoDoTitulo is < TamanhoMinimoDoTitulo or > TamanhoMaximoDoTitulo)
        {
            return Recusa(RecusaDoAviso.TituloInvalido);
        }

        texto = texto?.Trim() ?? string.Empty;
        var tamanhoDoTexto = texto.EnumerateRunes().Count();
        if (tamanhoDoTexto is < 1 or > TamanhoMaximoDoTexto)
        {
            return Recusa(RecusaDoAviso.TextoInvalido);
        }

        if (upload is not null)
        {
            if (upload.UsuarioId != autor.Id || upload.Finalidade != FinalidadeDoUpload.Aviso)
            {
                return Recusa(RecusaDoAviso.UploadNaoEncontrado);
            }

            if (upload.EstaConfirmado)
            {
                return Recusa(RecusaDoAviso.UploadJaUsado);
            }
        }

        upload?.Confirmar(agora);
        var aviso = new Aviso(autor.Id, escopo, titulo, texto, upload, agora);
        switch (escopo)
        {
            case EscopoDoAviso.Turma:
                aviso._turmas.AddRange(distintos.Select(turma => new AvisoTurma(aviso.Id, turma)));
                break;
            case EscopoDoAviso.Curso:
                aviso._cursos.AddRange(distintos.Select(curso => new AvisoCurso(aviso.Id, curso)));
                break;
        }

        return new(aviso, null);
    }

    // A instituição publica para a faculdade e para curso; o professor, para turma
    // (produto.md 2.1). A conta de professor e instituição publica nos três. Aluno, nunca.
    public static bool PodePublicar(Usuario autor, EscopoDoAviso escopo)
    {
        ArgumentNullException.ThrowIfNull(autor);
        return escopo switch
        {
            EscopoDoAviso.Faculdade or EscopoDoAviso.Curso => autor.Papeis.HasFlag(Papeis.Instituicao),
            EscopoDoAviso.Turma => autor.Papeis.HasFlag(Papeis.Professor),
            _ => false,
        };
    }

    private static ResultadoDoAviso Recusa(RecusaDoAviso recusa) => new(null, recusa);
}

// Uma turma do aviso de escopo turma.
public sealed class AvisoTurma
{
    // Para o EF Core materializar do banco.
    private AvisoTurma()
    {
    }

    internal AvisoTurma(Guid avisoId, Guid turmaId)
    {
        AvisoId = avisoId;
        TurmaId = turmaId;
    }

    public Guid AvisoId { get; private set; }

    public Guid TurmaId { get; private set; }
}

// Um curso do aviso de escopo curso.
public sealed class AvisoCurso
{
    // Para o EF Core materializar do banco.
    private AvisoCurso()
    {
    }

    internal AvisoCurso(Guid avisoId, Guid cursoId)
    {
        AvisoId = avisoId;
        CursoId = cursoId;
    }

    public Guid AvisoId { get; private set; }

    public Guid CursoId { get; private set; }
}

public enum EscopoDoAviso
{
    Faculdade,
    Curso,
    Turma,
}

public enum RecusaDoAviso
{
    // O papel não publica nesse escopo: aluno, turma sem o papel de professor, ou
    // faculdade e curso sem o papel de instituição.
    AutorNaoPublica,

    // Faculdade com alvo, ou curso e turma sem nenhum.
    AlvosInvalidos,

    TituloInvalido,
    TextoInvalido,
    UploadNaoEncontrado,
    UploadJaUsado,
}

public sealed record ResultadoDoAviso(Aviso? Aviso, RecusaDoAviso? Recusa);
