using Maxx.Application.Autenticacao;
using Maxx.Application.Demonstracao;

namespace Maxx.Application.Instituicao;

// A purga dos dados de demonstração por lote (produto.md 11, decisões do passo 7, D14): os
// dados do lote saem do banco numa transação só e, na mesma transação, as fotos do lote
// (posts, fotos de perfil e uploads pendentes) vão para a fila de remoção do bucket, que o SQL
// sozinho não alcança. Vale também para o lote vigente (P6), sempre com o nome do lote
// digitado de novo, e com a flag da demonstração desligada: a purga é depois da avaliação.
public sealed class PurgaDaDemonstracaoHandler(
    IUsuarioRepository usuarios,
    IPurgaDaDemonstracao purga,
    ConfiguracaoDaDemonstracao configuracao,
    TimeProvider relogio)
{
    public async Task<IReadOnlyList<LoteDaDemonstracao>> LotesAsync(Guid quemId, CancellationToken cancellationToken)
    {
        await AtosSobreOAlunoHandler.InstituicaoAsync(usuarios, quemId, cancellationToken);
        return [.. (await purga.LotesAsync(cancellationToken))
            .Select(lote => lote with { Vigente = !string.IsNullOrEmpty(configuracao.Lote) && lote.Lote == configuracao.Lote })];
    }

    public async Task<ResultadoDaPurga> PurgarAsync(Guid quemId, string lote, string? confirmacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lote);
        var instituicao = await AtosSobreOAlunoHandler.InstituicaoAsync(usuarios, quemId, cancellationToken);

        if (!string.Equals(confirmacao?.Trim(), lote, StringComparison.Ordinal))
        {
            throw new RecusaException(TipoDeRecusa.Invalido, CodigosDaInstituicao.ConfirmacaoDaPurgaInvalida, $"Confirmação diferente do lote {lote}.");
        }

        return await purga.PurgarAsync(lote, instituicao.Id, relogio.GetUtcNow(), cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDaInstituicao.LoteNaoEncontrado, $"Lote {lote} sem aluno.");
    }
}

public interface IPurgaDaDemonstracao
{
    // Os lotes com aluno de demonstração no banco, do mais recente ao mais antigo.
    Task<IReadOnlyList<LoteDaDemonstracao>> LotesAsync(CancellationToken cancellationToken);

    // Numa transação: apaga o lote, grava o registro da purga e enfileira a remoção das
    // fotos e os recálculos de ranking e raridade. Nulo se o lote não tem aluno.
    Task<ResultadoDaPurga?> PurgarAsync(string lote, Guid purgadoPorId, DateTimeOffset agora, CancellationToken cancellationToken);
}

// Vigente: o lote que a configuração grava agora em cada aluno de demonstração novo.
public sealed record LoteDaDemonstracao(string Lote, int Alunos, DateTimeOffset PrimeiroEm, DateTimeOffset UltimoEm, bool Vigente = false);

public sealed record ResultadoDaPurga(string Lote, int Alunos, int Fotos);
