using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Premios;

// O prêmio de participação (produto.md 4.7): o aluno reivindica com um post com foto, que é
// a comprovação, e alguém aprova. Congela tier, pontos e versão do item na reivindicação
// (ADR 0007). Amarra-se a um evento, semeado ou digitado (produto.md 4.6).
//
//   reivindicado ──aprovado──> válido ──contestado──> em averiguação ──confirma──> definitivo
//        │                       │                          └──revoga──> revogado
//        │                       └──15 dias sem contestação──> definitivo
//        └──reprovado──> post comum (nova tentativa é outro post de reivindicação)
//
// O ciclo inteiro mora aqui: reivindicação, decisão sobre a comprovação, contestação e
// decisão da instituição.
public sealed class PremioParticipacao
{
    // Da aprovação correm 15 dias de janela de contestação (produto.md 4.7).
    public static readonly TimeSpan JanelaDeContestacao = TimeSpan.FromDays(15);

    // Para o EF Core materializar do banco.
    private PremioParticipacao()
    {
        ItemCodigo = null!;
        ChaveDeDeduplicacao = null!;
    }

    private PremioParticipacao(
        ItemPremioParticipacao item,
        int versaoDoItem,
        Usuario aluno,
        Evento evento,
        Materia? materia,
        string chaveDeDeduplicacao,
        DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        AlunoId = aluno.Id;
        ItemCodigo = item.Codigo;
        VersaoDoItem = versaoDoItem;
        Tier = item.Tier;
        Pontos = item.Pontos;
        EventoId = evento.Id;
        MateriaId = materia?.Id;
        ChaveDeDeduplicacao = chaveDeDeduplicacao;
        Estado = EstadoDoPremioParticipacao.Reivindicado;
        ReivindicadoEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid AlunoId { get; private set; }

    public string ItemCodigo { get; private set; }

    public int VersaoDoItem { get; private set; }

    public Tier Tier { get; private set; }

    public int Pontos { get; private set; }

    public Guid EventoId { get; private set; }

    // Só na apresentação em sala (P17), que deduplica por matéria.
    public Guid? MateriaId { get; private set; }

    // Os campos que o item declara, com os valores desta reivindicação. Única entre os
    // prêmios que não foram reprovados: é o que decide a corrida de duas reivindicações.
    public string ChaveDeDeduplicacao { get; private set; }

    public EstadoDoPremioParticipacao Estado { get; private set; }

    public DateTimeOffset ReivindicadoEm { get; private set; }

    // Quem aprovou e quando (produto.md 4.10). Da aprovação corre a janela.
    public Guid? AprovadoPorId { get; private set; }

    public DateTimeOffset? AprovadoEm { get; private set; }

    // Quem reprovou, quando e por quê. O motivo é visível ao aluno.
    public Guid? ReprovadoPorId { get; private set; }

    public DateTimeOffset? ReprovadoEm { get; private set; }

    public string? MotivoDaReprovacao { get; private set; }

    // Quando o prêmio ficou definitivo: pelo fim da janela sem contestação ou pela
    // confirmação da instituição.
    public DateTimeOffset? DefinitivoEm { get; private set; }

    // Quando foi contestado: uma contestação por prêmio, e a marca fica depois da decisão.
    public DateTimeOffset? ContestadoEm { get; private set; }

    // Quem revogou, quando e por quê (produto.md 4.7): fica no histórico, visível ao aluno e
    // à instituição. Revoga a instituição, decidindo a contestação ou removendo o post.
    public Guid? RevogadoPorId { get; private set; }

    public DateTimeOffset? RevogadoEm { get; private set; }

    public string? MotivoDaRevogacao { get; private set; }

    // O instante em que a janela de contestação fecha, para o prêmio aprovado.
    public DateTimeOffset? FimDaJanela => AprovadoEm + JanelaDeContestacao;

    // Todos os estados seguram a chave, menos o reprovado: a nova tentativa é outra
    // reivindicação do mesmo evento. O revogado segura (decisão do passo 8, D5).
    public bool SeguraAChave => Estado != EstadoDoPremioParticipacao.Reprovado;

    // Válido e definitivo pontuam (produto.md 4.7).
    public bool Pontua => Estado is EstadoDoPremioParticipacao.Valido or EstadoDoPremioParticipacao.Definitivo;

    // A foto do post é a comprovação: enquanto alguém ainda pode decidir sobre o prêmio, o
    // post não sai (decisão do passo 8). Definitivo, o post é como outro qualquer.
    public bool PrendeOPost => Estado is EstadoDoPremioParticipacao.Reivindicado
        or EstadoDoPremioParticipacao.Valido
        or EstadoDoPremioParticipacao.EmAveriguacao;

    // cursoDoAluno: o curso atual, para o evento e o item restritos a curso. materia: só na
    // apresentação em sala; que o aluno estuda a matéria é pergunta ao banco, feita antes
    // por quem chama. ultimoEncerrado: o semestre letivo de fim mais recente antes de hoje.
    public static ResultadoDaReivindicacao Reivindicar(
        ItemPremioParticipacao item,
        int versaoDoItem,
        Usuario aluno,
        Curso? cursoDoAluno,
        Evento evento,
        Materia? materia,
        SemestreLetivo? ultimoEncerrado,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(aluno);
        ArgumentNullException.ThrowIfNull(evento);

        if (aluno.Papeis != Papeis.Aluno)
        {
            throw new DomainException($"Só aluno reivindica prêmio de participação; {aluno.Id} é {aluno.Papeis}.");
        }

        if (aluno.EhExAluno)
        {
            return Recusa(RecusaDaReivindicacao.ExAluno);
        }

        if (evento.Origem == OrigemDoEvento.TextoLivre && evento.CriadoPorId != aluno.Id)
        {
            throw new DomainException($"O evento {evento.Id} foi digitado por outra pessoa.");
        }

        if (!evento.AbertoAoCurso(cursoDoAluno?.Id))
        {
            return Recusa(RecusaDaReivindicacao.EventoNaoEncontrado);
        }

        if (!evento.Aceita(item))
        {
            return Recusa(RecusaDaReivindicacao.EventoNaoAceitaOItem);
        }

        var hoje = FusoHorario.DataLocal(agora);
        switch (evento.RecusaParaReivindicarEm(hoje, ultimoEncerrado))
        {
            case RecusaDoEvento.NaoComecou:
                return Recusa(RecusaDaReivindicacao.EventoNaoComecou);
            case RecusaDoEvento.DeSemestreEncerrado:
                return Recusa(RecusaDaReivindicacao.EventoDeSemestreEncerrado);
        }

        // A vigência do item vale na data do evento, que é quando a participação aconteceu.
        if (item.Vigencia is { Inicio: { } inicio } && evento.Inicio < inicio || item.Vigencia is { Fim: { } fim } && evento.Inicio > fim)
        {
            return Recusa(RecusaDaReivindicacao.ItemForaDeVigencia);
        }

        if (item.RestricaoCurso is { } restricao
            && !string.Equals(restricao, cursoDoAluno?.Sigla, StringComparison.OrdinalIgnoreCase))
        {
            return Recusa(RecusaDaReivindicacao.ItemDeOutroCurso);
        }

        var exigeMateria = Evento.SoPorTextoLivre(item);
        if (exigeMateria && materia is null)
        {
            return Recusa(RecusaDaReivindicacao.MateriaObrigatoria);
        }

        if (!exigeMateria && materia is not null)
        {
            return Recusa(RecusaDaReivindicacao.MateriaNaoSeAplica);
        }

        return new(
            new PremioParticipacao(item, versaoDoItem, aluno, evento, materia, Chave(item, aluno, evento, materia), agora),
            null);
    }

    // Aprovação da comprovação (produto.md 4.7): o professor de uma turma do aluno no
    // semestre vigente (escopo conferido no banco por quem chama) ou a instituição. Vale quem
    // chega primeiro: decidida, a comprovação recusa a segunda decisão. Aprovado, o prêmio
    // pontua na hora e abre a janela de contestação.
    public RecusaDaDecisao? Aprovar(Usuario aluno, Usuario quem, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(aluno);
        ArgumentNullException.ThrowIfNull(quem);
        if (aluno.Id != AlunoId)
        {
            throw new ArgumentException($"O prêmio {Id} não é de {aluno.Id}.", nameof(aluno));
        }

        ExigirAprovador(quem);
        if (Estado != EstadoDoPremioParticipacao.Reivindicado)
        {
            return RecusaDaDecisao.JaDecidida;
        }

        if (aluno.EhExAluno)
        {
            return RecusaDaDecisao.ExAluno;
        }

        Estado = EstadoDoPremioParticipacao.Valido;
        AprovadoPorId = quem.Id;
        AprovadoEm = agora;
        return null;
    }

    // Reprovação, sempre com motivo. A reivindicação se encerra e libera a chave: a nova
    // tentativa é outro post, com outra foto. O post desta perde o vínculo (quem chama).
    public RecusaDaDecisao? Reprovar(Usuario quem, string? motivo, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(quem);
        ExigirAprovador(quem);

        if (Estado != EstadoDoPremioParticipacao.Reivindicado)
        {
            return RecusaDaDecisao.JaDecidida;
        }

        if (Motivo.Normalizar(motivo) is not { } normalizado)
        {
            return RecusaDaDecisao.MotivoInvalido;
        }

        Estado = EstadoDoPremioParticipacao.Reprovado;
        ReprovadoPorId = quem.Id;
        ReprovadoEm = agora;
        MotivoDaReprovacao = normalizado;
        return null;
    }

    // Passados os 15 dias sem contestação, o prêmio válido fica definitivo (produto.md 4.7).
    // Contestado, em averiguação ou já decidido, nada muda: quem resolve é a instituição.
    public bool TornarDefinitivoSeAJanelaFechou(DateTimeOffset agora)
    {
        if (Estado != EstadoDoPremioParticipacao.Valido || agora < FimDaJanela)
        {
            return false;
        }

        Estado = EstadoDoPremioParticipacao.Definitivo;
        DefinitivoEm = agora;
        return true;
    }

    // Contestação (produto.md 4.7): o professor da turma, inclusive quem aprovou (escopo
    // conferido no banco por quem chama), ou a instituição, sempre com motivo, dentro dos 15
    // dias da aprovação. Uma por prêmio. Contestado, o prêmio vai a averiguação e para de
    // pontuar até a instituição decidir.
    public ResultadoDaContestacao Contestar(Usuario quem, string? motivo, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(quem);
        ExigirAprovador(quem);

        if (ContestadoEm is not null)
        {
            return new(null, RecusaDaContestacao.JaContestado);
        }

        switch (Estado)
        {
            case EstadoDoPremioParticipacao.Reivindicado or EstadoDoPremioParticipacao.Reprovado:
                return new(null, RecusaDaContestacao.NaoContestavel);
            case EstadoDoPremioParticipacao.Definitivo or EstadoDoPremioParticipacao.Revogado:
                return new(null, RecusaDaContestacao.ForaDaJanela);
        }

        if (agora >= FimDaJanela)
        {
            return new(null, RecusaDaContestacao.ForaDaJanela);
        }

        if (Motivo.Normalizar(motivo) is not { } normalizado)
        {
            return new(null, RecusaDaContestacao.MotivoInvalido);
        }

        Estado = EstadoDoPremioParticipacao.EmAveriguacao;
        ContestadoEm = agora;
        return new(new Contestacao(this, quem, normalizado, agora), null);
    }

    // A instituição confirma: o prêmio fica definitivo, volta a pontuar e a janela encerra na
    // hora; não pode mais ser contestado.
    public RecusaDaDecisao? Confirmar(Contestacao contestacao, Usuario instituicao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(contestacao);
        ArgumentNullException.ThrowIfNull(instituicao);
        ExigirContestacaoDeste(contestacao);
        Contestacao.ExigirInstituicao(instituicao);

        if (Estado != EstadoDoPremioParticipacao.EmAveriguacao || !contestacao.EstaAberta)
        {
            return RecusaDaDecisao.JaDecidida;
        }

        contestacao.Decidir(instituicao, DecisaoDaContestacao.Confirmada, agora);
        Estado = EstadoDoPremioParticipacao.Definitivo;
        DefinitivoEm = agora;
        return null;
    }

    // A instituição revoga, com motivo: o prêmio sai da pontuação e fica no histórico. Pela
    // contestação, decide a contestação aberta; pela remoção do post, revoga também o prêmio
    // válido, que não tem contestação. Quem chama desvincula o post.
    public RecusaDaDecisao? Revogar(Usuario instituicao, string? motivo, Contestacao? contestacao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(instituicao);
        Contestacao.ExigirInstituicao(instituicao);
        if (contestacao is not null)
        {
            ExigirContestacaoDeste(contestacao);
        }

        var revogavel = Estado == EstadoDoPremioParticipacao.EmAveriguacao
            ? contestacao is { EstaAberta: true }
            : Estado == EstadoDoPremioParticipacao.Valido && contestacao is null;
        if (!revogavel)
        {
            return RecusaDaDecisao.JaDecidida;
        }

        if (Motivo.Normalizar(motivo) is not { } normalizado)
        {
            return RecusaDaDecisao.MotivoInvalido;
        }

        contestacao?.Decidir(instituicao, DecisaoDaContestacao.Revogada, agora);
        Estado = EstadoDoPremioParticipacao.Revogado;
        RevogadoPorId = instituicao.Id;
        RevogadoEm = agora;
        MotivoDaRevogacao = normalizado;
        return null;
    }

    private void ExigirContestacaoDeste(Contestacao contestacao)
    {
        if (contestacao.PremioParticipacaoId != Id)
        {
            throw new ArgumentException($"A contestação {contestacao.Id} não é do prêmio {Id}.", nameof(contestacao));
        }
    }

    private static void ExigirAprovador(Usuario quem)
    {
        if (!quem.Papeis.HasFlag(Papeis.Professor) && !quem.Papeis.HasFlag(Papeis.Instituicao))
        {
            throw new DomainException($"Só professor ou instituição decide comprovação; {quem.Id} é {quem.Papeis}.");
        }
    }

    // A chave do catálogo (produto.md 4.6). Com evento semeado, aluno e evento. Com evento
    // digitado, o evento não identifica nada (cada reivindicação digita o seu), e a chave
    // degrada para aluno, item e data de início do evento. P17 já declara aluno, item, data
    // e matéria.
    private static string Chave(ItemPremioParticipacao item, Usuario aluno, Evento evento, Materia? materia)
    {
        var campos = item.ChaveDeDeduplicacao
            .SelectMany(campo => campo == CampoDeDeduplicacao.Evento && evento.Origem == OrigemDoEvento.TextoLivre
                ? new[] { CampoDeDeduplicacao.Item, CampoDeDeduplicacao.DataDoEvento }
                : [campo])
            .Distinct();

        return string.Join('|', campos.Select(campo => campo switch
        {
            CampoDeDeduplicacao.Aluno => $"aluno:{aluno.Id}",
            CampoDeDeduplicacao.Item => $"item:{item.Codigo}",
            CampoDeDeduplicacao.Evento => $"evento:{evento.Id}",
            CampoDeDeduplicacao.DataDoEvento => $"data:{evento.Inicio:yyyy-MM-dd}",
            CampoDeDeduplicacao.Materia => $"materia:{materia!.Id}",
            _ => throw new DomainException($"Prêmio de participação {item.Codigo} não deduplica por {campo}."),
        }));
    }

    private static ResultadoDaReivindicacao Recusa(RecusaDaReivindicacao recusa) => new(null, recusa);
}

public enum EstadoDoPremioParticipacao
{
    Reivindicado,
    Reprovado,
    Valido,
    EmAveriguacao,
    Definitivo,
    Revogado,
}

public enum RecusaDaReivindicacao
{
    ExAluno,

