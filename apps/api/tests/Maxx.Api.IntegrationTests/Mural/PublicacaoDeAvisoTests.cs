using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.Mural;
using Maxx.Application.Mural;
using Maxx.Domain.Academico;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;
using Maxx.Domain.Mural;
using Maxx.Domain.Notificacoes;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.Mural.MundoDoMural;

namespace Maxx.Api.IntegrationTests.Mural;

// Publicar aviso (produto.md 7) e a notificação de aviso novo de cada destinatário
// (produto.md 8), contra o Postgres e o bucket reais.
public sealed class PublicacaoDeAvisoTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static readonly Uri _avisos = new("/api/v1/avisos", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Professora_publica_para_duas_turmas_suas_num_aviso_so()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var renata = await mundo.ComoAsync(Renata);

        var resposta = await PublicarAsync(
            renata, new { escopo = "turma", turmaIds = new[] { mundo.BancoDeDados, mundo.Algoritmos }, titulo = " Prova remarcada ", texto = "Passou para 14/08." });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var publicado = (await resposta.Content.ReadFromJsonAsync<AvisoPublicadoResponse>(Ct))!;
        Assert.Equal($"/api/v1/avisos/{publicado.Id}", resposta.Headers.Location?.OriginalString);
        Assert.Equal(("turma", "Prova remarcada", "Passou para 14/08."), (publicado.Escopo, publicado.Titulo, publicado.Texto));
        Assert.Equal(mundo.Relogio.GetUtcNow(), publicado.PublicadoEm);
        Assert.Equal(new[] { mundo.BancoDeDados, mundo.Algoritmos }.Order(), publicado.TurmaIds.Order());
        Assert.Empty(publicado.CursoIds);
        Assert.Null(publicado.Foto);

        // Quem publicou e quando ficam no aviso; os alvos, na tabela do escopo.
        var aviso = Assert.Single(await mundo.LinhasAsync<Aviso>());
        Assert.Equal((publicado.Id, mundo.Id(Renata), EscopoDoAviso.Turma), (aviso.Id, aviso.AutorId, aviso.Escopo));
        Assert.Equal(
            new[] { mundo.BancoDeDados, mundo.Algoritmos }.Order(),
            (await mundo.LinhasAsync<AvisoTurma>()).Where(alvo => alvo.AvisoId == aviso.Id).Select(alvo => alvo.TurmaId).Order());
        Assert.Empty(await mundo.LinhasAsync<AvisoCurso>());

        // A notificação vai para a fila na mesma transação, com o aviso como carga.
        var trabalho = Assert.Single(await mundo.LinhasAsync<Trabalho>());
        Assert.Equal(NotificarAvisoNovo.Tipo, trabalho.Tipo);
        using var carga = JsonDocument.Parse(trabalho.Carga);
        Assert.Equal(aviso.Id, carga.RootElement.GetProperty("avisoId").GetGuid());
    }

    [Fact]
    public async Task Aviso_de_turmas_notifica_cada_aluno_ativo_matriculado_uma_vez()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var renata = await mundo.ComoAsync(Renata);
        var aviso = await PublicadoAsync(renata, new { escopo = "turma", turmaIds = new[] { mundo.BancoDeDados, mundo.Algoritmos }, titulo = "Prova", texto = "Amanhã." });

        mundo.Relogio.Avancar(TimeSpan.FromSeconds(5));
        await mundo.EsvaziarFilaAsync();

        // Ana está nas duas turmas e recebe uma. Bruno, por Algoritmos. Carla é ex-aluna, Eva
        // não tem conta, Diego é de outra turma, e a autora não se notifica.
        var notificacoes = await mundo.LinhasAsync<Notificacao>();
        Assert.Equal(Ordenados(mundo, Ana, Bruno), notificacoes.Select(notificacao => notificacao.UsuarioId).Order());
        Assert.All(notificacoes, notificacao =>
        {
            Assert.Equal((TipoDeNotificacao.AvisoNovo, aviso), (notificacao.Tipo, notificacao.ReferenciaId));
            Assert.Equal(mundo.Relogio.GetUtcNow(), notificacao.CriadaEm);
            Assert.Null(notificacao.LidaEm);
        });
    }

    [Fact]
    public async Task Aviso_da_faculdade_notifica_alunos_ativos_e_professores_mas_nao_a_ex_aluna_nem_o_autor()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        await PublicadoAsync(coordenacao, new { escopo = "faculdade", titulo = "Matrícula até 30/07", texto = "Renove pelo Portal do Aluno." });

        await mundo.EsvaziarFilaAsync();

        Assert.Equal(
            Ordenados(mundo, Ana, Bruno, Diego, Renata, Paulo, Marta),
            (await mundo.LinhasAsync<Notificacao>()).Select(notificacao => notificacao.UsuarioId).Order());
    }

    [Fact]
    public async Task Aviso_de_cursos_notifica_os_alunos_ativos_do_curso_atual()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        await PublicadoAsync(coordenacao, new { escopo = "curso", cursoIds = new[] { mundo.ADS }, titulo = "Semana de ADS", texto = "Inscrições abertas." });
        var resposta = await PublicarAsync(
            coordenacao, new { escopo = "curso", cursoIds = new[] { mundo.CC, mundo.ADS }, titulo = "Techday", texto = "Inscrições abertas." });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var dosDois = (await resposta.Content.ReadFromJsonAsync<AvisoPublicadoResponse>(Ct))!;
        Assert.Equal(new[] { mundo.CC, mundo.ADS }.Order(), dosDois.CursoIds.Order());
        await mundo.EsvaziarFilaAsync();

        var notificacoes = await mundo.LinhasAsync<Notificacao>();
        Assert.Equal(
            Ordenados(mundo, Ana, Bruno, Diego),
            notificacoes.Where(notificacao => notificacao.ReferenciaId == dosDois.Id).Select(notificacao => notificacao.UsuarioId).Order());
        Assert.Equal(
            [mundo.Id(Diego)],
            notificacoes.Where(notificacao => notificacao.ReferenciaId != dosDois.Id).Select(notificacao => notificacao.UsuarioId));
    }

    [Fact]
    public async Task Os_destinatarios_sao_os_do_instante_da_publicacao_e_o_trabalho_repetido_nao_duplica()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        var aviso = await PublicadoAsync(coordenacao, new { escopo = "faculdade", titulo = "Recesso", texto = "Sem aula na sexta." });

        // Depois da publicação e antes do trabalho: Bruno vira ex-aluno, e uma conta nova nasce.
        // Bruno era aluno ativo na publicação e é notificado; a conta nova, não.
        mundo.Relogio.Avancar(TimeSpan.FromMinutes(1));
        await using (var scope = mundo.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var agora = mundo.Relogio.GetUtcNow();
            (await db.Set<Usuario>().FindAsync([mundo.Id(Bruno)], Ct))!.MarcarExAluno(mundo.Id(Bruno), agora);
            var cc = (await db.Set<Curso>().FindAsync([mundo.CC], Ct))!;
            var cadastro = CadastroElegivel.Aluno("20260109", "fabio.nunes@al.unieduk.com.br", "Fábio Nunes", cc, 1);
            db.AddRange(cadastro, Usuario.CadastrarAluno(cadastro, cc, "Fábio Nunes", "fabioNunes", "hash", agora));
            await db.SaveChangesAsync(Ct);
        }

        await mundo.EsvaziarFilaAsync();
        var primeira = (await mundo.LinhasAsync<Notificacao>()).Select(notificacao => notificacao.UsuarioId).Order().ToList();

        // O mesmo trabalho de novo (retentativa, queda do worker) não grava nada.
        await using (var scope = mundo.Factory.Services.CreateAsyncScope())
        {
            var gravadas = await scope.ServiceProvider.GetRequiredService<IDistribuicaoDeAviso>()
                .NotificarAsync(aviso, mundo.Relogio.GetUtcNow(), Ct);
            Assert.Equal(0, gravadas);
        }

        Assert.Equal(Ordenados(mundo, Ana, Bruno, Diego, Renata, Paulo, Marta), primeira);
        Assert.Equal(primeira, (await mundo.LinhasAsync<Notificacao>()).Select(notificacao => notificacao.UsuarioId).Order());
    }

    [Fact]
    public async Task Turma_alheia_ou_de_outro_semestre_recusa_o_aviso_inteiro()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var renata = await mundo.ComoAsync(Renata);

        await AssertRecusaAsync(
            await PublicarAsync(renata, new { escopo = "turma", turmaIds = new[] { mundo.BancoDeDados, mundo.Web }, titulo = "Prova", texto = "Amanhã." }),
            HttpStatusCode.Forbidden,
            "fora-do-escopo");
        await AssertRecusaAsync(
            await PublicarAsync(
                renata, new { escopo = "turma", turmaIds = new[] { mundo.BancoDeDadosDoProximoSemestre }, titulo = "Prova", texto = "Amanhã." }),
            HttpStatusCode.Forbidden,
            "fora-do-escopo");

        Assert.Empty(await mundo.LinhasAsync<Aviso>());
        Assert.Empty(await mundo.LinhasAsync<Trabalho>());
    }

    [Fact]
    public async Task Cada_papel_publica_so_no_escopo_dele()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var renata = await mundo.ComoAsync(Renata);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        using var marta = await mundo.ComoAsync(Marta);

        await AssertRecusaAsync(
            await PublicarAsync(ana, new { escopo = "turma", turmaIds = new[] { mundo.BancoDeDados }, titulo = "Oi", texto = "Oi." }),
            HttpStatusCode.Forbidden,
            "aviso-nao-permitido");
        await AssertRecusaAsync(
            await PublicarAsync(renata, new { escopo = "faculdade", titulo = "Recesso", texto = "Sexta." }),
            HttpStatusCode.Forbidden,
            "aviso-nao-permitido");
        await AssertRecusaAsync(
            await PublicarAsync(renata, new { escopo = "curso", cursoIds = new[] { mundo.CC }, titulo = "Recesso", texto = "Sexta." }),
            HttpStatusCode.Forbidden,
            "aviso-nao-permitido");
        await AssertRecusaAsync(
            await PublicarAsync(coordenacao, new { escopo = "turma", turmaIds = new[] { mundo.BancoDeDados }, titulo = "Prova", texto = "Amanhã." }),
            HttpStatusCode.Forbidden,
            "aviso-nao-permitido");

        // A conta de professora e instituição publica para a faculdade.
        Assert.Equal(
            HttpStatusCode.Created,
            (await PublicarAsync(marta, new { escopo = "faculdade", titulo = "Recesso", texto = "Sexta." })).StatusCode);
    }

    [Fact]
    public async Task Alvos_fora_da_forma_do_escopo_e_curso_inexistente_sao_recusados()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        using var renata = await mundo.ComoAsync(Renata);

        await AssertRecusaAsync(
            await PublicarAsync(coordenacao, new { escopo = "faculdade", cursoIds = new[] { mundo.CC }, titulo = "Recesso", texto = "Sexta." }),
            HttpStatusCode.BadRequest,
            "alvos-do-aviso-invalidos");
        await AssertRecusaAsync(
            await PublicarAsync(coordenacao, new { escopo = "curso", titulo = "Recesso", texto = "Sexta." }),
            HttpStatusCode.BadRequest,
            "alvos-do-aviso-invalidos");
        await AssertRecusaAsync(
            await PublicarAsync(renata, new { escopo = "turma", turmaIds = Array.Empty<Guid>(), titulo = "Prova", texto = "Amanhã." }),
            HttpStatusCode.BadRequest,
            "alvos-do-aviso-invalidos");
        await AssertRecusaAsync(
            await PublicarAsync(
                renata,
                new { escopo = "turma", turmaIds = new[] { mundo.BancoDeDados }, cursoIds = new[] { mundo.CC }, titulo = "Prova", texto = "Amanhã." }),
            HttpStatusCode.BadRequest,
            "alvos-do-aviso-invalidos");
        await AssertRecusaAsync(
            await PublicarAsync(coordenacao, new { escopo = "curso", cursoIds = new[] { mundo.CC, Guid.CreateVersion7() }, titulo = "Recesso", texto = "Sexta." }),
            HttpStatusCode.NotFound,
            "curso-nao-encontrado");
        await AssertRecusaAsync(
            await PublicarAsync(coordenacao, new { escopo = "mural", titulo = "Recesso", texto = "Sexta." }),
            HttpStatusCode.BadRequest,
            "dados-invalidos");

        Assert.Empty(await mundo.LinhasAsync<Aviso>());
    }

    [Fact]
    public async Task Titulo_e_texto_fora_do_tamanho_sao_recusados()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var renata = await mundo.ComoAsync(Renata);
        var turmas = new[] { mundo.BancoDeDados };

        await AssertRecusaAsync(
            await PublicarAsync(renata, new { escopo = "turma", turmaIds = turmas, titulo = "ab", texto = "Amanhã." }),
            HttpStatusCode.BadRequest,
            "titulo-do-aviso-invalido");
        await AssertRecusaAsync(
            await PublicarAsync(renata, new { escopo = "turma", turmaIds = turmas, titulo = new string('a', 121), texto = "Amanhã." }),
            HttpStatusCode.BadRequest,
            "titulo-do-aviso-invalido");
        await AssertRecusaAsync(
            await PublicarAsync(renata, new { escopo = "turma", turmaIds = turmas, titulo = "Prova", texto = "   " }),
            HttpStatusCode.BadRequest,
            "texto-do-aviso-invalido");
        await AssertRecusaAsync(
            await PublicarAsync(renata, new { escopo = "turma", turmaIds = turmas, titulo = "Prova", texto = new string('a', 2001) }),
            HttpStatusCode.BadRequest,
            "texto-do-aviso-invalido");

        // No limite, passa.
        Assert.Equal(
            HttpStatusCode.Created,
            (await PublicarAsync(renata, new { escopo = "turma", turmaIds = turmas, titulo = new string('a', 120), texto = new string('a', 2000) })).StatusCode);
    }

    [Fact]
    public async Task Aviso_com_foto_enviada_traz_as_duas_derivadas_e_confirma_o_upload()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var renata = await mundo.ComoAsync(Renata);
        var upload = await FotoDeAvisoAsync(renata);

        var resposta = await PublicarAsync(
            renata, new { escopo = "turma", turmaIds = new[] { mundo.BancoDeDados }, titulo = "Laboratório", texto = "Sala nova.", uploadId = upload.Id });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var publicado = (await resposta.Content.ReadFromJsonAsync<AvisoPublicadoResponse>(Ct))!;
        using var http = new HttpClient();
        Assert.StartsWith(armazenamento.UrlDeLeitura.ToString(), publicado.Foto!.Feed, StringComparison.Ordinal);
        Assert.Equal(TamanhoDoFeed, (await http.GetByteArrayAsync(new Uri(publicado.Foto.Feed), Ct)).Length);
        Assert.Equal(TamanhoDaGrade, (await http.GetByteArrayAsync(new Uri(publicado.Foto.Grade), Ct)).Length);

        var confirmado = Assert.Single(await mundo.LinhasAsync<UploadPendente>());
        Assert.Equal(FinalidadeDoUpload.Aviso, confirmado.Finalidade);
        Assert.NotNull(confirmado.ConfirmadoEm);

        // A mesma foto não vira outro aviso.
        await AssertRecusaAsync(
            await PublicarAsync(
                renata, new { escopo = "turma", turmaIds = new[] { mundo.Algoritmos }, titulo = "Laboratório", texto = "Sala nova.", uploadId = upload.Id }),
            HttpStatusCode.Conflict,
            "upload-ja-usado");
    }

    [Fact]
    public async Task Foto_que_nao_chegou_ao_bucket_alheia_ou_de_post_e_recusada()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var renata = await mundo.ComoAsync(Renata);
        using var paulo = await mundo.ComoAsync(Paulo);
        using var ana = await mundo.ComoAsync(Ana);
        var naoEnviada = await FotoDeAvisoAsync(renata, enviar: false);
        var doPaulo = await FotoDeAvisoAsync(paulo);
        var dePost = await Social.MundoSocial.FotoEnviadaAsync(ana);
        object Pedido(Guid uploadId) => new { escopo = "turma", turmaIds = new[] { mundo.BancoDeDados }, titulo = "Prova", texto = "Amanhã.", uploadId };

        await AssertRecusaAsync(await PublicarAsync(renata, Pedido(naoEnviada.Id)), HttpStatusCode.Conflict, "foto-nao-enviada");
        await AssertRecusaAsync(await PublicarAsync(renata, Pedido(doPaulo.Id)), HttpStatusCode.NotFound, "upload-nao-encontrado");
        await AssertRecusaAsync(await PublicarAsync(renata, Pedido(dePost.Id)), HttpStatusCode.NotFound, "upload-nao-encontrado");
        await AssertRecusaAsync(await PublicarAsync(renata, Pedido(Guid.CreateVersion7())), HttpStatusCode.NotFound, "upload-nao-encontrado");

        Assert.Empty(await mundo.LinhasAsync<Aviso>());
        Assert.All(await mundo.LinhasAsync<UploadPendente>(), upload => Assert.Null(upload.ConfirmadoEm));
    }

    [Fact]
    public async Task Aluno_nao_pede_upload_de_aviso()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);

        var resposta = await ana.PostAsJsonAsync(
            new Uri("/api/v1/uploads", UriKind.Relative),
            new { finalidade = "aviso", tipoDeConteudo = "image/jpeg", tamanhoDoFeed = TamanhoDoFeed, tamanhoDaGrade = TamanhoDaGrade },
            Ct);

        await AssertRecusaAsync(resposta, HttpStatusCode.Forbidden, "envio-nao-permitido");
    }

    private static Task<HttpResponseMessage> PublicarAsync(HttpClient client, object pedido) =>
        client.PostAsJsonAsync(_avisos, pedido, Ct);

    private static async Task<Guid> PublicadoAsync(HttpClient client, object pedido)
    {
        var resposta = await PublicarAsync(client, pedido);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<AvisoPublicadoResponse>(Ct))!.Id;
    }

    private static IOrderedEnumerable<Guid> Ordenados(MundoDoMural mundo, params string[] emails) =>
        emails.Select(mundo.Id).Order();
}
