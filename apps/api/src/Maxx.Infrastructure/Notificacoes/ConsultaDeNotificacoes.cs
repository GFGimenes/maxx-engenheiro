using Maxx.Application.Notificacoes;
using Maxx.Application.Paginacao;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Mural;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Notificacoes;

// A página do sininho: as notificações e, numa consulta por tipo de referência, o que a
// frase e o toque de cada uma pedem. A referência é genérica, sem chave estrangeira: o que
// ela aponta pode ter saído (post removido), e então o campo vem vazio.
internal sealed class ConsultaDeNotificacoes(MaxxDbContext db) : IConsultaDeNotificacoes
{
    private static readonly TipoDeNotificacao[] _doPremioParticipacao =
    [
        TipoDeNotificacao.ComprovacaoAprovada,
        TipoDeNotificacao.ComprovacaoReprovada,
        TipoDeNotificacao.ContestacaoAberta,
        TipoDeNotificacao.DecisaoDeContestacao,
        TipoDeNotificacao.PremioRevogado,
        TipoDeNotificacao.PremioConcedido,
    ];

    public async Task<Pagina<NotificacaoLida>> PaginaAsync(
        Guid usuarioId, PosicaoNoTempo? depoisDe, int limite, CancellationToken cancellationToken)
    {
        var notificacoes = db.Set<Notificacao>().AsNoTracking().Where(notificacao => notificacao.UsuarioId == usuarioId);
        if (depoisDe is { } posicao)
        {
            notificacoes = notificacoes.Where(notificacao => EF.Functions.LessThan(
                ValueTuple.Create(notificacao.CriadaEm, notificacao.Id), ValueTuple.Create(posicao.Instante, posicao.Id)));
        }

        var linhas = await notificacoes
            .OrderByDescending(notificacao => notificacao.CriadaEm)
            .ThenByDescending(notificacao => notificacao.Id)
            .Take(limite + 1)
            .ToListAsync(cancellationToken);
        var pagina = linhas.Take(limite).ToList();

        var detalhes = await DetalhesAsync(pagina, cancellationToken);
        return new Pagina<NotificacaoLida>(
            [.. pagina.Select(notificacao => detalhes.Completar(new NotificacaoLida(
                notificacao.Id, notificacao.Tipo, notificacao.ReferenciaId, notificacao.CriadaEm, notificacao.LidaEm is not null)))],
            linhas.Count > limite ? CursorDeTempo.Codificar(new PosicaoNoTempo(pagina[^1].CriadaEm, pagina[^1].Id)) : null);
    }

    public Task MarcarLidasAsync(Guid usuarioId, DateTimeOffset ate, DateTimeOffset agora, CancellationToken cancellationToken) =>
        db.Set<Notificacao>()
            .Where(notificacao => notificacao.UsuarioId == usuarioId && notificacao.LidaEm == null && notificacao.CriadaEm <= ate)
            .ExecuteUpdateAsync(colunas => colunas.SetProperty(notificacao => notificacao.LidaEm, agora), cancellationToken);

    public Task<bool> TemNaoLidaAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        db.Set<Notificacao>().AnyAsync(notificacao => notificacao.UsuarioId == usuarioId && notificacao.LidaEm == null, cancellationToken);

    public async Task<bool> TemPremioNovoAsync(Guid alunoId, DateTimeOffset? depoisDe, CancellationToken cancellationToken)
    {
        var desde = depoisDe ?? DateTimeOffset.MinValue;
        return await db.Set<Medalha>().AnyAsync(
                medalha => medalha.AlunoId == alunoId && medalha.AnuladaEm == null && medalha.ConcedidaEm > desde, cancellationToken)
            || await db.Set<PremioParticipacao>().AnyAsync(
                premio => premio.AlunoId == alunoId
                    && (premio.Estado == EstadoDoPremioParticipacao.Valido || premio.Estado == EstadoDoPremioParticipacao.Definitivo)
                    && premio.AprovadoEm > desde,
                cancellationToken)
            || await db.Set<Conquista>().AnyAsync(
                conquista => conquista.AlunoId == alunoId && conquista.RemovidaEm == null && conquista.ConcedidaEm > desde, cancellationToken);
    }

