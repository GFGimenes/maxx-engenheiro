using Maxx.Application.Autenticacao;
using Maxx.Application.Fila;
using Maxx.Domain.Fotos;

namespace Maxx.Application.Fotos;

public interface IUploadPendenteRepository
{
    void Adicionar(UploadPendente upload);

    Task<UploadPendente?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    void Remover(UploadPendente upload);
}

// O pedido assinado: o registro e as duas URLs de PUT, uma por derivada.
public sealed record UploadAssinado(UploadPendente Upload, EnvioAssinado Feed, EnvioAssinado Grade);

// Pedido de envio (fluxos/02-post-com-comprovacao.md): valida quem pede, o tipo e o tamanho
// das duas derivadas, registra o upload pendente e devolve as URLs assinadas. A limpeza do
// que não for confirmado já sai enfileirada, adiada até o prazo, na mesma transação: não há
// upload pendente sem o trabalho que o apaga (ADR 0008).
public sealed class PedirUploadHandler(
    IUsuarioRepository usuarios,
    IUploadPendenteRepository uploads,
    IArmazenamentoDeFotos armazenamento,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<UploadAssinado> PedirAsync(
        Guid usuarioId,
        FinalidadeDoUpload finalidade,
        string? tipoDeConteudo,
        long tamanhoDoFeed,
        long tamanhoDaGrade,
        CancellationToken cancellationToken)
    {
        // Token válido de uma conta que já não existe (a purga da demonstração).
        var usuario = await usuarios.BuscarAsync(usuarioId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {usuarioId} não existe.");

        var resultado = UploadPendente.Pedir(usuario, finalidade, tipoDeConteudo, tamanhoDoFeed, tamanhoDaGrade, relogio.GetUtcNow());
        if (resultado.Upload is not { } upload)
        {
            throw Recusa(resultado.Recusa!.Value, usuarioId, finalidade);
        }

        // Assinar não vai à rede: se falhar, é configuração, e nada foi gravado.
        var feed = armazenamento.AssinarEnvio(upload.ChaveDoFeed, upload.TipoDeConteudo, upload.TamanhoDoFeed, upload.AssinaturaExpiraEm);
        var grade = armazenamento.AssinarEnvio(upload.ChaveDaGrade, upload.TipoDeConteudo, upload.TamanhoDaGrade, upload.AssinaturaExpiraEm);

        uploads.Adicionar(upload);
        fila.Enfileirar(new LimparUploadAbandonado(upload.Id), upload.LimparEm);
        await unitOfWork.SalvarAsync(cancellationToken);
        return new UploadAssinado(upload, feed, grade);
    }

    private static RecusaException Recusa(RecusaDoUpload recusa, Guid usuarioId, FinalidadeDoUpload finalidade) => recusa switch
    {
        RecusaDoUpload.FinalidadeNaoPermitida => new RecusaException(
            TipoDeRecusa.Proibido, CodigosDaFoto.EnvioNaoPermitido, $"Usuário {usuarioId} não envia foto para {finalidade}."),
        RecusaDoUpload.TipoNaoAceito => new RecusaException(
            TipoDeRecusa.Invalido, CodigosDaFoto.TipoDeFotoNaoAceito, "Tipo de foto fora do aceito."),
        RecusaDoUpload.TamanhoInvalido => new RecusaException(
            TipoDeRecusa.Invalido, CodigosDaFoto.TamanhoDeFotoInvalido, "Tamanho de foto fora da faixa."),
        _ => throw new ArgumentOutOfRangeException(nameof(recusa), recusa, null),
    };
}

// O upload pendente que ninguém confirmou até o prazo de limpeza sai do bucket e do banco.
public sealed record LimparUploadAbandonado(Guid UploadId) : ICargaDeTrabalho
{
    public static string Tipo => "limpar-upload-abandonado";
}

// No worker. A linha sai primeiro, e o DELETE, com o xmin, segura a linha até o trabalho
// terminar: uma confirmação que chegue agora espera e encontra a linha já apagada, em vez
// de confirmar um upload cujos objetos estão saindo. Depois saem os objetos. Se a remoção
// no bucket falha, o trabalho desfaz o DELETE e volta com recuo; se o trabalho roda de novo
// depois de remover, não acha nada e termina. Confirmado antes, não toca em nada.
public sealed class LimparUploadAbandonadoHandler(
    IUploadPendenteRepository uploads,
    IArmazenamentoDeFotos armazenamento,
    IUnitOfWork unitOfWork) : ITrabalhoHandler<LimparUploadAbandonado>
{
    public async Task ExecutarAsync(LimparUploadAbandonado carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);

        var upload = await uploads.BuscarAsync(carga.UploadId, cancellationToken);
        if (upload is null || upload.EstaConfirmado)
        {
            return;
        }

        uploads.Remover(upload);
        await unitOfWork.SalvarAsync(cancellationToken);
        await armazenamento.RemoverAsync(upload.Chaves, cancellationToken);
    }
}

// Fotos que já foram usadas e deixaram de ser: post apagado ou removido, foto de perfil
// trocada. As chaves não são segredo, só endereço.
public sealed record RemoverFotos(IReadOnlyList<string> Chaves) : ICargaDeTrabalho
{
    public static string Tipo => "remover-fotos";
}

// No worker. Remover chave que já não existe não é erro: o trabalho pode rodar de novo.
public sealed class RemoverFotosHandler(IArmazenamentoDeFotos armazenamento) : ITrabalhoHandler<RemoverFotos>
{
    public async Task ExecutarAsync(RemoverFotos carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);
        await armazenamento.RemoverAsync(carga.Chaves, cancellationToken);
    }
}
