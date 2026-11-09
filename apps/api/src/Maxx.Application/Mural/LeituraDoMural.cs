using Maxx.Application.Autenticacao;
using Maxx.Application.Paginacao;
using Maxx.Domain.Mural;

namespace Maxx.Application.Mural;

// Quem lê o mural e o que o define (produto.md 7): os vínculos vêm do banco a cada pedido,
// nunca do token. Curso é o atual, do cadastro; o professor não tem nenhum dos dois.
public sealed record LeitorDoMural(Guid Id, Guid CadastroId, Guid? CursoId, bool Instituicao);

// O aviso como o app mostra: o autor (nome e se é a instituição, que o app mostra como
// "UniMax"), as matérias das turmas ou as siglas dos cursos, e se o leitor já o leu.
public sealed record AvisoLido(
    Guid Id,
    EscopoDoAviso Escopo,
    string Titulo,
    string Texto,
    string? ChaveDaFotoDoFeed,
    string? ChaveDaFotoDaGrade,
    DateTimeOffset PublicadoEm,
    AutorDoAviso Autor,
    IReadOnlyList<string> Materias,
    IReadOnlyList<string> Cursos,
    bool Lido);

public sealed record AutorDoAviso(Guid Id, string NomeDeExibicao, bool Instituicao);

// O mural de cada um, numa regra só (passo 10, pergunta 3), que vale para a lista, para o
// aviso aberto pelo link, para a leitura e para o "Ler todos":
//
// - A instituição vê tudo.
// - Os outros veem os avisos da faculdade, os que publicaram, os dos cursos em que estão
//   (o curso atual) e os das turmas em que têm matrícula, de qualquer semestre. O
//   ex-aluno lê pelos mesmos vínculos; o professor, que não tem curso nem matrícula, vê a
//   faculdade e os dele.
public interface IConsultaDoMural
{
    // Nulo para quem não existe mais (token de conta purgada).
    Task<LeitorDoMural?> LeitorAsync(Guid usuarioId, CancellationToken cancellationToken);

    // Do mais novo ao mais antigo, por cursor (publicado_em, id). Com escopo, só ele; com
    // autor, só os desse autor (o perfil do professor).
    Task<Pagina<AvisoLido>> MuralAsync(
        LeitorDoMural leitor,
        EscopoDoAviso? escopo,
        Guid? autorId,
        PosicaoNoTempo? depoisDe,
        int limite,
        CancellationToken cancellationToken);

    // Nulo para o aviso que não existe e para o que o leitor não vê.
    Task<AvisoLido?> AvisoAsync(LeitorDoMural leitor, Guid avisoId, CancellationToken cancellationToken);

    // Se há aviso no mural do leitor que ele ainda não leu: a bolinha da aba Mural.
    Task<bool> TemNaoLidoAsync(LeitorDoMural leitor, CancellationToken cancellationToken);

    // Registra a leitura dos avisos visíveis ao leitor entre os pedidos (todos, sem ids) e
    // marca como lida a notificação de aviso novo de cada um. Idempotente: a leitura antiga
    // fica com o instante dela. Devolve quantas leituras novas gravou.
    Task<int> RegistrarLeituraAsync(
        LeitorDoMural leitor, IReadOnlyCollection<Guid>? avisoIds, DateTimeOffset agora, CancellationToken cancellationToken);
}

// Ler o mural, abrir um aviso, registrar a leitura e o "Ler todos" (produto.md 7).
public sealed class MuralHandler(IConsultaDoMural consulta, TimeProvider relogio)
{
    public async Task<Pagina<AvisoLido>> MuralAsync(
        Guid leitorId, EscopoDoAviso? escopo, Guid? autorId, string? cursor, int limite, CancellationToken cancellationToken)
    {
        var posicao = CursorDeTempo.Ler(cursor);
        var leitor = await LeitorAsync(leitorId, cancellationToken);
        return await consulta.MuralAsync(leitor, escopo, autorId, posicao, limite, cancellationToken);
    }

    public async Task<AvisoLido> AvisoAsync(Guid leitorId, Guid avisoId, CancellationToken cancellationToken)
    {
        var leitor = await LeitorAsync(leitorId, cancellationToken);
        return await consulta.AvisoAsync(leitor, avisoId, cancellationToken) ?? throw NaoEncontrado(avisoId);
    }

    // Abrir o aviso registra a leitura (passo 10, pergunta 2). O que o leitor não vê é 404,
    // como o que não existe: ninguém descobre aviso alheio pelo id.
    public async Task LerAsync(Guid leitorId, Guid avisoId, CancellationToken cancellationToken)
    {
        var leitor = await LeitorAsync(leitorId, cancellationToken);
        _ = await consulta.AvisoAsync(leitor, avisoId, cancellationToken) ?? throw NaoEncontrado(avisoId);
        await consulta.RegistrarLeituraAsync(leitor, [avisoId], relogio.GetUtcNow(), cancellationToken);
    }

    // "Ler todos": só o que a pessoa vê.
    public async Task<int> LerTodosAsync(Guid leitorId, CancellationToken cancellationToken)
    {
        var leitor = await LeitorAsync(leitorId, cancellationToken);
        return await consulta.RegistrarLeituraAsync(leitor, null, relogio.GetUtcNow(), cancellationToken);
    }

    private async Task<LeitorDoMural> LeitorAsync(Guid leitorId, CancellationToken cancellationToken) =>
        await consulta.LeitorAsync(leitorId, cancellationToken)
        ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {leitorId} não existe.");

    private static RecusaException NaoEncontrado(Guid avisoId) =>
        new(TipoDeRecusa.NaoEncontrado, CodigosDoMural.AvisoNaoEncontrado, $"Aviso {avisoId} inexistente ou fora do mural do leitor.");
}