    private async Task<Detalhes> DetalhesAsync(IReadOnlyCollection<Notificacao> pagina, CancellationToken cancellationToken)
    {
        Guid[] Referencias(params TipoDeNotificacao[] tipos) =>
            [.. pagina.Where(notificacao => tipos.Contains(notificacao.Tipo)).Select(notificacao => notificacao.ReferenciaId).Distinct()];

        var idsDeMedalha = Referencias(TipoDeNotificacao.MedalhaRecebida);
        var idsDePremio = Referencias(_doPremioParticipacao);
        var idsDeConquista = Referencias(TipoDeNotificacao.ConquistaObtida, TipoDeNotificacao.ConquistaRemovida);
        var idsDeAviso = Referencias(TipoDeNotificacao.AvisoNovo);

        var medalhas = await db.Set<Medalha>().AsNoTracking()
            .Where(medalha => idsDeMedalha.Contains(medalha.Id))
            .ToDictionaryAsync(medalha => medalha.Id, medalha => medalha.ItemCodigo, cancellationToken);
        var premios = await db.Set<PremioParticipacao>().AsNoTracking()
            .Where(premio => idsDePremio.Contains(premio.Id))
            .ToDictionaryAsync(premio => premio.Id, cancellationToken);
        var conquistas = await db.Set<Conquista>().AsNoTracking()
            .Where(conquista => idsDeConquista.Contains(conquista.Id))
            .ToDictionaryAsync(conquista => conquista.Id, conquista => conquista.ItemCodigo, cancellationToken);
        var avisos = await db.Set<Aviso>().AsNoTracking()
            .Where(aviso => idsDeAviso.Contains(aviso.Id))
            .ToDictionaryAsync(aviso => aviso.Id, aviso => aviso.Titulo, cancellationToken);

        // O post do prêmio que ainda está no ar: o da reivindicação, ou o que o aluno fez
        // depois. O removido pela instituição não entra.
        var postsDosPremios = await db.Set<Post>().AsNoTracking()
            .Where(post => post.PremioParticipacaoId != null && idsDePremio.Contains(post.PremioParticipacaoId.Value) && post.RemovidoEm == null)
            .Select(post => new { PremioId = post.PremioParticipacaoId!.Value, post.Id })
            .ToListAsync(cancellationToken);
        var decisoes = await db.Set<Contestacao>().AsNoTracking()
            .Where(contestacao => idsDePremio.Contains(contestacao.PremioParticipacaoId) && contestacao.Decisao != null)
            .ToDictionaryAsync(contestacao => contestacao.PremioParticipacaoId, contestacao => contestacao.Decisao!.Value, cancellationToken);

        var codigos = medalhas.Values.Concat(premios.Values.Select(premio => premio.ItemCodigo)).Concat(conquistas.Values).Distinct().ToArray();
        var nomes = await db.Set<ItemCatalogoRow>().AsNoTracking()
            .Where(item => codigos.Contains(item.Codigo))
            .ToDictionaryAsync(item => item.Codigo, item => item.Nome, cancellationToken);

        return new Detalhes(
            medalhas.ToDictionary(par => par.Key, par => nomes.GetValueOrDefault(par.Value)),
            premios,
            conquistas.ToDictionary(par => par.Key, par => nomes.GetValueOrDefault(par.Value)),
            avisos,
            postsDosPremios.GroupBy(post => post.PremioId).ToDictionary(grupo => grupo.Key, grupo => grupo.First().Id),
            decisoes,
            nomes);
    }

    private sealed record Detalhes(
        Dictionary<Guid, string?> Medalhas,
        Dictionary<Guid, PremioParticipacao> Premios,
        Dictionary<Guid, string?> Conquistas,
        Dictionary<Guid, string> Avisos,
        Dictionary<Guid, Guid> PostsDosPremios,
        Dictionary<Guid, DecisaoDaContestacao> Decisoes,
        Dictionary<string, string> Nomes)
    {
        public NotificacaoLida Completar(NotificacaoLida notificacao)
        {
            var referencia = notificacao.ReferenciaId;
            switch (notificacao.Tipo)
            {
                case TipoDeNotificacao.MedalhaRecebida:
                    return notificacao with { Item = Medalhas.GetValueOrDefault(referencia) };
                case TipoDeNotificacao.ConquistaObtida or TipoDeNotificacao.ConquistaRemovida:
                    return notificacao with { Item = Conquistas.GetValueOrDefault(referencia) };
                case TipoDeNotificacao.AvisoNovo:
                    return notificacao with { TituloDoAviso = Avisos.GetValueOrDefault(referencia) };
            }

            if (!Premios.TryGetValue(referencia, out var premio))
            {
                return notificacao;
            }

            var completa = notificacao with
            {
                Item = Nomes.GetValueOrDefault(premio.ItemCodigo),
                PostId = PostsDosPremios.TryGetValue(referencia, out var post) ? post : null,
            };
            return notificacao.Tipo switch
            {
                TipoDeNotificacao.ComprovacaoReprovada => completa with { Motivo = premio.MotivoDaReprovacao },
                TipoDeNotificacao.PremioRevogado => completa with { Motivo = premio.MotivoDaRevogacao },
                TipoDeNotificacao.DecisaoDeContestacao => completa with
                {
                    Decisao = Decisoes.TryGetValue(referencia, out var decisao) ? decisao : null,
                    Motivo = premio.MotivoDaRevogacao,
                },
                _ => completa,
            };
        }
    }
}