    // De outro curso: para quem pede, é como se não existisse.
    EventoNaoEncontrado,
    EventoNaoAceitaOItem,
    EventoNaoComecou,
    EventoDeSemestreEncerrado,
    ItemForaDeVigencia,
    ItemDeOutroCurso,
    MateriaObrigatoria,
    MateriaNaoSeAplica,
}

public sealed record ResultadoDaReivindicacao(PremioParticipacao? Premio, RecusaDaReivindicacao? Recusa);

public enum RecusaDaDecisao
{
    // Outro aprovador chegou antes, ou a comprovação já não está pendente.
    JaDecidida,
    ExAluno,
    MotivoInvalido,
}

// A comprovação (glossário): a foto que sustenta o prêmio de participação reivindicado, que
// é a do post da reivindicação. Guarda o evento, para a fila filtrar por ele e para a
// aprovação em lote da fase 2 (produto.md 13). A concessão direta da instituição não tem
// comprovação.
public sealed class Comprovacao
{
    // A fila marca o que passou de 7 dias, sem decidir nada sozinha (produto.md 4.7 e 12.6).
    public static readonly TimeSpan PrazoDaMarca = TimeSpan.FromDays(7);

    // Para o EF Core materializar do banco.
    private Comprovacao()
    {
    }

    private Comprovacao(PremioParticipacao premio, Guid postId)
    {
        Id = Guid.CreateVersion7();
        PremioParticipacaoId = premio.Id;
        AlunoId = premio.AlunoId;
        EventoId = premio.EventoId;
        PostId = postId;
        CriadaEm = premio.ReivindicadoEm;
    }

    public Guid Id { get; private set; }

    public Guid PremioParticipacaoId { get; private set; }

    public Guid AlunoId { get; private set; }

    public Guid EventoId { get; private set; }

    // O post da reivindicação. Nulo depois que o post sai, o que só acontece quando ele já
    // não prende a foto (PremioParticipacao.PrendeOPost).
    public Guid? PostId { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    // Passou de 7 vezes 24 horas desde a reivindicação (D11).
    public static bool PassouDoPrazo(DateTimeOffset criadaEm, DateTimeOffset agora) => agora - criadaEm > PrazoDaMarca;

    public static Comprovacao De(PremioParticipacao premio, Guid postId)
    {
        ArgumentNullException.ThrowIfNull(premio);
        if (premio.Estado != EstadoDoPremioParticipacao.Reivindicado)
        {
            throw new DomainException($"Só a reivindicação tem comprovação; o prêmio {premio.Id} está {premio.Estado}.");
        }

        return new Comprovacao(premio, postId);
    }
}
