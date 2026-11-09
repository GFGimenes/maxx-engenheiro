using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Domain.Tests.Catalogo;
using static Maxx.Domain.Tests.Academico.Calendario2026;

namespace Maxx.Domain.Tests.Premios;

public class PremioParticipacaoTests
{
    // 10/11/2026, 10h em São Paulo: em 2026/2, e 2026/1 é o último semestre encerrado.
    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);
    private static readonly Curso _adm = new("Administração", "ADM", 8);
    private static readonly Materia _bancoDeDados = new(_cc, "Banco de Dados");
    private static readonly SemestreLetivo _anterior = Primeiro();

    private readonly Usuario _ana = Aluno("20260101", "ana.souza", "anaSouza");

    private static Usuario Aluno(string ra, string email, string prefixo, Curso? curso = null) => Usuario.CadastrarAluno(
        CadastroElegivel.Aluno(ra, $"{email}@al.unieduk.com.br", prefixo, curso ?? _cc, 3), curso ?? _cc, prefixo, prefixo, "hash", _agora.AddDays(-90));

    private static Evento Semana(Curso? curso = null, DateOnly? inicio = null) => Evento.Semeado(
        "semana-academica-cc-2026-2", "Semana Acadêmica", inicio ?? Data(9, 14), Data(9, 18), [CatalogoDeTeste.P06, CatalogoDeTeste.P01], curso);

    private Evento Livre(DateOnly? inicio = null, Usuario? autor = null) =>
        Evento.TextoLivre("Palestra sobre LGPD", inicio ?? Data(10, 1), inicio ?? Data(10, 1), autor ?? _ana);

    private ResultadoDaReivindicacao Reivindicar(
        ItemPremioParticipacao item,
        Evento evento,
        Usuario? aluno = null,
        Curso? curso = null,
        Materia? materia = null,
        DateTimeOffset? agora = null) =>
        PremioParticipacao.Reivindicar(item, 2, aluno ?? _ana, curso ?? _cc, evento, materia, _anterior, agora ?? _agora);

    [Fact]
    public void Reivindicacao_congela_tier_pontos_e_versao_e_nasce_reivindicada_sem_pontuar()
    {
        var evento = Semana(_cc);

        var premio = Reivindicar(CatalogoDeTeste.P06, evento).Premio!;

        Assert.Equal(_ana.Id, premio.AlunoId);
        Assert.Equal(("P06", 2, Tier.Bronze, 30), (premio.ItemCodigo, premio.VersaoDoItem, premio.Tier, premio.Pontos));
        Assert.Equal(evento.Id, premio.EventoId);
        Assert.Null(premio.MateriaId);
        Assert.Equal(EstadoDoPremioParticipacao.Reivindicado, premio.Estado);
        Assert.Equal(_agora, premio.ReivindicadoEm);
        Assert.False(premio.Pontua);
        Assert.True(premio.SeguraAChave);
        Assert.True(premio.PrendeOPost);
    }

    [Fact]
    public void Com_evento_semeado_a_chave_e_aluno_e_evento()
    {
        var evento = Semana();

        var premio = Reivindicar(CatalogoDeTeste.P06, evento).Premio!;

        Assert.Equal($"aluno:{_ana.Id}|evento:{evento.Id}", premio.ChaveDeDeduplicacao);
    }

    [Fact]
    public void Com_evento_digitado_a_chave_degrada_para_aluno_item_e_data_de_inicio()
    {
        var premio = Reivindicar(CatalogoDeTeste.P01, Evento.TextoLivre("Semana de Inovação", Data(10, 5), Data(10, 9), _ana)).Premio!;

        Assert.Equal($"aluno:{_ana.Id}|item:P01|data:2026-10-05", premio.ChaveDeDeduplicacao);
    }

    [Fact]
    public void Dois_eventos_digitados_do_mesmo_item_na_mesma_data_dao_a_mesma_chave()
    {
        var primeiro = Reivindicar(CatalogoDeTeste.P01, Livre()).Premio!;
        var segundo = Reivindicar(CatalogoDeTeste.P01, Evento.TextoLivre("Outro nome", Data(10, 1), Data(10, 3), _ana)).Premio!;

        Assert.Equal(primeiro.ChaveDeDeduplicacao, segundo.ChaveDeDeduplicacao);
    }

    [Fact]
    public void Apresentacao_em_sala_deduplica_por_aluno_item_data_e_materia()
    {
        var premio = Reivindicar(CatalogoDeTeste.P17, Livre(), materia: _bancoDeDados).Premio!;

        Assert.Equal($"aluno:{_ana.Id}|item:P17|data:2026-10-01|materia:{_bancoDeDados.Id}", premio.ChaveDeDeduplicacao);
        Assert.Equal(_bancoDeDados.Id, premio.MateriaId);
    }

    [Fact]
    public void Apresentacao_em_sala_sem_materia_e_recusada_e_outro_item_com_materia_tambem()
    {
        Assert.Equal(RecusaDaReivindicacao.MateriaObrigatoria, Reivindicar(CatalogoDeTeste.P17, Livre()).Recusa);
        Assert.Equal(RecusaDaReivindicacao.MateriaNaoSeAplica, Reivindicar(CatalogoDeTeste.P01, Livre(), materia: _bancoDeDados).Recusa);
    }

    [Fact]
    public void Evento_semeado_que_nao_aceita_o_item_e_recusado()
    {
        Assert.Equal(RecusaDaReivindicacao.EventoNaoAceitaOItem, Reivindicar(CatalogoDeTeste.P17, Semana(), materia: _bancoDeDados).Recusa);
    }

    [Fact]
    public void Evento_de_outro_curso_e_como_se_nao_existisse()
    {
        var gabriel = Aluno("20260201", "gabriel.martins", "gabrielMartins", _adm);

        Assert.Equal(RecusaDaReivindicacao.EventoNaoEncontrado, Reivindicar(CatalogoDeTeste.P06, Semana(_cc), gabriel, _adm).Recusa);
    }

    [Fact]
    public void Evento_que_nao_comecou_ou_de_semestre_encerrado_e_recusado()
    {
        Assert.Equal(RecusaDaReivindicacao.EventoNaoComecou, Reivindicar(CatalogoDeTeste.P01, Livre(Data(11, 20))).Recusa);
        Assert.Equal(RecusaDaReivindicacao.EventoDeSemestreEncerrado, Reivindicar(CatalogoDeTeste.P01, Livre(Data(6, 27))).Recusa);
    }

    [Fact]
    public void Ex_aluno_nao_reivindica()
    {
        _ana.MarcarExAluno(_ana.Id, _agora.AddDays(-1));

        Assert.Equal(RecusaDaReivindicacao.ExAluno, Reivindicar(CatalogoDeTeste.P01, Livre()).Recusa);
    }

    [Fact]
    public void Item_restrito_a_outro_curso_e_recusado()
    {
        var soAdm = CatalogoDeTeste.P01 with { RestricaoCurso = "ADM" };

        Assert.Equal(RecusaDaReivindicacao.ItemDeOutroCurso, Reivindicar(soAdm, Livre()).Recusa);
    }

    [Fact]
    public void Vigencia_do_item_vale_na_data_do_evento()
    {
        var desdeOutubro = CatalogoDeTeste.P01 with { Vigencia = new Vigencia(Data(10, 2), null) };

        Assert.Equal(RecusaDaReivindicacao.ItemForaDeVigencia, Reivindicar(desdeOutubro, Livre(Data(10, 1))).Recusa);
        Assert.Null(Reivindicar(desdeOutubro, Livre(Data(10, 2))).Recusa);
    }

    [Fact]
    public void Evento_digitado_por_outra_pessoa_nao_serve()
    {
        var bruno = Aluno("20260102", "bruno.lima", "brunoLima");

        Assert.Throws<DomainException>(() => Reivindicar(CatalogoDeTeste.P01, Livre(autor: bruno)));
    }

    [Fact]
    public void Professor_nao_reivindica()
    {
        var renata = Usuario.CriarPeloSeed(CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), "renataA", _agora);

        Assert.Throws<DomainException>(() => Reivindicar(CatalogoDeTeste.P01, Livre(autor: renata), renata));
    }

    [Fact]
    public void Comprovacao_guarda_premio_aluno_evento_post_e_o_instante_da_reivindicacao()
    {
        var premio = Reivindicar(CatalogoDeTeste.P06, Semana()).Premio!;
        var postId = Guid.NewGuid();

        var comprovacao = Comprovacao.De(premio, postId);

        Assert.Equal(
            (premio.Id, _ana.Id, premio.EventoId, (Guid?)postId, _agora),
            (comprovacao.PremioParticipacaoId, comprovacao.AlunoId, comprovacao.EventoId, comprovacao.PostId, comprovacao.CriadaEm));
    }

    [Fact]
    public void Post_de_reivindicacao_tem_foto_e_o_premio_e_confirma_o_upload()
    {
        var premio = Reivindicar(CatalogoDeTeste.P06, Semana()).Premio!;
        var upload = UploadPendente.Pedir(_ana, FinalidadeDoUpload.Post, "image/jpeg", 1000, 500, _agora).Upload!;

        var post = Post.Reivindicar(_ana, " Na semana acadêmica ", upload, premio, _agora).Post!;

        Assert.Equal((premio.Id, (Guid?)null, "Na semana acadêmica"), (post.PremioParticipacaoId!.Value, post.MedalhaId, post.Legenda));
        Assert.True(post.TemFoto);
        Assert.True(post.TemPremio);
        Assert.Equal(_agora, upload.ConfirmadoEm);
    }

    [Fact]
    public void Post_de_reivindicacao_sem_foto_e_recusado_e_o_premio_de_outro_tambem()
    {
        var premio = Reivindicar(CatalogoDeTeste.P06, Semana()).Premio!;
        var bruno = Aluno("20260102", "bruno.lima", "brunoLima");
        var doBruno = UploadPendente.Pedir(bruno, FinalidadeDoUpload.Post, "image/jpeg", 1000, 500, _agora).Upload!;

        Assert.Equal(RecusaDaPublicacao.FotoObrigatoria, Post.Reivindicar(_ana, "legenda", null, premio, _agora).Recusa);
        Assert.Equal(RecusaDaPublicacao.PremioNaoEncontrado, Post.Reivindicar(bruno, null, doBruno, premio, _agora).Recusa);
    }

    private static Usuario Renata(bool tambemInstituicao = false) => Usuario.CriarPeloSeed(
        CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque", tambemInstituicao), "renataA", _agora.AddDays(-90));

    private static Usuario Coordenacao() =>
        Usuario.CriarPeloSeed(CadastroElegivel.Instituicao("coordenacao@unieduk.com.br", "Coordenação UniMax"), "coordenacao", _agora.AddDays(-90));

    private PremioParticipacao Pendente() => Reivindicar(CatalogoDeTeste.P06, Semana()).Premio!;

    [Fact]
    public void Aprovado_fica_valido_pontua_grava_quem_e_quando_e_abre_a_janela_de_15_dias()
    {
        var premio = Pendente();
        var renata = Renata();

        Assert.Null(premio.Aprovar(_ana, renata, _agora.AddHours(2)));

        Assert.Equal(EstadoDoPremioParticipacao.Valido, premio.Estado);
        Assert.True(premio.Pontua);
        Assert.True(premio.SeguraAChave);
        Assert.True(premio.PrendeOPost);
        Assert.Equal((renata.Id, _agora.AddHours(2)), (premio.AprovadoPorId!.Value, premio.AprovadoEm!.Value));
        Assert.Equal(_agora.AddHours(2).AddDays(15), premio.FimDaJanela);
    }

    [Fact]
    public void A_instituicao_e_a_conta_que_acumula_os_dois_papeis_tambem_aprovam()
    {
        Assert.Null(Pendente().Aprovar(_ana, Coordenacao(), _agora));
        Assert.Null(Pendente().Aprovar(_ana, Renata(tambemInstituicao: true), _agora));
    }

    [Fact]
    public void Vale_quem_chega_primeiro()
    {
        var premio = Pendente();
        premio.Aprovar(_ana, Renata(), _agora);

        Assert.Equal(RecusaDaDecisao.JaDecidida, premio.Aprovar(_ana, Coordenacao(), _agora.AddMinutes(1)));
        Assert.Equal(RecusaDaDecisao.JaDecidida, premio.Reprovar(Coordenacao(), "Foto de outro evento.", _agora.AddMinutes(1)));
        Assert.Equal(EstadoDoPremioParticipacao.Valido, premio.Estado);
    }

    [Fact]
    public void Reprovado_grava_quem_quando_e_o_motivo_e_libera_a_chave()
    {
        var premio = Pendente();
        var coordenacao = Coordenacao();

        Assert.Null(premio.Reprovar(coordenacao, "  A foto não mostra o evento.  ", _agora));

        Assert.Equal(EstadoDoPremioParticipacao.Reprovado, premio.Estado);
        Assert.Equal((coordenacao.Id, _agora, "A foto não mostra o evento."), (premio.ReprovadoPorId!.Value, premio.ReprovadoEm!.Value, premio.MotivoDaReprovacao));
        Assert.False(premio.Pontua);
        Assert.False(premio.SeguraAChave);
        Assert.False(premio.PrendeOPost);
        Assert.Null(premio.FimDaJanela);
        Assert.Equal(RecusaDaDecisao.JaDecidida, premio.Aprovar(_ana, Renata(), _agora));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Reprovar_sem_motivo_e_recusado(string? motivo)
    {
        var premio = Pendente();

        Assert.Equal(RecusaDaDecisao.MotivoInvalido, premio.Reprovar(Renata(), motivo, _agora));
        Assert.Equal(EstadoDoPremioParticipacao.Reivindicado, premio.Estado);
    }

    [Fact]
    public void Motivo_vai_ate_500_caracteres_contando_emoji_como_um()
    {
        var limite = new string('a', 499) + "\U0001F4F7";

        Assert.Equal(limite, Motivo.Normalizar(limite));
        Assert.Null(Motivo.Normalizar(limite + "a"));
        Assert.Equal(RecusaDaDecisao.MotivoInvalido, Pendente().Reprovar(Renata(), limite + "a", _agora));
    }

    [Fact]
    public void Ex_aluno_nao_tem_a_comprovacao_aprovada_mas_pode_ter_reprovada()
    {
        var premio = Pendente();
        _ana.MarcarExAluno(_ana.Id, _agora);

        Assert.Equal(RecusaDaDecisao.ExAluno, premio.Aprovar(_ana, Renata(), _agora));
        Assert.Null(premio.Reprovar(Renata(), "Ex-aluno não recebe prêmio.", _agora));
    }

    [Fact]
    public void Aluno_nao_decide_comprovacao()
    {
        var bruno = Aluno("20260102", "bruno.lima", "brunoLima");

        Assert.Throws<DomainException>(() => Pendente().Aprovar(_ana, bruno, _agora));
        Assert.Throws<DomainException>(() => Pendente().Reprovar(bruno, "Não.", _agora));
    }

    [Fact]
    public void Fica_definitivo_so_quando_a_janela_fecha_e_so_se_ainda_valido()
    {
        var premio = Pendente();
        Assert.False(premio.TornarDefinitivoSeAJanelaFechou(_agora.AddDays(30)));

        premio.Aprovar(_ana, Renata(), _agora);
        Assert.False(premio.TornarDefinitivoSeAJanelaFechou(_agora.AddDays(15).AddSeconds(-1)));
        Assert.True(premio.TornarDefinitivoSeAJanelaFechou(_agora.AddDays(15)));

        Assert.Equal((EstadoDoPremioParticipacao.Definitivo, _agora.AddDays(15)), (premio.Estado, premio.DefinitivoEm!.Value));
        Assert.True(premio.Pontua);
        Assert.False(premio.PrendeOPost);
        Assert.False(premio.TornarDefinitivoSeAJanelaFechou(_agora.AddDays(16)));
    }

    [Fact]
    public void A_marca_da_fila_vem_depois_de_7_vezes_24_horas()
    {
        Assert.False(Comprovacao.PassouDoPrazo(_agora, _agora.AddDays(7)));
        Assert.True(Comprovacao.PassouDoPrazo(_agora, _agora.AddDays(7).AddSeconds(1)));
    }

    [Fact]
    public void Notificacao_de_aprovacao_e_de_reprovacao_vao_para_o_aluno()
    {
        var aprovado = Pendente();
        aprovado.Aprovar(_ana, Renata(), _agora);
        var reprovado = Pendente();
        reprovado.Reprovar(Renata(), "Outro evento.", _agora);

        var aprovacao = Notificacao.ComprovacaoAprovada(aprovado, _agora);
        var reprovacao = Notificacao.ComprovacaoReprovada(reprovado, _agora);

        Assert.Equal((_ana.Id, aprovado.Id, TipoDeNotificacao.ComprovacaoAprovada), (aprovacao.UsuarioId, aprovacao.ReferenciaId, aprovacao.Tipo));
        Assert.Equal((_ana.Id, reprovado.Id, TipoDeNotificacao.ComprovacaoReprovada), (reprovacao.UsuarioId, reprovacao.ReferenciaId, reprovacao.Tipo));
        Assert.Throws<DomainException>(() => Notificacao.ComprovacaoAprovada(Pendente(), _agora));
        Assert.Throws<DomainException>(() => Notificacao.ComprovacaoReprovada(aprovado, _agora));
    }

    private PremioParticipacao Aprovado()
    {
        var premio = Pendente();
        premio.Aprovar(_ana, Renata(), _agora);
        return premio;
    }

    [Fact]
    public void Contestado_vai_a_averiguacao_para_de_pontuar_e_prende_o_post()
    {
        var premio = Aprovado();
        var renata = Renata();

        var resultado = premio.Contestar(renata, "  A foto é de outro evento.  ", _agora.AddDays(3));

        var contestacao = Assert.IsType<Contestacao>(resultado.Contestacao);
        Assert.Equal(
            (premio.Id, renata.Id, _agora.AddDays(3), "A foto é de outro evento.", true),
            (contestacao.PremioParticipacaoId, contestacao.ContestadaPorId, contestacao.ContestadaEm, contestacao.Motivo, contestacao.EstaAberta));
        Assert.Equal((EstadoDoPremioParticipacao.EmAveriguacao, _agora.AddDays(3)), (premio.Estado, premio.ContestadoEm!.Value));
        Assert.False(premio.Pontua);
        Assert.True(premio.PrendeOPost);
        Assert.True(premio.SeguraAChave);

        // Contestado, o fim da janela não o torna definitivo: quem decide é a instituição.
        Assert.False(premio.TornarDefinitivoSeAJanelaFechou(_agora.AddDays(30)));
    }

    [Fact]
    public void Quem_aprovou_tambem_contesta_e_a_instituicao_tambem()
    {
        var renata = Renata();
        var premio = Pendente();
        premio.Aprovar(_ana, renata, _agora);

        Assert.NotNull(premio.Contestar(renata, "Revi a foto.", _agora).Contestacao);
        Assert.NotNull(Aprovado().Contestar(Coordenacao(), "Revi a foto.", _agora).Contestacao);
    }

    [Fact]
    public void Uma_contestacao_por_premio_mesmo_depois_de_confirmado()
    {
        var premio = Aprovado();
        var contestacao = premio.Contestar(Renata(), "Revi a foto.", _agora).Contestacao!;

        Assert.Equal(RecusaDaContestacao.JaContestado, premio.Contestar(Coordenacao(), "De novo.", _agora).Recusa);

        premio.Confirmar(contestacao, Coordenacao(), _agora.AddDays(1));
        Assert.Equal(RecusaDaContestacao.JaContestado, premio.Contestar(Coordenacao(), "De novo.", _agora.AddDays(2)).Recusa);
    }

    [Fact]
    public void So_dentro_da_janela_e_so_o_premio_aprovado()
    {
        var premio = Aprovado();

        Assert.Equal(RecusaDaContestacao.ForaDaJanela, premio.Contestar(Renata(), "Tarde.", _agora.AddDays(15)).Recusa);
        Assert.NotNull(premio.Contestar(Renata(), "No limite.", _agora.AddDays(15).AddSeconds(-1)).Contestacao);
        Assert.Equal(RecusaDaContestacao.NaoContestavel, Pendente().Contestar(Renata(), "Pendente.", _agora).Recusa);

        var definitivo = Aprovado();
        definitivo.TornarDefinitivoSeAJanelaFechou(_agora.AddDays(15));
        Assert.Equal(RecusaDaContestacao.ForaDaJanela, definitivo.Contestar(Coordenacao(), "Definitivo.", _agora.AddDays(15)).Recusa);
    }

    [Fact]
    public void Contestar_sem_motivo_e_recusado_e_nada_muda()
    {
        var premio = Aprovado();

        Assert.Equal(RecusaDaContestacao.MotivoInvalido, premio.Contestar(Renata(), " ", _agora).Recusa);
        Assert.Equal(EstadoDoPremioParticipacao.Valido, premio.Estado);
        Assert.Null(premio.ContestadoEm);
    }

    [Fact]
    public void Confirmado_fica_definitivo_na_hora_e_volta_a_pontuar()
    {
        var premio = Aprovado();
        var contestacao = premio.Contestar(Renata(), "Revi a foto.", _agora).Contestacao!;
        var coordenacao = Coordenacao();

        Assert.Null(premio.Confirmar(contestacao, coordenacao, _agora.AddDays(2)));

        Assert.Equal((EstadoDoPremioParticipacao.Definitivo, _agora.AddDays(2)), (premio.Estado, premio.DefinitivoEm!.Value));
        Assert.True(premio.Pontua);
        Assert.False(premio.PrendeOPost);
        Assert.Equal((DecisaoDaContestacao.Confirmada, coordenacao.Id, _agora.AddDays(2)), (contestacao.Decisao!.Value, contestacao.DecididaPorId!.Value, contestacao.DecididaEm!.Value));
        Assert.Equal(RecusaDaDecisao.JaDecidida, premio.Confirmar(contestacao, coordenacao, _agora.AddDays(3)));
        Assert.Equal(RecusaDaDecisao.JaDecidida, premio.Revogar(coordenacao, "Tarde.", contestacao, _agora.AddDays(3)));
    }

    [Fact]
    public void Revogado_pela_contestacao_sai_da_pontuacao_e_fica_no_historico_com_o_motivo()
    {
        var premio = Aprovado();
        var contestacao = premio.Contestar(Renata(), "Revi a foto.", _agora).Contestacao!;
        var coordenacao = Coordenacao();

        Assert.Null(premio.Revogar(coordenacao, " Foto de outro evento. ", contestacao, _agora.AddDays(2)));

        Assert.Equal(
            (EstadoDoPremioParticipacao.Revogado, coordenacao.Id, _agora.AddDays(2), "Foto de outro evento."),
            (premio.Estado, premio.RevogadoPorId!.Value, premio.RevogadoEm!.Value, premio.MotivoDaRevogacao));
        Assert.False(premio.Pontua);
        Assert.False(premio.PrendeOPost);

        // Revogado segura a chave: não se reivindica de novo o mesmo evento (D5).
        Assert.True(premio.SeguraAChave);
        Assert.Equal(DecisaoDaContestacao.Revogada, contestacao.Decisao);
    }

    [Fact]
    public void Revogar_exige_motivo_e_em_averiguacao_a_contestacao_aberta()
    {
        var premio = Aprovado();
        var contestacao = premio.Contestar(Renata(), "Revi a foto.", _agora).Contestacao!;

        Assert.Equal(RecusaDaDecisao.MotivoInvalido, premio.Revogar(Coordenacao(), "", contestacao, _agora));
        Assert.Equal(RecusaDaDecisao.JaDecidida, premio.Revogar(Coordenacao(), "Sem a contestação.", null, _agora));
        Assert.Equal(EstadoDoPremioParticipacao.EmAveriguacao, premio.Estado);
    }

    [Fact]
    public void O_premio_valido_sem_contestacao_tambem_pode_ser_revogado_pela_instituicao()
    {
        var premio = Aprovado();

        Assert.Null(premio.Revogar(Coordenacao(), "Removido pela instituição.", null, _agora));
        Assert.Equal(EstadoDoPremioParticipacao.Revogado, premio.Estado);
        Assert.Equal(RecusaDaDecisao.JaDecidida, Pendente().Revogar(Coordenacao(), "Pendente.", null, _agora));
    }

    [Fact]
    public void So_a_instituicao_decide_contestacao_e_revoga()
    {
        var premio = Aprovado();
        var contestacao = premio.Contestar(Renata(), "Revi a foto.", _agora).Contestacao!;

        Assert.Throws<DomainException>(() => premio.Confirmar(contestacao, Renata(), _agora));
        Assert.Throws<DomainException>(() => premio.Revogar(Renata(), "Não.", contestacao, _agora));
        Assert.Null(premio.Confirmar(contestacao, Renata(tambemInstituicao: true), _agora));
    }

    [Fact]
    public void A_marca_da_fila_de_contestacoes_tambem_vem_depois_de_7_vezes_24_horas()
    {
        Assert.False(Contestacao.PassouDoPrazo(_agora, _agora.AddDays(7)));
        Assert.True(Contestacao.PassouDoPrazo(_agora, _agora.AddDays(7).AddTicks(1)));
    }

    [Fact]
    public void Notificacoes_de_contestacao_decisao_e_revogacao_vao_para_o_aluno()
    {
        var contestado = Aprovado();
        var contestacao = contestado.Contestar(Renata(), "Revi.", _agora).Contestacao!;
        var aberta = Notificacao.ContestacaoAberta(contestado, _agora);
        Assert.Throws<DomainException>(() => Notificacao.DecisaoDeContestacao(contestado, _agora));
        contestado.Confirmar(contestacao, Coordenacao(), _agora);
        var decisao = Notificacao.DecisaoDeContestacao(contestado, _agora);
        var revogado = Aprovado();
        revogado.Revogar(Coordenacao(), "Removido pela instituição.", null, _agora);
        var revogacao = Notificacao.PremioRevogado(revogado, _agora);

        Assert.Equal(
            [TipoDeNotificacao.ContestacaoAberta, TipoDeNotificacao.DecisaoDeContestacao, TipoDeNotificacao.PremioRevogado],
            new[] { aberta, decisao, revogacao }.Select(notificacao => notificacao.Tipo));
        Assert.All(new[] { aberta, decisao, revogacao }, notificacao => Assert.Equal(_ana.Id, notificacao.UsuarioId));
        Assert.Throws<DomainException>(() => Notificacao.ContestacaoAberta(Aprovado(), _agora));
    }

    [Fact]
    public void Post_de_reivindicacao_desvinculado_perde_o_premio()
    {
        var premio = Reivindicar(CatalogoDeTeste.P06, Semana()).Premio!;
        var upload = UploadPendente.Pedir(_ana, FinalidadeDoUpload.Post, "image/jpeg", 1000, 500, _agora).Upload!;
        var post = Post.Reivindicar(_ana, null, upload, premio, _agora).Post!;

        post.DesvincularPremio();

        Assert.Null(post.PremioParticipacaoId);
        Assert.False(post.TemPremio);
    }
}
