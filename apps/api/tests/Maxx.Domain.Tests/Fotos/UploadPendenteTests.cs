using Maxx.Domain.Academico;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Fotos;

public class UploadPendenteTests
{
    private const long _feed = 300 * 1024;
    private const long _grade = 40 * 1024;

    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);

    private static Usuario Aluna() => Usuario.CadastrarAluno(
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3), _cc, "Ana", "anaSouza", "hash", _agora);

    private static Usuario ExAluna()
    {
        var aluna = Aluna();
        aluna.MarcarExAluno(_agora);
        return aluna;
    }

    private static Usuario Professora() =>
        Usuario.CriarPeloSeed(CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), "renataA", _agora);

    private static Usuario ProfessoraEInstituicao() => Usuario.CriarPeloSeed(
        CadastroElegivel.Professor("marta@prof.unieduk.com.br", "Marta Lima", tambemInstituicao: true), "martaL", _agora);

    private static Usuario Instituicao() =>
        Usuario.CriarPeloSeed(CadastroElegivel.Instituicao("coordenacao@unieduk.com.br", "Coordenação UniMax"), "coordenacao", _agora);

    [Fact]
    public void Pedido_valido_registra_duas_chaves_aleatorias_com_o_tipo_e_os_tamanhos()
    {
        var aluna = Aluna();

        var upload = UploadPendente.Pedir(aluna, FinalidadeDoUpload.Post, "image/jpeg", _feed, _grade, _agora).Upload!;

        Assert.Equal(aluna.Id, upload.UsuarioId);
        Assert.Equal(FinalidadeDoUpload.Post, upload.Finalidade);
        Assert.Equal("image/jpeg", upload.TipoDeConteudo);
        Assert.Equal((_feed, _grade), (upload.TamanhoDoFeed, upload.TamanhoDaGrade));
        Assert.Equal(_agora, upload.CriadoEm);
        Assert.False(upload.EstaConfirmado);

        // As duas derivadas sob o mesmo ULID, cada uma com o próprio nome.
        Assert.Matches("^[0-7][0-9A-HJKMNP-TV-Z]{25}/feed\\.jpg$", upload.ChaveDoFeed);
        Assert.Equal(upload.ChaveDoFeed.Replace("/feed.jpg", "/grade.jpg", StringComparison.Ordinal), upload.ChaveDaGrade);
        Assert.Equal([upload.ChaveDoFeed, upload.ChaveDaGrade], upload.Chaves);
    }

    [Fact]
    public void Dois_pedidos_nunca_repetem_a_chave()
    {
        var aluna = Aluna();

        var primeiro = UploadPendente.Pedir(aluna, FinalidadeDoUpload.Post, "image/jpeg", _feed, _grade, _agora).Upload!;
        var segundo = UploadPendente.Pedir(aluna, FinalidadeDoUpload.Post, "image/jpeg", _feed, _grade, _agora).Upload!;

        Assert.NotEqual(primeiro.ChaveDoFeed, segundo.ChaveDoFeed);
    }

    [Fact]
    public void Assinatura_vale_15_minutos_e_a_limpeza_e_24_horas_depois()
    {
        var upload = UploadPendente.Pedir(Aluna(), FinalidadeDoUpload.Post, "image/jpeg", _feed, _grade, _agora).Upload!;

        Assert.Equal(_agora.AddMinutes(15), upload.AssinaturaExpiraEm);
        Assert.Equal(_agora.AddHours(24), upload.LimparEm);
    }

    [Theory]
    [InlineData("IMAGE/JPEG")]
    [InlineData(" image/jpeg ")]
    public void Tipo_e_comparado_sem_caixa_e_gravado_na_forma_canonica(string tipo)
    {
        var upload = UploadPendente.Pedir(Aluna(), FinalidadeDoUpload.Post, tipo, _feed, _grade, _agora).Upload!;

        Assert.Equal("image/jpeg", upload.TipoDeConteudo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/heic")]
    [InlineData("image/jpeg; charset=utf-8")]
    [InlineData("text/html")]
    public void So_jpeg_e_aceito(string? tipo)
    {
        var resultado = UploadPendente.Pedir(Aluna(), FinalidadeDoUpload.Post, tipo, _feed, _grade, _agora);

        Assert.Null(resultado.Upload);
        Assert.Equal(RecusaDoUpload.TipoNaoAceito, resultado.Recusa);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(UploadPendente.TamanhoMaximoDoFeed, UploadPendente.TamanhoMaximoDaGrade)]
    public void Tamanho_vai_de_1_byte_ao_maximo_de_cada_derivada(long feed, long grade) =>
        Assert.NotNull(UploadPendente.Pedir(Aluna(), FinalidadeDoUpload.Post, "image/jpeg", feed, grade, _agora).Upload);

    [Theory]
    [InlineData(0, _grade)]
    [InlineData(-1, _grade)]
    [InlineData(UploadPendente.TamanhoMaximoDoFeed + 1, _grade)]
    [InlineData(_feed, 0)]
    [InlineData(_feed, UploadPendente.TamanhoMaximoDaGrade + 1)]
    public void Tamanho_fora_da_faixa_e_recusado(long feed, long grade)
    {
        var resultado = UploadPendente.Pedir(Aluna(), FinalidadeDoUpload.Post, "image/jpeg", feed, grade, _agora);

        Assert.Null(resultado.Upload);
        Assert.Equal(RecusaDoUpload.TamanhoInvalido, resultado.Recusa);
    }

    [Fact]
    public void Limites_sao_1_MiB_no_feed_e_200_KiB_na_grade()
    {
        Assert.Equal(1_048_576, UploadPendente.TamanhoMaximoDoFeed);
        Assert.Equal(204_800, UploadPendente.TamanhoMaximoDaGrade);
    }

    public static TheoryData<string, FinalidadeDoUpload, bool> Permissoes => new()
    {
        // Post: só aluno ativo (produto.md 2.2 e 6.1).
        { "aluna", FinalidadeDoUpload.Post, true },
        { "ex-aluna", FinalidadeDoUpload.Post, false },
        { "professora", FinalidadeDoUpload.Post, false },
        { "professora e instituição", FinalidadeDoUpload.Post, false },
        { "instituição", FinalidadeDoUpload.Post, false },

        // Foto de perfil: aluno e professor (produto.md 2.3). A ex-aluna mantém o perfil.
        { "aluna", FinalidadeDoUpload.FotoDePerfil, true },
        { "ex-aluna", FinalidadeDoUpload.FotoDePerfil, true },
        { "professora", FinalidadeDoUpload.FotoDePerfil, true },
        { "professora e instituição", FinalidadeDoUpload.FotoDePerfil, true },
        { "instituição", FinalidadeDoUpload.FotoDePerfil, false },
    };

    [Theory]
    [MemberData(nameof(Permissoes))]
    public void Quem_pode_enviar_depende_da_finalidade(string quem, FinalidadeDoUpload finalidade, bool pode)
    {
        var usuario = quem switch
        {
            "aluna" => Aluna(),
            "ex-aluna" => ExAluna(),
            "professora" => Professora(),
            "professora e instituição" => ProfessoraEInstituicao(),
            _ => Instituicao(),
        };

        var resultado = UploadPendente.Pedir(usuario, finalidade, "image/jpeg", _feed, _grade, _agora);

        Assert.Equal(pode, resultado.Upload is not null);
        Assert.Equal(pode ? null : RecusaDoUpload.FinalidadeNaoPermitida, resultado.Recusa);
    }

    [Fact]
    public void Permissao_e_conferida_antes_do_tipo_e_do_tamanho()
    {
        var resultado = UploadPendente.Pedir(Instituicao(), FinalidadeDoUpload.Post, "text/html", 0, 0, _agora);

        Assert.Equal(RecusaDoUpload.FinalidadeNaoPermitida, resultado.Recusa);
    }
}
