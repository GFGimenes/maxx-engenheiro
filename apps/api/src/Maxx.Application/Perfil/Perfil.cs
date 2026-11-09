using Maxx.Application.Autenticacao;
using Maxx.Application.Fila;
using Maxx.Application.Fotos;
using Maxx.Application.Medalhas;
using Maxx.Application.Paginacao;
using Maxx.Application.Posts;
using Maxx.Domain;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Application.Perfil;

// O perfil como o app mostra (produto.md 6.5 e 9.1). Só consulta, sem regra.
public interface IConsultaDePerfil
{
    // Nulo para quem não existe e para a conta só de instituição, que não tem perfil social
    // (produto.md 2.1). O RA vem só quando o leitor é o próprio aluno.
    Task<PerfilLido?> PerfilAsync(Guid usuarioId, Guid leitorId, CancellationToken cancellationToken);

    // Os posts não removidos do autor, do mais novo ao mais antigo: a grade do perfil.
    Task<Pagina<Guid>> PostsAsync(Guid autorId, PosicaoNoTempo? depoisDe, int limite, CancellationToken cancellationToken);

    // Alunos, ativos e ex-alunos, por nome de exibição e arroba, sem diferenciar acento nem
    // maiúsculas, com filtros de curso e de semestre (produto.md 6.4). Por nome, A a Z.
    Task<Pagina<AlunoEncontrado>> BuscarAlunosAsync(
        FiltroDaBusca filtro, string? cursor, int limite, Guid leitorId, CancellationToken cancellationToken);

    // Os cursos, pela sigla: os filtros da Busca (produto.md 6.4).
    Task<IReadOnlyList<CursoDaBusca>> CursosAsync(CancellationToken cancellationToken);

    // Se o prefixo é de outra pessoa, agora ou no passado (reserva permanente).
    Task<bool> PrefixoDeOutraPessoaAsync(string prefixo, Guid usuarioId, CancellationToken cancellationToken);
}

public sealed record PerfilLido(
    Guid Id,
    string NomeDeExibicao,
    string Arroba,
    Papeis Papeis,
    string? Curso,
    string? NomeDoCurso,
    int? Semestre,
    bool ExAluno,
    string? Ra,
    int? Pontos,
    int Seguidores,
    int Seguindo,
    bool EuSigo,
    string? ChaveDaFotoDoFeed,
    string? ChaveDaFotoDaGrade,
    ProfessorDoPerfil? Professor,
    int? Posicao);

// O que o perfil do professor mostra além do nome (produto.md 2.1): as turmas do semestre e
// as medalhas que ele concedeu. Os avisos chegam no passo 10.
public sealed record ProfessorDoPerfil(IReadOnlyList<TurmaDoProfessor> Turmas, int MedalhasConcedidas);

// Termo sem "@" e sem o sufixo; curso pela sigla; semestre do aluno, de 1 a 8.
public sealed record FiltroDaBusca(string Termo, string? Curso, int? Semestre);

// A duração dá os semestres do filtro: cada curso vai do 1º ao último dele (Medicina tem 12).
public sealed record CursoDaBusca(string Sigla, string Nome, int DuracaoEmSemestres, Guid Id = default);

public sealed record AlunoEncontrado(
    Guid Id, string NomeDeExibicao, string Arroba, string? Curso, int? Semestre, bool ExAluno, string? ChaveDaFotoDaGrade, bool EuSigo);

// A busca de alunos (produto.md 6.4): o termo vale pelo nome e pelo arroba, e quem cola um
// arroba inteiro ("@anaSouza-CC") procura pelo prefixo.
public sealed class BuscaDeAlunosHandler(IConsultaDePerfil consulta)
{
    public const int TamanhoMinimoDoTermo = 2;

    public Task<Pagina<AlunoEncontrado>> BuscarAsync(
        Guid leitorId, string? termo, string? curso, int? semestre, string? cursor, int limite, CancellationToken cancellationToken)
    {
        termo = Normalizar(termo);
        curso = string.IsNullOrWhiteSpace(curso) ? null : curso.Trim();
        if (termo.Length < TamanhoMinimoDoTermo && curso is null && semestre is null)
        {
            throw new RecusaException(TipoDeRecusa.Invalido, CodigosDoPerfil.BuscaSemCriterio, "Busca sem termo nem filtro.");
        }

        return consulta.BuscarAlunosAsync(new FiltroDaBusca(termo, curso, semestre), cursor, limite, leitorId, cancellationToken);
    }

    public static string Normalizar(string? termo)
    {
        termo = termo?.Trim() ?? string.Empty;
        if (termo.StartsWith('@'))
        {
            termo = termo[1..];
            var hifen = termo.IndexOf('-', StringComparison.Ordinal);
            termo = hifen >= 0 ? termo[..hifen] : termo;
        }

        return termo.Trim();
    }
}

