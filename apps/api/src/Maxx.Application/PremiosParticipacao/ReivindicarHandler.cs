using Maxx.Application.Autenticacao;
using Maxx.Application.Fotos;
using Maxx.Application.Posts;
using Maxx.Domain;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;

namespace Maxx.Application.PremiosParticipacao;

// A reivindicação de prêmio de participação (fluxos/02-post-com-comprovacao.md): um post
// com foto, público com a marca de pendente, e o prêmio Reivindicado, que não pontua. As
// regras são do domínio; aqui, buscar o que o pedido cita, conferir no bucket que a foto
// chegou e gravar evento digitado, prêmio, comprovação e post numa transação só. A corrida
// de duas reivindicações com a mesma chave é do índice único; a de dois posts com a mesma
// foto, do xmin do upload. Nada vai para a fila: quem notifica e recalcula é a decisão.
public sealed class ReivindicarHandler(
    IUsuarioRepository usuarios,
    IUploadPendenteRepository uploads,
    IEventoRepository eventos,
    IPremioParticipacaoRepository premios,
    IPostRepository posts,
    IArmazenamentoDeFotos armazenamento,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<Reivindicacao> ReivindicarAsync(Guid alunoId, PedidoDeReivindicacao pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var aluno = await usuarios.BuscarAsync(alunoId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {alunoId} não existe.");
        var item = await premios.ItemAsync(pedido.Item.Trim(), cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosDoPremioParticipacao.ItemNaoEncontrado, $"{pedido.Item} não é prêmio de participação.");
        var curso = await eventos.CursoDoAlunoAsync(alunoId, cancellationToken);
        var evento = await EventoAsync(aluno, pedido, cancellationToken);
        var materia = await MateriaAsync(alunoId, pedido.MateriaId, evento, cancellationToken);
        var agora = relogio.GetUtcNow();
        var ultimoEncerrado = await eventos.UltimoSemestreEncerradoAsync(FusoHorario.DataLocal(agora), cancellationToken);

        var reivindicado = PremioParticipacao.Reivindicar(item.Item, item.Versao, aluno, curso, evento, materia, ultimoEncerrado, agora);
        if (reivindicado.Premio is not { } premio)
        {
            throw Recusa(reivindicado.Recusa!.Value, alunoId);
        }

        var upload = await uploads.BuscarAsync(pedido.UploadId, cancellationToken)
            ?? throw PublicarPostHandler.Recusa(RecusaDaPublicacao.UploadNaoEncontrado, alunoId);
        var publicado = Post.Reivindicar(aluno, pedido.Legenda, upload, premio, agora);
        if (publicado.Post is not { } post)
        {
            throw PublicarPostHandler.Recusa(publicado.Recusa!.Value, alunoId);
        }

        if (await premios.ChaveOcupadaAsync(premio.ChaveDeDeduplicacao, cancellationToken))
        {
            throw JaReivindicado(premio);
        }

        // O app diz que enviou; o bucket confirma. Sem as duas derivadas, a comprovação
        // seria uma foto quebrada.
        foreach (var chave in post.ChavesDaFoto)
        {
            if (!await armazenamento.ExisteAsync(chave, cancellationToken))
            {
                throw new RecusaException(TipoDeRecusa.Conflito, CodigosDoPost.FotoNaoEnviada, $"Objeto {chave} não chegou ao bucket.");
            }
        }

        if (evento.Origem == OrigemDoEvento.TextoLivre)
        {
            premios.Adicionar(evento);
        }

        premios.Adicionar(premio);
        posts.Adicionar(post);
        premios.Adicionar(Comprovacao.De(premio, post.Id));
        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeUnicidadeException conflito) when (conflito.Unicidade == Unicidade.PremioParticipacaoAtivo)
        {
            throw JaReivindicado(premio);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outro post confirmou o mesmo upload entre a leitura e a gravação.
            throw new RecusaException(TipoDeRecusa.Conflito, CodigosDoPost.UploadJaUsado, $"Upload {pedido.UploadId} usado ao mesmo tempo.");
        }

        return new Reivindicacao(post, premio);
    }

    // Um entre o semeado, pelo id, e o digitado, com nome e período. O semeado de outro curso
    // é recusado pelo domínio, como se não existisse.
    private async Task<Evento> EventoAsync(Domain.Identidade.Usuario aluno, PedidoDeReivindicacao pedido, CancellationToken cancellationToken)
    {
        switch (pedido.EventoId, pedido.EventoDigitado)
        {
            case (null, null):
                throw new RecusaException(TipoDeRecusa.Invalido, CodigosDoPremioParticipacao.EventoObrigatorio, "Reivindicação sem evento.");
            case (not null, not null):
                throw new RecusaException(TipoDeRecusa.Invalido, CodigosDoPremioParticipacao.EventoAmbiguo, "Evento semeado e digitado juntos.");
            case ({ } id, null):
                var semeado = await eventos.BuscarAsync(id, cancellationToken);
                return semeado is { Origem: OrigemDoEvento.Semeado }
                    ? semeado
                    : throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDoPremioParticipacao.EventoNaoEncontrado, $"Evento {id} não é semeado.");
            case (null, { } digitado):
                try
                {
                    return Evento.TextoLivre(digitado.Nome, digitado.Inicio, digitado.Fim, aluno);
                }
                catch (DomainException ex)
                {
                    throw new RecusaException(TipoDeRecusa.Invalido, CodigosDoPremioParticipacao.EventoInvalido, ex.Message);
                }
        }
    }

    // A matéria da apresentação em sala: uma turma dela em que o aluno estava matriculado no
    // semestre que contém a data do evento. Se a matéria cabe no item é pergunta do domínio.
    private async Task<Materia?> MateriaAsync(Guid alunoId, Guid? materiaId, Evento evento, CancellationToken cancellationToken)
    {
        if (materiaId is not { } id)
        {
            return null;
        }

        return await premios.MateriaCursadaAsync(alunoId, id, evento.Inicio, cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosDoPremioParticipacao.MateriaNaoEncontrada, $"Aluno {alunoId} não cursava {id} em {evento.Inicio}.");
    }

    private static RecusaException JaReivindicado(PremioParticipacao premio) =>
        new(TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.PremioJaReivindicado, $"Chave {premio.ChaveDeDeduplicacao} ocupada.");

    private static RecusaException Recusa(RecusaDaReivindicacao recusa, Guid alunoId) => recusa switch
    {
        RecusaDaReivindicacao.ExAluno => new(TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.ExAluno, $"{alunoId} é ex-aluno."),
        RecusaDaReivindicacao.EventoNaoEncontrado => new(
            TipoDeRecusa.NaoEncontrado, CodigosDoPremioParticipacao.EventoNaoEncontrado, $"Evento de outro curso para {alunoId}."),
        RecusaDaReivindicacao.EventoNaoAceitaOItem => new(
            TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.EventoNaoAceitaOItem, "O evento não aceita o item."),
        RecusaDaReivindicacao.EventoNaoComecou => new(
            TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.EventoNaoComecou, "O evento ainda não começou."),
        RecusaDaReivindicacao.EventoDeSemestreEncerrado => new(
            TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.EventoDeSemestreEncerrado, "O evento é de um semestre encerrado."),
        RecusaDaReivindicacao.ItemForaDeVigencia => new(
            TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.ItemForaDeVigencia, "Item fora de vigência na data do evento."),
        RecusaDaReivindicacao.ItemDeOutroCurso => new(
            TipoDeRecusa.Conflito, CodigosDoPremioParticipacao.ItemDeOutroCurso, "Item restrito a outro curso."),
        RecusaDaReivindicacao.MateriaObrigatoria => new(
            TipoDeRecusa.Invalido, CodigosDoPremioParticipacao.MateriaObrigatoria, "Apresentação em sala sem matéria."),
        RecusaDaReivindicacao.MateriaNaoSeAplica => new(
            TipoDeRecusa.Invalido, CodigosDoPremioParticipacao.MateriaNaoSeAplica, "Matéria num item que não deduplica por ela."),
        _ => throw new ArgumentOutOfRangeException(nameof(recusa), recusa, null),
    };
}

