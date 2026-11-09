using Maxx.Application.Autenticacao;
using Maxx.Application.Mural;
using Maxx.Application.Paginacao;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;

namespace Maxx.Application.Notificacoes;

// A notificação como o sininho mostra (produto.md 8). O texto é do app, pelo tipo; aqui vai
// só o que a frase e o toque precisam: o nome do item, o motivo da reprovação ou da
// revogação, a decisão da contestação, o post do prêmio e o título do aviso. Cada campo vem
// só nos tipos que o usam.
public sealed record NotificacaoLida(
    Guid Id,
    TipoDeNotificacao Tipo,
    Guid ReferenciaId,
    DateTimeOffset CriadaEm,
    bool Lida,
    string? Item = null,
    string? Motivo = null,
    DecisaoDaContestacao? Decisao = null,
    Guid? PostId = null,
    string? TituloDoAviso = null);

// O que acende as três bolinhas: o sininho, a aba Mural e a aba Troféus.
public sealed record Novidades(bool Sininho, bool Mural, bool Trofeus);

public interface IConsultaDeNotificacoes
{
    // As do usuário, da mais nova à mais antiga, por cursor (criada_em, id).
    Task<Pagina<NotificacaoLida>> PaginaAsync(
        Guid usuarioId, PosicaoNoTempo? depoisDe, int limite, CancellationToken cancellationToken);

    // Marca como lidas as do usuário criadas até o instante dado; as que chegaram depois,
    // que o sininho aberto ainda não mostrou, ficam acesas.
    Task MarcarLidasAsync(Guid usuarioId, DateTimeOffset ate, DateTimeOffset agora, CancellationToken cancellationToken);

    Task<bool> TemNaoLidaAsync(Guid usuarioId, CancellationToken cancellationToken);

    // Prêmio do aluno obtido depois do instante (nulo: desde sempre): medalha ativa, prêmio de
    // participação aprovado e ainda válido ou definitivo, conquista não removida.
    Task<bool> TemPremioNovoAsync(Guid alunoId, DateTimeOffset? depoisDe, CancellationToken cancellationToken);
}

// O sininho, as bolinhas e o marcador de Troféus (produto.md 8; passo 10, perguntas 5 e 6).
public sealed class NotificacoesHandler(
    IUsuarioRepository usuarios,
    IConsultaDeNotificacoes consulta,
    IConsultaDoMural mural,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<Pagina<NotificacaoLida>> ListarAsync(
        Guid usuarioId, string? cursor, int limite, CancellationToken cancellationToken)
    {
        var posicao = CursorDeTempo.Ler(cursor);
        _ = await UsuarioAsync(usuarioId, cancellationToken);
        return await consulta.PaginaAsync(usuarioId, posicao, limite, cancellationToken);
    }

    // Abrir o sininho marca tudo como lido (pergunta 6). O app manda o instante da mais nova
    // que mostrou; sem ele, vale agora.
    public async Task MarcarLidasAsync(Guid usuarioId, DateTimeOffset? ate, CancellationToken cancellationToken)
    {
        _ = await UsuarioAsync(usuarioId, cancellationToken);
        var agora = relogio.GetUtcNow();
        await consulta.MarcarLidasAsync(usuarioId, ate is { } limite && limite < agora ? limite : agora, agora, cancellationToken);
    }

    public async Task<Novidades> NovidadesAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var usuario = await UsuarioAsync(usuarioId, cancellationToken);
        var leitor = await mural.LeitorAsync(usuarioId, cancellationToken)
            ?? throw new InvalidOperationException($"O usuário {usuarioId} existe e não lê o mural.");

        // Troféus é do aluno: o professor e a instituição não têm prêmio.
        var ehAluno = usuario.Papeis == Domain.Academico.Papeis.Aluno;
        return new Novidades(
            await consulta.TemNaoLidaAsync(usuarioId, cancellationToken),
            await mural.TemNaoLidoAsync(leitor, cancellationToken),
            ehAluno && await consulta.TemPremioNovoAsync(usuarioId, usuario.TrofeusVistoEm, cancellationToken));
    }

    // Abrir a aba Troféus apaga a bolinha dela (pergunta 5).
    public async Task MarcarTrofeusVistosAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var usuario = await UsuarioAsync(usuarioId, cancellationToken);
        usuario.MarcarTrofeusVistos(relogio.GetUtcNow());
        await unitOfWork.SalvarAsync(cancellationToken);
    }

    private async Task<Domain.Identidade.Usuario> UsuarioAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        await usuarios.BuscarAsync(usuarioId, cancellationToken)
        ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {usuarioId} não existe.");
}
