using System.Net;
using System.Net.Http.Json;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.IntegrationTests.Social;
using Maxx.Api.Posts;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests.PremiosParticipacao;

// O MundoSocial com o que o prêmio de participação precisa: três itens do catálogo (P01,
// P06, P17), a semana acadêmica de CC (começou há 10 dias, aceita P06), uma de Administração
// (curso sem ninguém do mundo), a Elisa (aluna de CC com conta, sem matrícula na turma da
// Renata) e o Marcos (professor sem turma). Postgres e bucket reais.
internal sealed record MundoDePremios(MundoSocial Social, DateOnly Hoje, Guid Semana, Guid SemanaDeAdm, Guid BancoDeDados)
    : IAsyncDisposable
{
    public const string Elisa = "elisa.prado@al.unieduk.com.br";
    public const string Marcos = "marcos@prof.unieduk.com.br";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<MundoDePremios> CriarAsync(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    {
        var social = await MundoSocial.CriarAsync(postgres, armazenamento);
        await using var scope = social.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var cc = await db.Set<Curso>().SingleAsync(curso => curso.Sigla == "CC", Ct);
        var bancoDeDados = await db.Set<Materia>().SingleAsync(Ct);
        var adm = new Curso("Administração", "ADM", 8);
        var agora = social.Relogio.GetUtcNow();
        var hoje = FusoHorario.DataLocal(agora);
        var semana = Evento.Semeado(
            "semana-academica-cc", "Semana Acadêmica de Computação", hoje.AddDays(-10), hoje.AddDays(-6), [CatalogoDeParticipacao.P06], cc);
        var semanaDeAdm = Evento.Semeado(
            "semana-academica-adm", "Semana Acadêmica de Administração", hoje.AddDays(-10), hoje.AddDays(-6), [CatalogoDeParticipacao.P06], adm);

        var hash = new Argon2idSenhaHasher().Hash(MundoSocial.Senha);
        var elisa = CadastroElegivel.Aluno("20260105", Elisa, "Elisa Prado", cc, 2);
        var marcos = CadastroElegivel.Professor(Marcos, "Marcos Teixeira");
        var contaDoMarcos = Usuario.CriarPeloSeed(marcos, "marcosT", agora);
        contaDoMarcos.DefinirSenha(hash);

        db.AddRange(CatalogoDeParticipacao.Linhas());
        db.AddRange(adm, semana, semanaDeAdm, elisa, marcos, contaDoMarcos);
        db.Add(Usuario.CadastrarAluno(elisa, cc, "Elisa Prado", "elisaPrado", hash, agora));
        await db.SaveChangesAsync(Ct);

        return new MundoDePremios(social, hoje, semana.Id, semanaDeAdm.Id, bancoDeDados.Id);
    }

    public static Task<HttpResponseMessage> ReivindicarAsync(HttpClient client, object pedido) =>
        client.PostAsJsonAsync(new Uri("/api/v1/reivindicacoes", UriKind.Relative), pedido, Ct);

    // A reivindicação da semana acadêmica (P06) com foto enviada de verdade; devolve o post.
    public static async Task<PostResponse> NaSemanaAsync(HttpClient client, Guid semana, string? legenda = null)
    {
        var upload = await MundoSocial.FotoEnviadaAsync(client);
        var resposta = await ReivindicarAsync(client, new { item = "P06", eventoId = semana, uploadId = upload.Id, legenda });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<PostResponse>(Ct))!;
    }

    // A comprovação de um prêmio, pelo id do prêmio que o post traz.
    public async Task<Comprovacao> ComprovacaoAsync(Guid premioId) =>
        Assert.Single(await Social.LinhasAsync<Comprovacao>(), comprovacao => comprovacao.PremioParticipacaoId == premioId);

    public async Task<PremioParticipacao> PremioAsync(Guid premioId) =>
        Assert.Single(await Social.LinhasAsync<PremioParticipacao>(), premio => premio.Id == premioId);

    public ValueTask DisposeAsync() => Social.DisposeAsync();
}
