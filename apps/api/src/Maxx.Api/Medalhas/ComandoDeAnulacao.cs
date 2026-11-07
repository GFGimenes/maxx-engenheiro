using Maxx.Application;
using Maxx.Application.Medalhas;

namespace Maxx.Api.Medalhas;

// `Maxx.Api anular-medalha <id-da-medalha> --instituicao <e-mail>`: a anulação da fase 1,
// até a área web da instituição no passo 12. No compose:
//   docker compose run --rm seeder anular-medalha <id> --instituicao <e-mail>
// Sai com 0 quando a medalha fica anulada (ou já estava), 1 quando o pedido é recusado e 2
// quando o uso está errado.
internal static class ComandoDeAnulacao
{
    public const string Nome = "anular-medalha";

    public const string Uso = "Uso: Maxx.Api anular-medalha <id-da-medalha> --instituicao <e-mail>";

    // Quantos argumentos o comando consome; o que vier depois é configuração.
    public const int Argumentos = 4;

    public static async Task<int> ExecutarAsync(
        IServiceProvider servicos, string[] args, TextWriter saida, TextWriter erro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        ArgumentNullException.ThrowIfNull(saida);
        ArgumentNullException.ThrowIfNull(erro);

        if (args is not [Nome, var texto, "--instituicao", var email, ..] || !Guid.TryParse(texto, out var medalhaId)
            || string.IsNullOrWhiteSpace(email))
        {
            await erro.WriteLineAsync(Uso);
            return 2;
        }

        try
        {
            var anulada = await servicos.GetRequiredService<AnularMedalhaHandler>().AnularAsync(medalhaId, email, cancellationToken);
            await saida.WriteLineAsync(anulada ? $"Medalha {medalhaId} anulada." : $"Medalha {medalhaId} já estava anulada.");
            return 0;
        }
        catch (RecusaException recusa)
        {
            // Recusa é resposta esperada, não falha do programa: sem pilha.
            await erro.WriteLineAsync(recusa.Codigo switch
            {
                CodigosDaMedalha.MedalhaNaoEncontrada => $"Nenhuma medalha com o id {medalhaId}.",
                CodigosDaMedalha.SemContaDeInstituicao => $"{email.Trim()} não é uma conta da instituição.",
                _ => recusa.Message,
            });
            return 1;
        }
    }
}
