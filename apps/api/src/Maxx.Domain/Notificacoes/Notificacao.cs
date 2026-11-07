using Maxx.Domain.Premios;

namespace Maxx.Domain.Notificacoes;

// Registro no banco, mostrado no sininho do feed a partir do passo 10 (produto.md 8). No
// passo 6 nasce o primeiro tipo, a medalha recebida. A referência aponta para o que gerou
// a notificação (a medalha, aqui); o texto é do cliente, a partir do tipo.
public sealed class Notificacao
{
    // Para o EF Core materializar do banco.
    private Notificacao()
    {
    }

    private Notificacao(Guid usuarioId, TipoDeNotificacao tipo, Guid referenciaId, DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        UsuarioId = usuarioId;
        Tipo = tipo;
        ReferenciaId = referenciaId;
        CriadaEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public TipoDeNotificacao Tipo { get; private set; }

    public Guid ReferenciaId { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public DateTimeOffset? LidaEm { get; private set; }

    // Só medalha ativa notifica: a anulada antes de o trabalho rodar não chega ao aluno.
    public static Notificacao MedalhaRecebida(Medalha medalha, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(medalha);
        if (!medalha.EstaAtiva)
        {
            throw new DomainException($"A medalha {medalha.Id} está anulada e não notifica.");
        }

        return new Notificacao(medalha.AlunoId, TipoDeNotificacao.MedalhaRecebida, medalha.Id, agora);
    }
}

public enum TipoDeNotificacao
{
    MedalhaRecebida,
}
