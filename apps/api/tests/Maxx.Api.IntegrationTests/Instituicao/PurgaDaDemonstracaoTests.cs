using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maxx.Api.Instituicao;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.IntegrationTests.Social;
using Maxx.Application.Fotos;
using Maxx.Application.Medalhas;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.Instituicao;

// A purga da demonstração por lote pela área da instituição (passo 12, etapa 3): numa
// transação, os dados do lote saem do banco e as fotos dele (post, foto de perfil, upload
// pendente) vão para a fila de remoção do bucket. Nada de outro lote nem de aluno de verdade
// é tocado. A rota vale com a flag da demonstração desligada. Postgres e bucket reais.
public sealed class PurgaDaDemonstracaoTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private const string _purgado = "banca-1";
    private const string _vigente = "banca-2";
    private const long _feed = 300 * 1024;
    private const long _grade = 40 * 1024;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lista_os_lotes_com_o_vigente_marcado_e_so_para_a_instituicao()
    {
        await using var mundo = await MundoAsync();
        await DemoAsync(mundo, "Dani Demo", "daniDemo", _purgado);
        await DemoAsync(mundo, "Edu Demo", "eduDemo", _vigente);
        await DemoAsync(mundo, "Fabi Demo", "fabiDemo", _vigente);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        using var renata = await mundo.ComoAsync(Renata);

        var lotes = (await coordenacao.GetFromJsonAsync<List<LoteDaDemonstracaoResponse>>(Rota(string.Empty), Ct))!;

        Assert.Equal(
            [(_purgado, 1, false), (_vigente, 2, true)],
            lotes.Select(lote => (lote.Lote, lote.Alunos, lote.Vigente)).OrderBy(lote => lote.Lote));
        Assert.Equal(HttpStatusCode.Forbidden, (await renata.GetAsync(Rota(string.Empty), Ct)).StatusCode);
    }

    [Fact]
    public async Task Purga_tira_o_lote_inteiro_numa_transacao_e_manda_as_fotos_para_fora_do_bucket()
    {
        await using var mundo = await MundoAsync();
        var dani = await DemoAsync(mundo, "Dani Demo", "daniDemo", _purgado);
        var edu = await DemoAsync(mundo, "Edu Demo", "eduDemo", _vigente);
        var fotos = await AtividadeAsync(mundo, dani);
        await mundo.EsvaziarFilaAsync();
        Assert.All(fotos, chave => Assert.True(mundo.Bucket.ExisteAsync(chave, Ct).Result));
        using var coordenacao = await mundo.ComoAsync(Coordenacao);

        var resposta = await coordenacao.PostAsJsonAsync(Rota($"/{_purgado}/purga"), new { confirmacao = _purgado }, Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(new PurgaResponse(_purgado, 1, fotos.Count), await resposta.Content.ReadFromJsonAsync<PurgaResponse>(Ct));

        // Nada do lote fica, e o que é de outra pessoa sobre ele sai junto.
        Assert.DoesNotContain(await mundo.LinhasAsync<Usuario>(), usuario => usuario.Id == dani);
        Assert.DoesNotContain(await mundo.LinhasAsync<CadastroElegivel>(), linha => linha.NomeCompleto == "Dani Demo");
        Assert.DoesNotContain(await mundo.LinhasAsync<Post>(), post => post.AutorId == dani);
        Assert.DoesNotContain(await mundo.LinhasAsync<Medalha>(), medalha => medalha.AlunoId == dani);
        Assert.DoesNotContain(await mundo.LinhasAsync<Notificacao>(), notificacao => notificacao.UsuarioId == dani);
        Assert.DoesNotContain(await mundo.LinhasAsync<UploadPendente>(), upload => upload.UsuarioId == dani);
        Assert.DoesNotContain(await mundo.LinhasAsync<Seguimento>(), seguimento => seguimento.SeguidorId == dani || seguimento.SeguidoId == dani);
        Assert.DoesNotContain(await mundo.LinhasAsync<Curtida>(), curtida => curtida.UsuarioId == dani);
        Assert.Equal(["Bem-vinda!"], (await mundo.LinhasAsync<Comentario>()).Select(comentario => comentario.Texto));

        // O resto fica: o outro lote, os alunos de verdade e o post da Ana.
        Assert.Contains(await mundo.LinhasAsync<Usuario>(), usuario => usuario.Id == edu);
        Assert.Single(await mundo.LinhasAsync<Post>(), post => post.AutorId == mundo.Id(Ana));
        var registro = Assert.Single(await mundo.LinhasAsync<PurgaDaDemonstracao>());
        Assert.Equal((_purgado, 1, fotos.Count, mundo.Id(Coordenacao)), (registro.Lote, registro.Alunos, registro.Fotos, registro.PurgadoPorId));

        var trabalhos = await mundo.LinhasAsync<Trabalho>();
        Assert.Single(trabalhos, trabalho => trabalho.Tipo == RemoverFotos.Tipo);
        Assert.Single(trabalhos, trabalho => trabalho.Tipo == RecalcularRanking.Tipo);
        Assert.Single(trabalhos, trabalho => trabalho.Tipo == RecalcularRaridade.Tipo);

        // O worker tira as fotos do bucket.
        mundo.Relogio.Avancar(TimeSpan.FromSeconds(11));
        await mundo.EsvaziarFilaAsync();
        foreach (var chave in fotos)
        {
            Assert.False(await mundo.Bucket.ExisteAsync(chave, Ct));
        }
    }

    [Fact]
    public async Task Purga_pede_o_lote_digitado_de_novo_vale_para_o_vigente_e_nao_acha_lote_vazio()
    {
        await using var mundo = await MundoAsync();
        await DemoAsync(mundo, "Edu Demo", "eduDemo", _vigente);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        using var renata = await mundo.ComoAsync(Renata);

        await AssertRecusaAsync(
            await coordenacao.PostAsJsonAsync(Rota($"/{_vigente}/purga"), new { confirmacao = "banca" }, Ct),
            HttpStatusCode.BadRequest,
            "confirmacao-invalida");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await renata.PostAsJsonAsync(Rota($"/{_vigente}/purga"), new { confirmacao = _vigente }, Ct)).StatusCode);
        Assert.Single(await mundo.LinhasAsync<Usuario>(), usuario => usuario.LoteDaDemonstracao == _vigente);

        Assert.Equal(
            HttpStatusCode.OK,
            (await coordenacao.PostAsJsonAsync(Rota($"/{_vigente}/purga"), new { confirmacao = _vigente }, Ct)).StatusCode);
        await AssertRecusaAsync(
            await coordenacao.PostAsJsonAsync(Rota($"/{_vigente}/purga"), new { confirmacao = _vigente }, Ct),
            HttpStatusCode.NotFound,
            "lote-nao-encontrado");
        Assert.DoesNotContain(await mundo.LinhasAsync<Usuario>(), usuario => usuario.LoteDaDemonstracao is not null);
    }

    [Fact]
    public async Task Falha_no_meio_da_purga_desfaz_tudo_e_nao_manda_foto_nenhuma_para_fora()
    {
        await using var mundo = await MundoAsync();
        var dani = await DemoAsync(mundo, "Dani Demo", "daniDemo", _purgado);
        var fotos = await AtividadeAsync(mundo, dani);
        await mundo.EsvaziarFilaAsync();

        // Um aviso em nome da Dani, que a purga não apaga (aluno não publica aviso): a chave
        // estrangeira recusa apagar a conta dela, depois de o post e a medalha já terem saído.
        await using (var scope = mundo.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Database.ExecuteSqlAsync($"""
                INSERT INTO aviso (id, autor_id, escopo, titulo, texto, publicado_em)
                VALUES ({Guid.CreateVersion7()}, {dani}, 'faculdade', 'Aviso impossível', 'Só para travar a purga.', now())
                """, Ct);
        }

        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        var resposta = await coordenacao.PostAsJsonAsync(Rota($"/{_purgado}/purga"), new { confirmacao = _purgado }, Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        Assert.Contains(await mundo.LinhasAsync<Usuario>(), usuario => usuario.Id == dani);
        Assert.Contains(await mundo.LinhasAsync<Post>(), post => post.AutorId == dani);
        Assert.Contains(await mundo.LinhasAsync<Medalha>(), medalha => medalha.AlunoId == dani);
        Assert.Empty(await mundo.LinhasAsync<PurgaDaDemonstracao>());
        Assert.DoesNotContain(await mundo.LinhasAsync<Trabalho>(), trabalho => trabalho.Tipo == RemoverFotos.Tipo);
        foreach (var chave in fotos)
        {
            Assert.True(await mundo.Bucket.ExisteAsync(chave, Ct));
        }
    }

    private static Uri Rota(string resto) => new($"/api/v1/instituicao/demonstracao/lotes{resto}", UriKind.Relative);

    // A flag da demonstração fica desligada: só o lote vigente é configurado.
    private Task<MundoSocial> MundoAsync() =>
        CriarAsync(postgres, armazenamento, configuracao: new Dictionary<string, string> { ["Demo:Lote"] = _vigente });

    // Um aluno de demonstração do curso, matriculado na turma de Banco de Dados.
    private static async Task<Guid> DemoAsync(MundoSocial mundo, string nome, string prefixo, string lote)
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var turma = await db.Set<Turma>().Include(turma => turma.Materia).SingleAsync(turma => turma.Id == mundo.Turma, Ct);
        var curso = await db.Set<Curso>().SingleAsync(curso => curso.Id == turma.Materia.CursoId, Ct);
        var cadastro = CadastroElegivel.AlunoDeDemonstracao($"DEMO{prefixo.ToUpperInvariant()[..4]}{lote[^1]}", nome, curso);
        var usuario = Usuario.EntrarNaDemonstracao(cadastro, curso, prefixo, $"hash-{prefixo}", lote, mundo.Relogio.GetUtcNow());
        db.AddRange(cadastro, new Matricula(cadastro, turma), usuario);
        await db.SaveChangesAsync(Ct);
        return usuario.Id;
    }

    // O que a Dani faz na demonstração: post com foto, foto de perfil, um upload largado,
    // medalha, comentário, curtida e seguimento nos dois sentidos com a Ana. O Bruno comenta no
    // post da Ana, e esse comentário fica. Devolve as chaves
    // das fotos que a purga tem de tirar do bucket.
    private static async Task<List<string>> AtividadeAsync(MundoSocial mundo, Guid daniId)
    {
        await using (var scope = mundo.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ConcederMedalhaHandler>()
                .ConcederAsync(mundo.Id(Renata), mundo.Turma, daniId, "M01", Ct);
        }

        await using var escopo = mundo.Factory.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var bucket = escopo.ServiceProvider.GetRequiredService<IArmazenamentoDeFotos>();
        var agora = mundo.Relogio.GetUtcNow();
        var dani = await db.Set<Usuario>().SingleAsync(usuario => usuario.Id == daniId, Ct);
        var ana = await db.Set<Usuario>().SingleAsync(usuario => usuario.Id == mundo.Id(Ana), Ct);
        var bruno = await db.Set<Usuario>().SingleAsync(usuario => usuario.Id == mundo.Id(Bruno), Ct);

        var doPost = await EnviadoAsync(bucket, dani, FinalidadeDoUpload.Post, agora);
        var doPerfil = await EnviadoAsync(bucket, dani, FinalidadeDoUpload.FotoDePerfil, agora);
        var largado = await EnviadoAsync(bucket, dani, FinalidadeDoUpload.Post, agora);
        var postDaDani = Post.Publicar(dani, "Na palestra de abertura", doPost, null, agora).Post!;
        Assert.Null(dani.TrocarFoto(doPerfil, agora).Recusa);
        var postDaAna = Post.Publicar(ana, "Primeiro dia!", null, null, agora).Post!;
        db.AddRange(doPost, doPerfil, largado, postDaDani, postDaAna);
        db.AddRange(
            Comentario.Escrever(dani, postDaAna, "Oi, Ana!", agora).Comentario!,
            Comentario.Escrever(bruno, postDaAna, "Bem-vinda!", agora).Comentario!,
            Comentario.Escrever(ana, postDaDani, "Que legal!", agora).Comentario!,
            Curtida.Curtir(dani, postDaAna, agora)!,
            Curtida.Curtir(ana, postDaDani, agora)!,
            Seguimento.Seguir(ana, dani, professorDoSeguido: false, agora)!,
            Seguimento.Seguir(dani, ana, professorDoSeguido: false, agora)!);
        await db.SaveChangesAsync(Ct);

        return [.. new[] { doPost, doPerfil, largado }.SelectMany(upload => new[] { upload.ChaveDoFeed, upload.ChaveDaGrade })];
    }

    private static async Task<UploadPendente> EnviadoAsync(
        IArmazenamentoDeFotos bucket, Usuario usuario, FinalidadeDoUpload finalidade, DateTimeOffset agora)
    {
        var upload = UploadPendente.Pedir(usuario, finalidade, "image/jpeg", _feed, _grade, agora).Upload!;
        using var http = new HttpClient();
        foreach (var (chave, tamanho) in new[] { (upload.ChaveDoFeed, _feed), (upload.ChaveDaGrade, _grade) })
        {
            var envio = bucket.AssinarEnvio(chave, "image/jpeg", tamanho, DateTimeOffset.UtcNow.AddMinutes(10));
            using var corpo = new ByteArrayContent(new byte[tamanho]);
            corpo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            Assert.Equal(HttpStatusCode.OK, (await http.PutAsync(envio.Url, corpo, Ct)).StatusCode);
        }

        return upload;
    }
}
