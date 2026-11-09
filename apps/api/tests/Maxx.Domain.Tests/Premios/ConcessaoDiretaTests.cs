using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Domain.Tests.Catalogo;
using static Maxx.Domain.Tests.Academico.Calendario2026;

namespace Maxx.Domain.Tests.Premios;

// A concessão direta de prêmio de participação pela instituição (passo 12, P2): nasce
// válida, aprovada pela instituição, sem comprovação, com a janela de 15 dias e as mesmas
// regras de evento, item e curso da reivindicação.
public class ConcessaoDiretaTests
{
    // 10/11/2026, 10h em São Paulo: em 2026/2, e 2026/1 é o último semestre encerrado.
    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);
    private static readonly Curso _adm = new("Administração", "ADM", 8);
    private static readonly SemestreLetivo _anterior = Primeiro();

    private readonly Usuario _ana = Usuario.CadastrarAluno(
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3), _cc, "Ana", "anaSouza", "hash", _agora.AddDays(-90));

    private readonly Usuario _coordenacao = Usuario.CriarPeloSeed(
        CadastroElegivel.Instituicao("coordenacao@unieduk.com.br", "Coordenação UniMax"), "coordenacao", _agora.AddDays(-90));

    private static Evento Semana(Curso? curso = null, DateOnly? inicio = null) => Evento.Semeado(
        "semana-academica-cc-2026-2", "Semana Acadêmica", inicio ?? Data(9, 14), (inicio ?? Data(9, 14)).AddDays(4), [CatalogoDeTeste.P06], curso);

    private ResultadoDaReivindicacao Conceder(Evento evento, Usuario? quem = null, Curso? curso = null) =>
        PremioParticipacao.ConcederPelaInstituicao(CatalogoDeTeste.P06, 2, _ana, curso ?? _cc, evento, null, _anterior, quem ?? _coordenacao, _agora);

    [Fact]
    public void Nasce_valido_aprovado_pela_instituicao_pontuando_e_com_a_janela_aberta()
    {
        var premio = Conceder(Semana(_cc)).Premio!;

        Assert.Equal(EstadoDoPremioParticipacao.Valido, premio.Estado);
        Assert.Equal((_coordenacao.Id, _agora), (premio.AprovadoPorId, premio.AprovadoEm));
        Assert.Equal(_agora + PremioParticipacao.JanelaDeContestacao, premio.FimDaJanela);
        Assert.True(premio.Pontua);
        Assert.Equal(("P06", 2, 30), (premio.ItemCodigo, premio.VersaoDoItem, premio.Pontos));
    }

    [Fact]
    public void E_contestavel_e_vira_definitivo_no_fim_da_janela_como_qualquer_aprovado()
    {
        var contestado = Conceder(Semana()).Premio!;
        Assert.NotNull(contestado.Contestar(_coordenacao, "Não estava no evento.", _agora.AddDays(1)).Contestacao);

        var esquecido = Conceder(Semana()).Premio!;
        Assert.True(esquecido.TornarDefinitivoSeAJanelaFechou(_agora + PremioParticipacao.JanelaDeContestacao));
    }

    [Fact]
    public void Valem_as_regras_da_reivindicacao_de_evento_curso_e_ex_aluno()
    {
        Assert.Equal(RecusaDaReivindicacao.EventoNaoEncontrado, Conceder(Semana(_adm)).Recusa);
        Assert.Equal(RecusaDaReivindicacao.EventoNaoComecou, Conceder(Semana(inicio: Data(11, 20))).Recusa);
        Assert.Equal(RecusaDaReivindicacao.EventoDeSemestreEncerrado, Conceder(Semana(inicio: Data(6, 1))).Recusa);

        _ana.MarcarExAluno(_coordenacao.Id, _agora.AddDays(-1));
        Assert.Equal(RecusaDaReivindicacao.ExAluno, Conceder(Semana()).Recusa);
    }

    [Fact]
    public void O_evento_digitado_e_da_instituicao_e_nao_do_aluno()
    {
        var daInstituicao = Evento.TextoLivre("Monitoria de Cálculo", Data(10, 1), Data(10, 30), _coordenacao);
        var doAluno = Evento.TextoLivre("Monitoria de Cálculo", Data(10, 1), Data(10, 30), _ana);

        Assert.NotNull(PremioParticipacao.ConcederPelaInstituicao(
            CatalogoDeTeste.P01, 1, _ana, _cc, daInstituicao, null, _anterior, _coordenacao, _agora).Premio);
        Assert.Throws<DomainException>(() => PremioParticipacao.ConcederPelaInstituicao(
            CatalogoDeTeste.P01, 1, _ana, _cc, doAluno, null, _anterior, _coordenacao, _agora));
    }

    [Fact]
    public void So_a_instituicao_concede_e_so_aluno_recebe()
    {
        var renata = Usuario.CriarPeloSeed(CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), "renataA", _agora);

        Assert.Throws<DomainException>(() => Conceder(Semana(), quem: renata));
        Assert.Throws<DomainException>(() => PremioParticipacao.ConcederPelaInstituicao(
            CatalogoDeTeste.P06, 2, renata, null, Semana(), null, _anterior, _coordenacao, _agora));
    }

    [Fact]
    public void A_notificacao_de_premio_concedido_vai_para_o_aluno()
    {
        var premio = Conceder(Semana()).Premio!;

        var notificacao = Notificacao.PremioConcedido(premio, _agora);

        Assert.Equal((_ana.Id, TipoDeNotificacao.PremioConcedido, premio.Id), (notificacao.UsuarioId, notificacao.Tipo, notificacao.ReferenciaId));
    }

    [Fact]
    public void O_aluno_posta_o_premio_concedido_so_enquanto_ele_pontua()
    {
        var premio = Conceder(Semana()).Premio!;

        var post = Post.Publicar(_ana, null, null, null, null, premio, _agora).Post!;
        Assert.Equal(premio.Id, post.PremioParticipacaoId);
        Assert.False(post.TemFoto);

        premio.Contestar(_coordenacao, "Conferir a lista de presença.", _agora.AddHours(1));
        Assert.Equal(RecusaDaPublicacao.PremioIndisponivel, Post.Publicar(_ana, "Valeu!", null, null, null, premio, _agora).Recusa);
    }

    [Fact]
    public void So_o_dono_posta_o_premio_concedido()
    {
        var premio = Conceder(Semana()).Premio!;
        var bruno = Usuario.CadastrarAluno(
            CadastroElegivel.Aluno("20260102", "bruno.lima@al.unieduk.com.br", "Bruno Lima", _cc, 3), _cc, "Bruno", "brunoLima", "hash", _agora);

        Assert.Equal(RecusaDaPublicacao.PremioNaoEncontrado, Post.Publicar(bruno, null, null, null, null, premio, _agora).Recusa);
    }
}
