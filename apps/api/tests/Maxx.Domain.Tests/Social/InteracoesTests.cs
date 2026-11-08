using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Social;

namespace Maxx.Domain.Tests.Social;

public class InteracoesTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);

    private readonly Usuario _ana = Aluno("20260101", "anaSouza");
    private readonly Usuario _bruno = Aluno("20260102", "brunoLima");

    private static Usuario Aluno(string ra, string prefixo) => Usuario.CadastrarAluno(
        CadastroElegivel.Aluno(ra, $"{prefixo}@al.unieduk.com.br", prefixo, _cc, 3), _cc, prefixo, prefixo, "hash", _agora);

    private static Usuario ExAluna()
    {
        var carla = Aluno("20260103", "carlaDias");
        carla.MarcarExAluno(_agora);
        return carla;
    }

    private static Usuario Professora(bool tambemInstituicao = false) => Usuario.CriarPeloSeed(
        CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque", tambemInstituicao), "renataA", _agora);

    private static Usuario Instituicao() =>
        Usuario.CriarPeloSeed(CadastroElegivel.Instituicao("coordenacao@unieduk.com.br", "Coordenação UniMax"), "coordenacao", _agora);

    private Post PostDaAna() => Post.Publicar(_ana, "Oi", null, null, _agora).Post!;

    private Post PostRemovido()
    {
        var post = PostDaAna();
        post.RemoverPelaInstituicao(Instituicao(), _agora);
        return post;
    }

    [Fact]
    public void Aluno_ex_aluno_e_professor_interagem_e_a_instituicao_nao()
    {
        Assert.True(Interacao.PodeInteragir(_ana));
        Assert.True(Interacao.PodeInteragir(ExAluna()));
        Assert.True(Interacao.PodeInteragir(Professora()));
        Assert.True(Interacao.PodeInteragir(Professora(tambemInstituicao: true)));
        Assert.False(Interacao.PodeInteragir(Instituicao()));
    }

    [Fact]
    public void Curtida_e_de_quem_curte_no_post()
    {
        var post = PostDaAna();

        var curtida = Curtida.Curtir(_bruno, post, _agora)!;

        Assert.Equal((post.Id, _bruno.Id, _agora), (curtida.PostId, curtida.UsuarioId, curtida.CriadaEm));
        Assert.NotNull(Curtida.Curtir(_ana, post, _agora));
    }

    [Fact]
    public void Instituicao_nao_curte_e_post_removido_nao_recebe_curtida()
    {
        Assert.Null(Curtida.Curtir(Instituicao(), PostDaAna(), _agora));
        Assert.Null(Curtida.Curtir(_bruno, PostRemovido(), _agora));
    }

    [Fact]
    public void Comentario_guarda_o_texto_sem_espacos_nas_pontas()
    {
        var post = PostDaAna();

        var comentario = Comentario.Escrever(_bruno, post, "  Parabéns!  ", _agora).Comentario!;

        Assert.Equal((post.Id, _bruno.Id, "Parabéns!", _agora), (comentario.PostId, comentario.AutorId, comentario.Texto, comentario.CriadoEm));
        Assert.False(comentario.EstaRemovido);
    }

    [Fact]
    public void Comentario_vai_ate_500_caracteres_contando_emoji_como_um()
    {
        var quinhentos = string.Concat(Enumerable.Repeat("\U0001F44F", 500));

        Assert.NotNull(Comentario.Escrever(_bruno, PostDaAna(), quinhentos, _agora).Comentario);
        Assert.Equal(RecusaDoComentario.Longo, Comentario.Escrever(_bruno, PostDaAna(), quinhentos + "!", _agora).Recusa);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void Comentario_vazio_e_recusado(string? texto) =>
        Assert.Equal(RecusaDoComentario.Vazio, Comentario.Escrever(_bruno, PostDaAna(), texto, _agora).Recusa);

    [Fact]
    public void Instituicao_nao_comenta_e_post_removido_nao_recebe_comentario()
    {
        Assert.Equal(RecusaDoComentario.NaoInterage, Comentario.Escrever(Instituicao(), PostDaAna(), "Oi", _agora).Recusa);
        Assert.Equal(RecusaDoComentario.PostRemovido, Comentario.Escrever(_bruno, PostRemovido(), "Oi", _agora).Recusa);
    }

    [Fact]
    public void Comentario_e_apagado_pelo_autor_removido_pela_instituicao_e_o_dono_do_post_nao_apaga()
    {
        var comentario = Comentario.Escrever(_bruno, PostDaAna(), "Oi", _agora).Comentario!;

        Assert.Equal(ModoDeRemocao.Apagar, comentario.ModoDeRemocaoPor(_bruno));
        Assert.Equal(ModoDeRemocao.RemoverPelaInstituicao, comentario.ModoDeRemocaoPor(Instituicao()));
        Assert.Equal(ModoDeRemocao.NaoPermitida, comentario.ModoDeRemocaoPor(_ana));
        Assert.Equal(ModoDeRemocao.NaoPermitida, comentario.ModoDeRemocaoPor(Professora()));
    }

    [Fact]
    public void Remocao_do_comentario_pela_instituicao_grava_quem_e_quando()
    {
        var comentario = Comentario.Escrever(_bruno, PostDaAna(), "Impróprio", _agora).Comentario!;
        var instituicao = Instituicao();

        comentario.RemoverPelaInstituicao(instituicao, _agora.AddHours(1));

        Assert.Equal((_agora.AddHours(1), instituicao.Id), (comentario.RemovidoEm, comentario.RemovidoPorId));
        Assert.Equal(ModoDeRemocao.NaoPermitida, comentario.ModoDeRemocaoPor(instituicao));
        Assert.Throws<DomainException>(() => comentario.RemoverPelaInstituicao(instituicao, _agora));
        Assert.Throws<DomainException>(() => Comentario.Escrever(_bruno, PostDaAna(), "Oi", _agora).Comentario!.RemoverPelaInstituicao(_ana, _agora));
    }

    [Fact]
    public void Aluno_segue_aluno_ativo_ou_ex_aluno()
    {
        var seguimento = Seguimento.Seguir(_ana, _bruno, professorDoSeguido: false, _agora)!;

        Assert.Equal((_ana.Id, _bruno.Id, _agora), (seguimento.SeguidorId, seguimento.SeguidoId, seguimento.CriadoEm));
        Assert.NotNull(Seguimento.Seguir(_ana, ExAluna(), professorDoSeguido: false, _agora));
        Assert.NotNull(Seguimento.Seguir(ExAluna(), _ana, professorDoSeguido: false, _agora));
    }

    [Fact]
    public void Professor_segue_so_os_proprios_alunos()
    {
        Assert.NotNull(Seguimento.Seguir(Professora(), _ana, professorDoSeguido: true, _agora));
        Assert.NotNull(Seguimento.Seguir(Professora(tambemInstituicao: true), _ana, professorDoSeguido: true, _agora));
        Assert.Null(Seguimento.Seguir(Professora(), _ana, professorDoSeguido: false, _agora));
    }

    [Fact]
    public void Ninguem_segue_professor_instituicao_ou_a_si_mesmo_e_a_instituicao_nao_segue()
    {
        Assert.Null(Seguimento.Seguir(_ana, Professora(), professorDoSeguido: false, _agora));
        Assert.Null(Seguimento.Seguir(_ana, Instituicao(), professorDoSeguido: false, _agora));
        Assert.Null(Seguimento.Seguir(_ana, _ana, professorDoSeguido: false, _agora));
        Assert.Null(Seguimento.Seguir(Instituicao(), _ana, professorDoSeguido: true, _agora));
    }
}
