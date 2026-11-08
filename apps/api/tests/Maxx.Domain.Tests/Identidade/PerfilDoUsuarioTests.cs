using Maxx.Domain.Academico;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Identidade;

// Editar perfil (produto.md 6.5): foto, nome de exibição e a parte do arroba antes do hífen.
public class PerfilDoUsuarioTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);

    private readonly Usuario _ana = Usuario.CadastrarAluno(
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3), _cc, "Ana Souza", "anaSouza", "hash", _agora);

    private static UploadPendente FotoDe(Usuario usuario, FinalidadeDoUpload finalidade = FinalidadeDoUpload.FotoDePerfil) =>
        UploadPendente.Pedir(usuario, finalidade, "image/jpeg", 1000, 500, _agora).Upload!;

    [Fact]
    public void Sem_foto_o_avatar_sao_as_iniciais()
    {
        Assert.False(_ana.TemFoto);
        Assert.Null(_ana.ChaveDaFotoDoFeed);
    }

    [Fact]
    public void Primeira_foto_confirma_o_upload_e_nao_deixa_foto_antiga()
    {
        var foto = FotoDe(_ana);

        var troca = _ana.TrocarFoto(foto, _agora.AddMinutes(1));

        Assert.Null(troca.Recusa);
        Assert.Empty(troca.ChavesAntigas);
        Assert.Equal((foto.ChaveDoFeed, foto.ChaveDaGrade), (_ana.ChaveDaFotoDoFeed, _ana.ChaveDaFotoDaGrade));
        Assert.Equal(_agora.AddMinutes(1), foto.ConfirmadoEm);
    }

    [Fact]
    public void Trocar_a_foto_devolve_as_duas_chaves_da_antiga()
    {
        var antiga = FotoDe(_ana);
        _ana.TrocarFoto(antiga, _agora);

        var troca = _ana.TrocarFoto(FotoDe(_ana), _agora);

        Assert.Equal([antiga.ChaveDoFeed, antiga.ChaveDaGrade], troca.ChavesAntigas);
    }

    [Fact]
    public void Foto_de_outra_pessoa_de_post_ou_ja_usada_nao_serve_e_nada_muda()
    {
        var bruno = Usuario.CadastrarAluno(
            CadastroElegivel.Aluno("20260102", "bruno.lima@al.unieduk.com.br", "Bruno Lima", _cc, 3), _cc, "Bruno", "brunoLima", "hash", _agora);
        var usada = FotoDe(_ana);
        _ana.TrocarFoto(usada, _agora);

        Assert.Equal(RecusaDaTrocaDeFoto.UploadNaoEncontrado, _ana.TrocarFoto(FotoDe(bruno), _agora).Recusa);
        Assert.Equal(RecusaDaTrocaDeFoto.UploadNaoEncontrado, _ana.TrocarFoto(FotoDe(_ana, FinalidadeDoUpload.Post), _agora).Recusa);
        Assert.Equal(RecusaDaTrocaDeFoto.UploadJaUsado, _ana.TrocarFoto(usada, _agora).Recusa);
        Assert.Equal(usada.ChaveDoFeed, _ana.ChaveDaFotoDoFeed);
    }

    [Fact]
    public void Remover_a_foto_volta_as_iniciais_e_devolve_as_chaves()
    {
        var foto = FotoDe(_ana);
        _ana.TrocarFoto(foto, _agora);

        Assert.Equal([foto.ChaveDoFeed, foto.ChaveDaGrade], _ana.RemoverFoto());
        Assert.False(_ana.TemFoto);
        Assert.Empty(_ana.RemoverFoto());
    }

    [Fact]
    public void Nome_de_exibicao_segue_a_regra_do_cadastro()
    {
        _ana.EditarNomeDeExibicao("  Aninha  ");

        Assert.Equal("Aninha", _ana.NomeDeExibicao);
        Assert.Throws<DomainException>(() => _ana.EditarNomeDeExibicao("   "));
        Assert.Throws<DomainException>(() => _ana.EditarNomeDeExibicao(new string('a', 61)));
        Assert.Equal("Aninha", _ana.NomeDeExibicao);
    }
}
