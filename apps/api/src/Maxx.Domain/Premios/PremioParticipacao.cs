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
// Nesta etapa nasce a reivindicação; aprovação, reprovação, contestação e decisão vêm
// nas seguintes.
public sealed class PremioParticipacao
{
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

// A comprovação (glossário): a foto que sustenta o prêmio de participação reivindicado, que
// é a do post da reivindicação. Guarda o evento, para a fila filtrar por ele e para a
// aprovação em lote da fase 2 (produto.md 13). A concessão direta da instituição não tem
// comprovação.
public sealed class Comprovacao
{
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
