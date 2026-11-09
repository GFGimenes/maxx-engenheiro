using Maxx.Domain.Academico;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Domain.Tests.Academico;
using Maxx.Domain.Tests.Catalogo;

namespace Maxx.Domain.Tests.Social;

public class PostTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);

    private readonly Usuario _ana = Aluno("20260101", "ana.souza", "anaSouza");
    private readonly Usuario _bruno = Aluno("20260102", "bruno.lima", "brunoLima");

    private static Usuario Aluno(string ra, string email, string prefixo) => Usuario.CadastrarAluno(
        CadastroElegivel.Aluno(ra, $"{email}@al.unieduk.com.br", prefixo, _cc, 3), _cc, prefixo, prefixo, "hash", _agora);

    private static Usuario Professora(bool tambemInstituicao = false) => Usuario.CriarPeloSeed(
        CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque", tambemInstituicao), "renataA", _agora);

    private static Usuario Instituicao() =>
        Usuario.CriarPeloSeed(CadastroElegivel.Instituicao("coordenacao@unieduk.com.br", "Coordenação UniMax"), "coordenacao", _agora);

    private static Medalha MedalhaDe(Usuario aluno)
    {
        var semestre = Calendario2026.Segundo();
        return Medalha.Conceder(
            CatalogoDeTeste.M01, 1, aluno, "CC", new Turma(new Materia(_cc, "Banco de Dados"), semestre), semestre, Professora(), [], _agora).Medalha!;
    }

    private static UploadPendente UploadDe(Usuario usuario, FinalidadeDoUpload finalidade = FinalidadeDoUpload.Post) =>
        UploadPendente.Pedir(usuario, finalidade, "image/jpeg", 1000, 500, _agora).Upload!;

    [Fact]
    public void Post_com_premio_foto_e_legenda_guarda_os_tres_e_confirma_o_upload()
    {
        var medalha = MedalhaDe(_ana);
        var upload = UploadDe(_ana);

        var post = Post.Publicar(_ana, "  Nota 10 em Banco de Dados!  ", upload, medalha, _agora.AddMinutes(3)).Post!;

        Assert.Equal(_ana.Id, post.AutorId);
        Assert.Equal("Nota 10 em Banco de Dados!", post.Legenda);
        Assert.Equal((upload.ChaveDoFeed, upload.ChaveDaGrade), (post.ChaveDaFotoDoFeed, post.ChaveDaFotoDaGrade));
        Assert.Equal([upload.ChaveDoFeed, upload.ChaveDaGrade], post.ChavesDaFoto);
        Assert.Equal(medalha.Id, post.MedalhaId);
        Assert.Equal(_agora.AddMinutes(3), post.CriadoEm);
        Assert.Equal(_agora.AddMinutes(3), upload.ConfirmadoEm);
        Assert.False(post.EstaRemovido);
    }

    [Fact]
    public void Basta_um_dos_tres()
    {
        var soPremio = Post.Publicar(_ana, null, null, MedalhaDe(_ana), _agora).Post!;
        var soFoto = Post.Publicar(_ana, null, UploadDe(_ana), null, _agora).Post!;
        var soLegenda = Post.Publicar(_ana, "Primeiro dia no campus.", null, null, _agora).Post!;

        Assert.True(soPremio.TemPremio && !soPremio.TemFoto && !soPremio.TemLegenda);
        Assert.True(soFoto.TemFoto && !soFoto.TemPremio && !soFoto.TemLegenda);
        Assert.True(soLegenda.TemLegenda && !soLegenda.TemPremio && !soLegenda.TemFoto);
        Assert.Empty(soLegenda.ChavesDaFoto);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Post_vazio_nao_existe(string? legenda)
    {
        var resultado = Post.Publicar(_ana, legenda, null, null, _agora);

        Assert.Null(resultado.Post);
        Assert.Equal(RecusaDaPublicacao.Vazio, resultado.Recusa);
    }

    [Fact]
    public void Legenda_vai_ate_1000_caracteres_contando_emoji_como_um()
    {
        var mil = string.Concat(Enumerable.Repeat("\U0001F393", 1000));

        Assert.NotNull(Post.Publicar(_ana, mil, null, null, _agora).Post);
        Assert.Equal(RecusaDaPublicacao.LegendaLonga, Post.Publicar(_ana, mil + "a", null, null, _agora).Recusa);
    }

    [Fact]
    public void So_aluno_ativo_publica()
    {
        var exAluna = Aluno("20260103", "carla.dias", "carlaDias");
        exAluna.MarcarExAluno(exAluna.Id, _agora);

        foreach (var quem in new[] { exAluna, Professora(), Professora(tambemInstituicao: true), Instituicao() })
        {
            Assert.Equal(RecusaDaPublicacao.AutorNaoPublica, Post.Publicar(quem, "Oi", null, null, _agora).Recusa);
        }
    }

    [Fact]
    public void Upload_de_outra_pessoa_ou_de_foto_de_perfil_nao_serve()
    {
        Assert.Equal(RecusaDaPublicacao.UploadNaoEncontrado, Post.Publicar(_ana, null, UploadDe(_bruno), null, _agora).Recusa);
        Assert.Equal(
            RecusaDaPublicacao.UploadNaoEncontrado,
            Post.Publicar(_ana, null, UploadDe(_ana, FinalidadeDoUpload.FotoDePerfil), null, _agora).Recusa);
    }

    [Fact]
    public void A_mesma_foto_nao_vira_dois_posts()
    {
        var upload = UploadDe(_ana);
        Assert.NotNull(Post.Publicar(_ana, null, upload, null, _agora).Post);

        var segundo = Post.Publicar(_ana, "De novo", upload, null, _agora);

        Assert.Equal(RecusaDaPublicacao.UploadJaUsado, segundo.Recusa);
    }

    [Fact]
    public void Recusado_nao_confirma_o_upload()
    {
        var upload = UploadDe(_ana);

        Post.Publicar(_ana, null, upload, MedalhaDe(_bruno), _agora);

        Assert.False(upload.EstaConfirmado);
    }

    [Fact]
    public void Premio_de_outra_pessoa_ou_anulado_nao_serve()
    {
        var anulada = MedalhaDe(_ana);
        anulada.Anular(Instituicao().Id, _agora);

        Assert.Equal(RecusaDaPublicacao.PremioNaoEncontrado, Post.Publicar(_ana, null, null, MedalhaDe(_bruno), _agora).Recusa);
        Assert.Equal(RecusaDaPublicacao.PremioAnulado, Post.Publicar(_ana, null, null, anulada, _agora).Recusa);
    }

    [Fact]
    public void O_autor_apaga_a_instituicao_remove_e_mais_ninguem()
    {
        var post = Post.Publicar(_ana, "Oi", null, null, _agora).Post!;

        Assert.Equal(ModoDeRemocao.Apagar, post.ModoDeRemocaoPor(_ana));
        Assert.Equal(ModoDeRemocao.RemoverPelaInstituicao, post.ModoDeRemocaoPor(Instituicao()));
        Assert.Equal(ModoDeRemocao.RemoverPelaInstituicao, post.ModoDeRemocaoPor(Professora(tambemInstituicao: true)));
        Assert.Equal(ModoDeRemocao.NaoPermitida, post.ModoDeRemocaoPor(_bruno));
        Assert.Equal(ModoDeRemocao.NaoPermitida, post.ModoDeRemocaoPor(Professora()));
    }

    [Fact]
    public void Ex_aluno_ainda_apaga_o_proprio_post()
    {
        var post = Post.Publicar(_ana, "Oi", null, null, _agora).Post!;
        _ana.MarcarExAluno(_ana.Id, _agora);

        Assert.Equal(ModoDeRemocao.Apagar, post.ModoDeRemocaoPor(_ana));
    }

    [Fact]
    public void Remocao_pela_instituicao_grava_quem_e_quando_e_nao_se_repete()
    {
        var post = Post.Publicar(_ana, "Oi", null, null, _agora).Post!;
        var instituicao = Instituicao();

        post.RemoverPelaInstituicao(instituicao, _agora.AddHours(1));

        Assert.Equal((_agora.AddHours(1), instituicao.Id), (post.RemovidoEm, post.RemovidoPorId));
        Assert.True(post.EstaRemovido);
        Assert.Equal(ModoDeRemocao.NaoPermitida, post.ModoDeRemocaoPor(instituicao));
        Assert.Equal(ModoDeRemocao.NaoPermitida, post.ModoDeRemocaoPor(_ana));
        Assert.Throws<DomainException>(() => post.RemoverPelaInstituicao(instituicao, _agora));
    }

    [Fact]
    public void Quem_nao_e_instituicao_nao_remove()
    {
        var post = Post.Publicar(_ana, "Oi", null, null, _agora).Post!;

        Assert.Throws<DomainException>(() => post.RemoverPelaInstituicao(_bruno, _agora));
        Assert.Throws<DomainException>(() => post.RemoverPelaInstituicao(_ana, _agora));
    }

    [Fact]
    public void Medalha_anulada_desvincula_o_post_que_tem_foto_ou_legenda()
    {
        var comLegenda = Post.Publicar(_ana, "Nota 10!", null, MedalhaDe(_ana), _agora).Post!;
        var comFoto = Post.Publicar(_ana, null, UploadDe(_ana), MedalhaDe(_ana), _agora).Post!;

        comLegenda.DesvincularPremio();
        comFoto.DesvincularPremio();

        Assert.Null(comLegenda.MedalhaId);
        Assert.Null(comFoto.MedalhaId);
        Assert.Equal("Nota 10!", comLegenda.Legenda);
        Assert.True(comFoto.TemFoto);
    }

    [Fact]
    public void Post_so_com_o_premio_nao_e_desvinculado_porque_ficaria_vazio()
    {
        var soPremio = Post.Publicar(_ana, null, null, MedalhaDe(_ana), _agora).Post!;

        Assert.True(soPremio.FicariaVazioSemPremio);
        Assert.Throws<DomainException>(soPremio.DesvincularPremio);
    }
}
