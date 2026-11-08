using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;
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
        _ana.MarcarExAluno(_agora.AddDays(-1));

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
