using Maxx.Domain.Academico;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;
using Maxx.Domain.Mural;

namespace Maxx.Domain.Tests.Mural;

public class AvisoTests
{
    private const long _feed = 300 * 1024;
    private const long _grade = 40 * 1024;

    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);
    private static readonly Guid _turmaA = Guid.CreateVersion7();
    private static readonly Guid _turmaB = Guid.CreateVersion7();

    private static Usuario Aluna() => Usuario.CadastrarAluno(
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3), _cc, "Ana", "anaSouza", "hash", _agora);

    private static Usuario Professora() =>
        Usuario.CriarPeloSeed(CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), "renataA", _agora);

    private static Usuario ProfessoraEInstituicao() => Usuario.CriarPeloSeed(
        CadastroElegivel.Professor("marta@prof.unieduk.com.br", "Marta Lima", tambemInstituicao: true), "martaL", _agora);

    private static Usuario Instituicao() =>
        Usuario.CriarPeloSeed(CadastroElegivel.Instituicao("coordenacao@unieduk.com.br", "Coordenação UniMax"), "coordenacao", _agora);

    private static UploadPendente Upload(Usuario dono, FinalidadeDoUpload finalidade = FinalidadeDoUpload.Aviso) =>
        UploadPendente.Pedir(dono, finalidade, "image/jpeg", _feed, _grade, _agora).Upload!;

    private static ResultadoDoAviso Publicar(
        Usuario autor,
        EscopoDoAviso escopo,
        IReadOnlyCollection<Guid> alvos,
        string? titulo = "Prova remarcada",
        string? texto = "A prova de Banco de Dados passou para 14/08.",
        UploadPendente? upload = null) =>
        Aviso.Publicar(autor, escopo, alvos, titulo, texto, upload, _agora);

    [Fact]
    public void Professora_publica_para_duas_turmas_num_aviso_so()
    {
        var renata = Professora();

        var aviso = Publicar(renata, EscopoDoAviso.Turma, [_turmaA, _turmaB]).Aviso!;

        Assert.Equal(renata.Id, aviso.AutorId);
        Assert.Equal(EscopoDoAviso.Turma, aviso.Escopo);
        Assert.Equal("Prova remarcada", aviso.Titulo);
        Assert.Equal("A prova de Banco de Dados passou para 14/08.", aviso.Texto);
        Assert.Equal(_agora, aviso.PublicadoEm);
        Assert.Equal([_turmaA, _turmaB], aviso.Turmas.Select(turma => turma.TurmaId));
        Assert.All(aviso.Turmas, turma => Assert.Equal(aviso.Id, turma.AvisoId));
        Assert.Empty(aviso.Cursos);
        Assert.False(aviso.TemFoto);
    }

    [Fact]
    public void Turma_repetida_conta_uma_vez()
    {
        var aviso = Publicar(Professora(), EscopoDoAviso.Turma, [_turmaA, _turmaA]).Aviso!;

        Assert.Equal([_turmaA], aviso.Turmas.Select(turma => turma.TurmaId));
    }

    [Fact]
    public void Instituicao_publica_para_a_faculdade_sem_alvo_e_para_um_ou_mais_cursos()
    {
        var coordenacao = Instituicao();
        var outroCurso = Guid.CreateVersion7();

        var faculdade = Publicar(coordenacao, EscopoDoAviso.Faculdade, []).Aviso!;
        var cursos = Publicar(coordenacao, EscopoDoAviso.Curso, [_cc.Id, outroCurso]).Aviso!;

        Assert.Equal(EscopoDoAviso.Faculdade, faculdade.Escopo);
        Assert.Empty(faculdade.Turmas);
        Assert.Empty(faculdade.Cursos);
        Assert.Equal([_cc.Id, outroCurso], cursos.Cursos.Select(curso => curso.CursoId));
        Assert.Empty(cursos.Turmas);
    }

    public static TheoryData<string, EscopoDoAviso, bool> Permissoes => new()
    {
        // A instituição publica para a faculdade e para curso; o professor, para turma
        // (produto.md 2.1). A conta que acumula os dois publica nos três.
        { "aluna", EscopoDoAviso.Faculdade, false },
        { "aluna", EscopoDoAviso.Curso, false },
        { "aluna", EscopoDoAviso.Turma, false },
        { "professora", EscopoDoAviso.Faculdade, false },
        { "professora", EscopoDoAviso.Curso, false },
        { "professora", EscopoDoAviso.Turma, true },
        { "instituição", EscopoDoAviso.Faculdade, true },
        { "instituição", EscopoDoAviso.Curso, true },
        { "instituição", EscopoDoAviso.Turma, false },
        { "professora e instituição", EscopoDoAviso.Faculdade, true },
        { "professora e instituição", EscopoDoAviso.Curso, true },
        { "professora e instituição", EscopoDoAviso.Turma, true },
    };

    [Theory]
    [MemberData(nameof(Permissoes))]
    public void Quem_publica_depende_do_escopo(string quem, EscopoDoAviso escopo, bool pode)
    {
        var autor = quem switch
        {
            "aluna" => Aluna(),
            "professora" => Professora(),
            "professora e instituição" => ProfessoraEInstituicao(),
            _ => Instituicao(),
        };
        IReadOnlyCollection<Guid> alvos = escopo == EscopoDoAviso.Faculdade ? [] : [Guid.CreateVersion7()];

        var resultado = Publicar(autor, escopo, alvos);

        Assert.Equal(pode, resultado.Aviso is not null);
        Assert.Equal(pode ? null : RecusaDoAviso.AutorNaoPublica, resultado.Recusa);
    }

    [Fact]
    public void Faculdade_com_alvo_e_curso_ou_turma_sem_alvo_sao_recusados()
    {
        Assert.Equal(RecusaDoAviso.AlvosInvalidos, Publicar(Instituicao(), EscopoDoAviso.Faculdade, [_cc.Id]).Recusa);
        Assert.Equal(RecusaDoAviso.AlvosInvalidos, Publicar(Instituicao(), EscopoDoAviso.Curso, []).Recusa);
        Assert.Equal(RecusaDoAviso.AlvosInvalidos, Publicar(Professora(), EscopoDoAviso.Turma, []).Recusa);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("  Prova remarcada  ")]
    [InlineData("Olá 🎉")]
    public void Titulo_de_3_a_120_caracteres_e_aparado(string titulo)
    {
        var aviso = Publicar(Professora(), EscopoDoAviso.Turma, [_turmaA], titulo: titulo).Aviso!;

        Assert.Equal(titulo.Trim(), aviso.Titulo);
    }

    [Fact]
    public void Titulo_de_120_caracteres_contando_emoji_como_um_passa()
    {
        var titulo = string.Concat(Enumerable.Repeat("🎉", Aviso.TamanhoMaximoDoTitulo));

        Assert.NotNull(Publicar(Professora(), EscopoDoAviso.Turma, [_turmaA], titulo: titulo).Aviso);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData(" ab ")]
    public void Titulo_curto_ou_vazio_e_recusado(string? titulo)
    {
        Assert.Equal(
            RecusaDoAviso.TituloInvalido, Publicar(Professora(), EscopoDoAviso.Turma, [_turmaA], titulo: titulo).Recusa);
    }

    [Fact]
    public void Titulo_acima_de_120_caracteres_e_recusado()
    {
        var titulo = new string('a', Aviso.TamanhoMaximoDoTitulo + 1);

        Assert.Equal(
            RecusaDoAviso.TituloInvalido, Publicar(Professora(), EscopoDoAviso.Turma, [_turmaA], titulo: titulo).Recusa);
    }

    [Fact]
    public void Texto_de_1_a_2000_caracteres_e_aparado()
    {
        Assert.Equal("x", Publicar(Professora(), EscopoDoAviso.Turma, [_turmaA], texto: " x ").Aviso!.Texto);

        var limite = new string('a', Aviso.TamanhoMaximoDoTexto);
        Assert.Equal(limite, Publicar(Professora(), EscopoDoAviso.Turma, [_turmaA], texto: limite).Aviso!.Texto);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Texto_vazio_e_recusado(string? texto)
    {
        Assert.Equal(RecusaDoAviso.TextoInvalido, Publicar(Professora(), EscopoDoAviso.Turma, [_turmaA], texto: texto).Recusa);
    }

    [Fact]
    public void Texto_acima_de_2000_caracteres_e_recusado()
    {
        var texto = new string('a', Aviso.TamanhoMaximoDoTexto + 1);

        Assert.Equal(RecusaDoAviso.TextoInvalido, Publicar(Professora(), EscopoDoAviso.Turma, [_turmaA], texto: texto).Recusa);
    }

    [Fact]
    public void Foto_do_autor_para_aviso_fica_no_aviso_e_o_upload_e_confirmado()
    {
        var renata = Professora();
        var upload = Upload(renata);

        var aviso = Publicar(renata, EscopoDoAviso.Turma, [_turmaA], upload: upload).Aviso!;

        Assert.True(aviso.TemFoto);
        Assert.Equal((upload.ChaveDoFeed, upload.ChaveDaGrade), (aviso.ChaveDaFotoDoFeed, aviso.ChaveDaFotoDaGrade));
        Assert.True(upload.EstaConfirmado);
    }

    [Fact]
    public void Upload_alheio_ou_de_outra_finalidade_e_recusado_sem_confirmar()
    {
        var renata = Professora();
        var alheio = Upload(Instituicao());
        var dePerfil = Upload(renata, FinalidadeDoUpload.FotoDePerfil);

        Assert.Equal(
            RecusaDoAviso.UploadNaoEncontrado, Publicar(renata, EscopoDoAviso.Turma, [_turmaA], upload: alheio).Recusa);
        Assert.Equal(
            RecusaDoAviso.UploadNaoEncontrado, Publicar(renata, EscopoDoAviso.Turma, [_turmaA], upload: dePerfil).Recusa);
        Assert.False(alheio.EstaConfirmado);
        Assert.False(dePerfil.EstaConfirmado);
    }

    [Fact]
    public void Upload_ja_usado_e_recusado()
    {
        var renata = Professora();
        var upload = Upload(renata);
        Assert.NotNull(Publicar(renata, EscopoDoAviso.Turma, [_turmaA], upload: upload).Aviso);

        Assert.Equal(RecusaDoAviso.UploadJaUsado, Publicar(renata, EscopoDoAviso.Turma, [_turmaB], upload: upload).Recusa);
    }

    [Fact]
    public void Recusa_nao_confirma_o_upload()
    {
        var renata = Professora();
        var upload = Upload(renata);

        Assert.Equal(
            RecusaDoAviso.TituloInvalido, Publicar(renata, EscopoDoAviso.Turma, [_turmaA], titulo: "ab", upload: upload).Recusa);
        Assert.False(upload.EstaConfirmado);
    }
}