// Editar perfil (produto.md 6.5): foto, nome de exibição e a parte do arroba antes do
// hífen, cada um opcional. Tudo ou nada: uma recusa não grava o resto.
public sealed class EditarPerfilHandler(
    IUsuarioRepository usuarios,
    IUploadPendenteRepository uploads,
    IConsultaDePerfil consulta,
    IArmazenamentoDeFotos armazenamento,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task EditarAsync(
        Guid usuarioId, string? nomeDeExibicao, string? prefixo, Guid? fotoUploadId, bool removerFoto, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.BuscarComPrefixosAsync(usuarioId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {usuarioId} não existe.");
        var agora = relogio.GetUtcNow();

        if (nomeDeExibicao is not null)
        {
            Validar(() => usuario.EditarNomeDeExibicao(nomeDeExibicao), CodigosDeRecusa.NomeDeExibicaoInvalido);
        }

        if (prefixo is not null)
        {
            Validar(() => PrefixoDeArroba.Validar(prefixo), CodigosDeRecusa.PrefixoInvalido);
            if (await consulta.PrefixoDeOutraPessoaAsync(prefixo.Trim(), usuarioId, cancellationToken))
            {
                throw new RecusaException(TipoDeRecusa.Conflito, CodigosDeRecusa.PrefixoIndisponivel, "Prefixo de outra pessoa.");
            }

            usuario.EditarPrefixo(prefixo, agora);
        }

        await EditarFotoAsync(usuario, fotoUploadId, removerFoto, agora, cancellationToken);

        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeUnicidadeException conflito) when (conflito.Unicidade == Unicidade.Prefixo)
        {
            // Outra pessoa reservou o mesmo prefixo entre a consulta e a gravação.
            throw new RecusaException(TipoDeRecusa.Conflito, CodigosDeRecusa.PrefixoIndisponivel, "Prefixo reservado ao mesmo tempo.");
        }
        catch (ConflitoDeConcorrenciaException)
        {
            throw new RecusaException(TipoDeRecusa.Conflito, CodigosDoPost.UploadJaUsado, $"Upload {fotoUploadId} usado ao mesmo tempo.");
        }
    }

    // A disponibilidade para quem já tem conta: o próprio prefixo, atual ou antigo, está livre
    // para ele (reserva permanente, produto.md 2.3.1).
    public async Task<bool> PrefixoDisponivelAsync(Guid usuarioId, string prefixo, CancellationToken cancellationToken)
    {
        Validar(() => PrefixoDeArroba.Validar(prefixo), CodigosDeRecusa.PrefixoInvalido);
        return !await consulta.PrefixoDeOutraPessoaAsync(prefixo.Trim(), usuarioId, cancellationToken);
    }

    private async Task EditarFotoAsync(Usuario usuario, Guid? fotoUploadId, bool removerFoto, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        if (fotoUploadId is not null && removerFoto)
        {
            throw new RecusaException(TipoDeRecusa.Invalido, CodigosDoPerfil.FotoDePerfilAmbigua, "Trocar e remover a foto no mesmo pedido.");
        }

        IReadOnlyList<string> antigas = [];
        if (fotoUploadId is { } id)
        {
            var upload = await uploads.BuscarAsync(id, cancellationToken)
                ?? throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDoPost.UploadNaoEncontrado, $"Upload {id} não existe.");
            var troca = usuario.TrocarFoto(upload, agora);
            if (troca.Recusa is { } recusa)
            {
                throw recusa == RecusaDaTrocaDeFoto.UploadJaUsado
                    ? new RecusaException(TipoDeRecusa.Conflito, CodigosDoPost.UploadJaUsado, $"Upload {id} já usado.")
                    : new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDoPost.UploadNaoEncontrado, $"Upload {id} alheio ou de post.");
            }

            foreach (var chave in upload.Chaves)
            {
                if (!await armazenamento.ExisteAsync(chave, cancellationToken))
                {
                    throw new RecusaException(TipoDeRecusa.Conflito, CodigosDoPost.FotoNaoEnviada, $"Objeto {chave} não chegou ao bucket.");
                }
            }

            antigas = troca.ChavesAntigas;
        }
        else if (removerFoto)
        {
            antigas = usuario.RemoverFoto();
        }

        if (antigas.Count > 0)
        {
            fila.Enfileirar(new RemoverFotos(antigas));
        }
    }

    private static void Validar(Action validacao, string codigo)
    {
        try
        {
            validacao();
        }
        catch (DomainException falha)
        {
            throw new RecusaException(TipoDeRecusa.Invalido, codigo, falha.Message);
        }
    }
}
