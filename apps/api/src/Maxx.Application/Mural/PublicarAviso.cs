using Maxx.Application.Autenticacao;
using Maxx.Application.Autorizacao;
using Maxx.Application.Fila;
using Maxx.Application.Fotos;
using Maxx.Domain.Fotos;
using Maxx.Domain.Mural;

namespace Maxx.Application.Mural;

public interface IAvisoRepository
{
    void Adicionar(Aviso aviso);

    // Quantos dos cursos pedidos existem: o aviso de curso não aponta para curso inexistente.
    Task<int> CursosExistentesAsync(IReadOnlyCollection<Guid> cursoIds, CancellationToken cancellationToken);
}

// Publicação de aviso (produto.md 7). O papel, a forma e os alvos são do domínio; aqui, o
// escopo de turma do professor no banco, a existência dos cursos, a foto no bucket e a
// gravação. Na mesma transação, o trabalho que grava a notificação de aviso novo de cada
// destinatário (D4 do passo 10): sem aviso gravado, nenhuma notificação; com aviso, todas.
public sealed class PublicarAvisoHandler(
    IUsuarioRepository usuarios,
    IUploadPendenteRepository uploads,
    IAvisoRepository avisos,
    IEscopoDeTurma escopo,
    IArmazenamentoDeFotos armazenamento,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<Aviso> PublicarAsync(
        Guid autorId,
        EscopoDoAviso escopoDoAviso,
        IReadOnlyCollection<Guid> alvos,
        string? titulo,
        string? texto,
        Guid? uploadId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alvos);

        var autor = await usuarios.BuscarAsync(autorId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {autorId} não existe.");

        // O papel antes do escopo: o aluno que pede turma ouve que não publica, não que a
        // turma não é dele.
        if (!Aviso.PodePublicar(autor, escopoDoAviso))
        {
            throw Recusa(RecusaDoAviso.AutorNaoPublica, autorId);
        }

        // O escopo de turma do professor é do banco, nunca do token (CLAUDE.md, Segurança):
        // uma turma de fora recusa o aviso inteiro.
        if (escopoDoAviso == EscopoDoAviso.Turma)
        {
            foreach (var turmaId in alvos.Distinct())
            {
                if (!await escopo.ProfessorLecionaAsync(autorId, turmaId, cancellationToken))
                {
                    throw new RecusaException(
                        TipoDeRecusa.Proibido, CodigosDoMural.ForaDoEscopo, $"Professor {autorId} fora da turma {turmaId}.");
                }
            }
        }

        if (escopoDoAviso == EscopoDoAviso.Curso)
        {
            var distintos = alvos.Distinct().ToList();
            if (await avisos.CursosExistentesAsync(distintos, cancellationToken) != distintos.Count)
            {
                throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDoMural.CursoNaoEncontrado, "Curso inexistente no aviso.");
            }
        }

        UploadPendente? upload = null;
        if (uploadId is { } idDoUpload)
        {
            upload = await uploads.BuscarAsync(idDoUpload, cancellationToken)
                ?? throw Recusa(RecusaDoAviso.UploadNaoEncontrado, autorId);
        }

        var resultado = Aviso.Publicar(autor, escopoDoAviso, alvos, titulo, texto, upload, relogio.GetUtcNow());
        if (resultado.Aviso is not { } aviso)
        {
            throw Recusa(resultado.Recusa!.Value, autorId);
        }

        // O app diz que enviou; o bucket confirma, como no post.
        if (upload is not null)
        {
            foreach (var chave in upload.Chaves)
            {
                if (!await armazenamento.ExisteAsync(chave, cancellationToken))
                {
                    throw new RecusaException(TipoDeRecusa.Conflito, CodigosDoMural.FotoNaoEnviada, $"Objeto {chave} não chegou ao bucket.");
                }
            }
        }

        avisos.Adicionar(aviso);
        fila.Enfileirar(new NotificarAvisoNovo(aviso.Id));
        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outro aviso ou post confirmou o mesmo upload entre a leitura e a gravação.
            throw new RecusaException(TipoDeRecusa.Conflito, CodigosDoMural.UploadJaUsado, $"Upload {uploadId} usado ao mesmo tempo.");
        }

        return aviso;
    }

    private static RecusaException Recusa(RecusaDoAviso recusa, Guid autorId) => recusa switch
    {
        RecusaDoAviso.AutorNaoPublica => new(
            TipoDeRecusa.Proibido, CodigosDoMural.AvisoNaoPermitido, $"{autorId} não publica aviso nesse escopo."),
        RecusaDoAviso.AlvosInvalidos => new(TipoDeRecusa.Invalido, CodigosDoMural.AlvosDoAvisoInvalidos, "Alvos fora do escopo."),
        RecusaDoAviso.TituloInvalido => new(TipoDeRecusa.Invalido, CodigosDoMural.TituloDoAvisoInvalido, "Título fora do tamanho."),
        RecusaDoAviso.TextoInvalido => new(TipoDeRecusa.Invalido, CodigosDoMural.TextoDoAvisoInvalido, "Texto fora do tamanho."),
        RecusaDoAviso.UploadNaoEncontrado => new(
            TipoDeRecusa.NaoEncontrado, CodigosDoMural.UploadNaoEncontrado, $"Upload inexistente ou alheio para {autorId}."),
        RecusaDoAviso.UploadJaUsado => new(TipoDeRecusa.Conflito, CodigosDoMural.UploadJaUsado, "Upload já confirmado."),
        _ => throw new ArgumentOutOfRangeException(nameof(recusa), recusa, null),
    };
}

// Grava a notificação de aviso novo de cada destinatário (produto.md 7 e 8).
public sealed record NotificarAvisoNovo(Guid AvisoId) : ICargaDeTrabalho
{
    public static string Tipo => "notificar-aviso-novo";
}

// A distribuição em lote, de uma vez no banco, para um aviso de faculdade não passar dos
// 60 s do trabalho (D4 do passo 10).
public interface IDistribuicaoDeAviso
{
    // Grava uma notificação de aviso novo para cada destinatário que ainda não a tem e
    // devolve quantas gravou. Os destinatários são fixados no instante da publicação:
    // quem ganhou conta, virou ex-aluno ou trocou de vínculo depois não muda o conjunto.
    //
    // - Aluno ativo naquele instante, alcançado pelo escopo: todos na faculdade, o curso
    //   atual no curso, a matrícula na turma.
    // - Professor, só no aviso de faculdade.
    // - Nunca o autor. Ex-aluno lê o mural, mas não é notificado.
    Task<int> NotificarAsync(Guid avisoId, DateTimeOffset agora, CancellationToken cancellationToken);
}

// No worker. Idempotente: quem já tem a notificação não ganha outra, e rodar de novo
// depois de uma queda não muda nada.
public sealed class NotificarAvisoNovoHandler(IDistribuicaoDeAviso distribuicao, TimeProvider relogio)
    : ITrabalhoHandler<NotificarAvisoNovo>
{
    public async Task ExecutarAsync(NotificarAvisoNovo carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);
        await distribuicao.NotificarAsync(carga.AvisoId, relogio.GetUtcNow(), cancellationToken);
    }
}
