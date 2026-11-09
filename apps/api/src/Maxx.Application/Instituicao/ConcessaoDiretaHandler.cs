using Maxx.Application.Autenticacao;
using Maxx.Application.Fila;
using Maxx.Application.Premios;
using Maxx.Application.PremiosParticipacao;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Premios;

namespace Maxx.Application.Instituicao;

// A concessão direta de prêmio de participação pela instituição (produto.md 4.7, passo 12):
// já aprovado, válido e sem comprovação, com a janela de 15 dias como qualquer aprovado. O
// aluno escolhe se posta (P2). As regras são as da reivindicação, no domínio; aqui, buscar o
// que o pedido cita e gravar o prêmio com a notificação, os recálculos de ranking e raridade
// e o fim da janela, na mesma transação. O instante da concessão é o do desempate.
public sealed class ConcessaoDiretaHandler(
    IUsuarioRepository usuarios,
    IAlunosDaInstituicaoRepository alunos,
    IEventoRepository eventos,
    IPremioParticipacaoRepository premios,
    EventosHandler eventosReivindicaveis,
    MateriasDoAlunoHandler materiasDoAluno,
    IConsultaDePremios consultaDePremios,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<PremioParticipacao> ConcederAsync(
        Guid quemId, Guid cadastroId, PedidoDeConcessao pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var instituicao = await AtosSobreOAlunoHandler.InstituicaoAsync(usuarios, quemId, cancellationToken);
        var aluno = await AtosSobreOAlunoHandler.ContaDoAlunoAsync(alunos, cadastroId, cancellationToken);
        var item = await premios.ItemAsync(pedido.Item.Trim(), cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosDoPremioParticipacao.ItemNaoEncontrado, $"{pedido.Item} não é prêmio de participação.");
        var curso = await eventos.CursoDoAlunoAsync(aluno.Id, cancellationToken);
        var evento = await ReivindicarHandler.EventoDoPedidoAsync(
            eventos, instituicao, pedido.EventoId, pedido.EventoDigitado, cancellationToken);
        var materia = await ReivindicarHandler.MateriaDoPedidoAsync(premios, aluno.Id, pedido.MateriaId, evento, cancellationToken);
        var agora = relogio.GetUtcNow();
        var ultimoEncerrado = await eventos.UltimoSemestreEncerradoAsync(FusoHorario.DataLocal(agora), cancellationToken);

        var concedido = PremioParticipacao.ConcederPelaInstituicao(
            item.Item, item.Versao, aluno, curso, evento, materia, ultimoEncerrado, instituicao, agora);
        if (concedido.Premio is not { } premio)
        {
            throw ReivindicarHandler.Recusa(concedido.Recusa!.Value, aluno.Id);
        }

        if (await premios.ChaveOcupadaAsync(premio.ChaveDeDeduplicacao, cancellationToken))
        {
            throw ReivindicarHandler.JaReivindicado(premio);
        }

        if (evento.Origem == OrigemDoEvento.TextoLivre)
        {
            premios.Adicionar(evento);
        }

        premios.Adicionar(premio);
        fila.Enfileirar(new NotificarPremioConcedido(premio.Id));
        fila.PedirRecalculos();
        fila.Enfileirar(new TornarPremioDefinitivo(premio.Id), premio.FimDaJanela);
        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeUnicidadeException conflito) when (conflito.Unicidade == Unicidade.PremioParticipacaoAtivo)
        {
            throw ReivindicarHandler.JaReivindicado(premio);
        }

        return premio;
    }

    // Os eventos semeados que o aluno poderia reivindicar hoje, com o item: a mesma lista
    // que o app dele mostra. Fora dela, a instituição digita o evento.
    public async Task<IReadOnlyList<Evento>> EventosAsync(
        Guid quemId, Guid cadastroId, string? item, CancellationToken cancellationToken)
    {
        await AtosSobreOAlunoHandler.InstituicaoAsync(usuarios, quemId, cancellationToken);
        var aluno = await AtosSobreOAlunoHandler.ContaDoAlunoAsync(alunos, cadastroId, cancellationToken);
        return await eventosReivindicaveis.ReivindicaveisAsync(aluno.Id, item, cancellationToken);
    }

    // Os prêmios de participação do catálogo que o aluno pode receber: os mesmos da tela de
    // Troféus dele, sem os restritos a outro curso, com o critério e se pedem a matéria.
    public async Task<IReadOnlyList<ItemDoAluno>> ItensAsync(Guid quemId, Guid cadastroId, CancellationToken cancellationToken)
    {
        await AtosSobreOAlunoHandler.InstituicaoAsync(usuarios, quemId, cancellationToken);
        var aluno = await AtosSobreOAlunoHandler.ContaDoAlunoAsync(alunos, cadastroId, cancellationToken);
        var premios = await consultaDePremios.DoAlunoAsync(aluno.Id, cancellationToken);
        return [.. (premios?.Itens ?? []).Where(item => item.Tipo == TipoDeItem.PremioParticipacao)];
    }

    // As matérias que o aluno cursa hoje, para a apresentação em sala (P17).
    public async Task<IReadOnlyList<Materia>> MateriasAsync(Guid quemId, Guid cadastroId, CancellationToken cancellationToken)
    {
        await AtosSobreOAlunoHandler.InstituicaoAsync(usuarios, quemId, cancellationToken);
        var aluno = await AtosSobreOAlunoHandler.ContaDoAlunoAsync(alunos, cadastroId, cancellationToken);
        return await materiasDoAluno.CursadasAsync(aluno.Id, cancellationToken);
    }
}

// O evento: o id de um semeado ou o digitado, nunca os dois. A matéria, só na apresentação
// em sala (P17). Sem foto: a concessão direta não tem comprovação.
public sealed record PedidoDeConcessao(string Item, Guid? EventoId, EventoDigitado? EventoDigitado, Guid? MateriaId);