// O evento: o id de um semeado ou o digitado, nunca os dois. A matéria, só na apresentação
// em sala (P17). A foto é obrigatória: é a comprovação.
public sealed record PedidoDeReivindicacao(
    string Item, Guid? EventoId, EventoDigitado? EventoDigitado, Guid? MateriaId, Guid UploadId, string? Legenda);

public sealed record EventoDigitado(string Nome, DateOnly Inicio, DateOnly Fim);

public sealed record Reivindicacao(Post Post, PremioParticipacao Premio);

public sealed record ItemDePremioParticipacao(ItemPremioParticipacao Item, int Versao);

public interface IPremioParticipacaoRepository
{
    void Adicionar(Evento evento);

    void Adicionar(PremioParticipacao premio);

    void Adicionar(Comprovacao comprovacao);

    Task<PremioParticipacao?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    // O item com a versão de agora, que o prêmio congela (ADR 0007).
    Task<ItemDePremioParticipacao?> ItemAsync(string codigo, CancellationToken cancellationToken);

    // Se algum prêmio que não foi reprovado já tem a chave.
    Task<bool> ChaveOcupadaAsync(string chave, CancellationToken cancellationToken);

    // A matéria, se o aluno (usuário) estava matriculado numa turma dela cujo semestre
    // contém a data.
    Task<Materia?> MateriaCursadaAsync(Guid alunoId, Guid materiaId, DateOnly data, CancellationToken cancellationToken);
}
