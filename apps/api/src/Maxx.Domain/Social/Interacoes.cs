using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Social;

// Quem curte e comenta (produto.md 2.1, 2.2 e 6.3): aluno, inclusive o ex-aluno, e
// professor. A instituição não participa do social: a voz dela é o mural, e no feed ela só
// remove o que é impróprio.
public static class Interacao
{
    public static bool PodeInteragir(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return usuario.Papeis.HasFlag(Papeis.Aluno) || usuario.Papeis.HasFlag(Papeis.Professor);
    }
}

// Uma curtida por pessoa e post, pela chave da tabela. Curtir de novo não muda nada.
public sealed class Curtida
{
    // Para o EF Core materializar do banco.
    private Curtida()
    {
    }

    private Curtida(Guid postId, Guid usuarioId, DateTimeOffset agora)
    {
        PostId = postId;
        UsuarioId = usuarioId;
        CriadaEm = agora;
    }

    public Guid PostId { get; private set; }

    public Guid UsuarioId { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    // Nulo quando quem pede não interage, ou quando o post já foi removido.
    public static Curtida? Curtir(Usuario usuario, Post post, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentNullException.ThrowIfNull(post);
        return Interacao.PodeInteragir(usuario) && !post.EstaRemovido ? new Curtida(post.Id, usuario.Id, agora) : null;
    }
}

// Comentário de post (produto.md 6.1). Apagam o autor dele e a instituição; o dono do post
// não apaga comentário de terceiro na fase 1. Como no post, o autor apaga de verdade e a
// instituição remove, gravando quem e quando.
public sealed class Comentario
{
    // Em caracteres, contando um emoji como um.
    public const int TamanhoMaximo = 500;

    // Para o EF Core materializar do banco.
    private Comentario()
    {
        Texto = null!;
    }

    private Comentario(Guid postId, Guid autorId, string texto, DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        PostId = postId;
        AutorId = autorId;
        Texto = texto;
        CriadoEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid PostId { get; private set; }

    public Guid AutorId { get; private set; }

    public string Texto { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? RemovidoEm { get; private set; }

    public Guid? RemovidoPorId { get; private set; }

    public bool EstaRemovido => RemovidoEm is not null;

    public static ResultadoDoComentario Escrever(Usuario autor, Post post, string? texto, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(autor);
        ArgumentNullException.ThrowIfNull(post);

        if (!Interacao.PodeInteragir(autor))
        {
            return new(null, RecusaDoComentario.NaoInterage);
        }

        if (post.EstaRemovido)
        {
            return new(null, RecusaDoComentario.PostRemovido);
        }

        texto = texto?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            return new(null, RecusaDoComentario.Vazio);
        }

        if (texto.EnumerateRunes().Count() > TamanhoMaximo)
        {
            return new(null, RecusaDoComentario.Longo);
        }

        return new(new Comentario(post.Id, autor.Id, texto, agora), null);
    }

    // O autor apaga; a instituição remove; ninguém mais, nem o dono do post.
    public ModoDeRemocao ModoDeRemocaoPor(Usuario quem)
    {
        ArgumentNullException.ThrowIfNull(quem);

        if (EstaRemovido)
        {
            return ModoDeRemocao.NaoPermitida;
        }

        if (quem.Id == AutorId)
        {
            return ModoDeRemocao.Apagar;
        }

        return quem.Papeis.HasFlag(Papeis.Instituicao) ? ModoDeRemocao.RemoverPelaInstituicao : ModoDeRemocao.NaoPermitida;
    }

    public void RemoverPelaInstituicao(Usuario instituicao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(instituicao);
        if (ModoDeRemocaoPor(instituicao) != ModoDeRemocao.RemoverPelaInstituicao)
        {
            throw new DomainException($"@{instituicao.Prefixo} não remove o comentário {Id} como instituição.");
        }

        RemovidoEm = agora;
        RemovidoPorId = instituicao.Id;
    }
}

public enum RecusaDoComentario
{
    NaoInterage,
    PostRemovido,
    Vazio,
    Longo,
}

public sealed record ResultadoDoComentario(Comentario? Comentario, RecusaDoComentario? Recusa);

// Seguir (produto.md 6.3): livre, sem aprovação. Aluno segue aluno, ativo ou ex-aluno.
// Professor segue os alunos dele, e o vínculo de turma é pergunta ao banco, feita antes por
// quem chama; o seguimento continua depois do semestre. Ninguém segue professor, ninguém
// segue a si mesmo, e a instituição não participa.
public sealed class Seguimento
{
    // Para o EF Core materializar do banco.
    private Seguimento()
    {
    }

    private Seguimento(Guid seguidorId, Guid seguidoId, DateTimeOffset agora)
    {
        SeguidorId = seguidorId;
        SeguidoId = seguidoId;
        CriadoEm = agora;
    }

    public Guid SeguidorId { get; private set; }

    public Guid SeguidoId { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public static Seguimento? Seguir(Usuario seguidor, Usuario seguido, bool professorDoSeguido, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(seguidor);
        ArgumentNullException.ThrowIfNull(seguido);

        if (seguidor.Id == seguido.Id || seguido.Papeis != Papeis.Aluno)
        {
            return null;
        }

        var pode = seguidor.Papeis == Papeis.Aluno || (seguidor.Papeis.HasFlag(Papeis.Professor) && professorDoSeguido);
        return pode ? new Seguimento(seguidor.Id, seguido.Id, agora) : null;
    }
}
