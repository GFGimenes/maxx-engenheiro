using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Maxx.Application.Autenticacao;
using Maxx.Application.Paginacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Ranking;

namespace Maxx.Application.Ranking;

// A leitura do ranking (produto.md 5): só lê a tabela que o worker escreve (ADR 0005). Quem
// vê o quê (decisão do passo 11, pergunta 6): o aluno vê as quatro visões, com o curso
// atual; o ex-aluno vê o geral e só o histórico do último curso; professor e instituição
// veem o geral, semestre e histórico.
public interface IConsultaDoRanking
{
    // Nulo para quem não existe.
    Task<LeitorDoRanking?> LeitorAsync(Guid leitorId, CancellationToken cancellationToken);

    // O semestre que a tela chama de atual (SemestresDoRanking.Atual). Nulo antes do primeiro.
    Task<SemestreDoRanking?> SemestreAtualAsync(DateOnly hoje, CancellationToken cancellationToken);

    // A página da visão a partir da posição seguinte a depoisDaPosicao, pelo índice da visão.
    // soSeguidos: só quem o leitor segue, e ele mesmo, com a posição de todos.
    Task<IReadOnlyList<LinhaLida>> PaginaAsync(
        VisaoDoRanking visao, Guid leitorId, bool soSeguidos, int depoisDaPosicao, int limite, CancellationToken cancellationToken);

    // A linha do aluno na visão; nula quando ele não tem pontos nela.
    Task<LinhaLida?> LinhaDoAlunoAsync(VisaoDoRanking visao, Guid alunoId, CancellationToken cancellationToken);
}

public sealed record LeitorDoRanking(Guid Id, Papeis Papeis, Guid? CursoId, string? Curso, string? NomeDoCurso, bool ExAluno)
{
    public bool EhAluno => Papeis == Papeis.Aluno;
}

public sealed record SemestreDoRanking(Guid Id, string Codigo);

// Uma linha como a tela mostra: posição, pontos e quem é. O semestre é o do aluno, de perfil.
public sealed record LinhaLida(
    Guid AlunoId,
    int Posicao,
    int Pontos,
    string NomeDeExibicao,
    string Arroba,
    string? Curso,
    string? NomeDoCurso,
    int? Semestre,
    bool ExAluno,
    string? ChaveDaFotoDaGrade);

public sealed record RankingLido(
    EscopoDoRanking Escopo,
    PeriodoDoRanking Periodo,
    string? Semestre,
    string? Curso,
    string? NomeDoCurso,
    LinhaLida? Eu,
    Pagina<LinhaLida> Pagina);

public static class CodigosDoRanking
{
    // Escopo ou período ausente no pedido.
    public const string VisaoIncompleta = "visao-incompleta";

    // Professor e instituição não têm curso: veem só o geral.
    public const string RankingDeCursoSoParaAluno = "ranking-de-curso-so-para-aluno";

    // O ex-aluno sai do ranking do semestre (produto.md 2.2): do curso, só o histórico.
    public const string ExAlunoSoNoHistorico = "ex-aluno-so-no-historico";
}

public sealed class LeituraDoRankingHandler(IConsultaDoRanking consulta, TimeProvider relogio)
{
    public async Task<RankingLido> LerAsync(
        Guid leitorId,
        EscopoDoRanking escopo,
        PeriodoDoRanking periodo,
        bool soSeguidos,
        string? cursor,
        int limite,
        CancellationToken cancellationToken)
    {
        var depoisDaPosicao = CursorDePosicao.Ler(cursor);
        var leitor = await consulta.LeitorAsync(leitorId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {leitorId} não existe.");

        Guid? cursoId = null;
        if (escopo == EscopoDoRanking.Curso)
        {
            if (!leitor.EhAluno || leitor.CursoId is null)
            {
                throw new RecusaException(TipoDeRecusa.Proibido, CodigosDoRanking.RankingDeCursoSoParaAluno, $"{leitorId} não é aluno.");
            }

            if (leitor.ExAluno && periodo == PeriodoDoRanking.Semestre)
            {
                throw new RecusaException(TipoDeRecusa.Proibido, CodigosDoRanking.ExAlunoSoNoHistorico, $"{leitorId} é ex-aluno.");
            }

            cursoId = leitor.CursoId;
        }

        SemestreDoRanking? semestre = null;
        if (periodo == PeriodoDoRanking.Semestre)
        {
            semestre = await consulta.SemestreAtualAsync(FusoHorario.DataLocal(relogio.GetUtcNow()), cancellationToken);
            if (semestre is null)
            {
                return Vazio(escopo, periodo, leitor);
            }
        }

        var visao = new VisaoDoRanking(escopo, periodo, semestre?.Id, cursoId);
        var linhas = await consulta.PaginaAsync(visao, leitorId, soSeguidos, depoisDaPosicao, limite + 1, cancellationToken);
        var pagina = linhas.Take(limite).ToList();
        var eu = leitor.EhAluno ? await consulta.LinhaDoAlunoAsync(visao, leitorId, cancellationToken) : null;

        return new RankingLido(
            escopo,
            periodo,
            semestre?.Codigo,
            escopo == EscopoDoRanking.Curso ? leitor.Curso : null,
            escopo == EscopoDoRanking.Curso ? leitor.NomeDoCurso : null,
            eu,
            new Pagina<LinhaLida>(pagina, linhas.Count > limite ? CursorDePosicao.Codificar(pagina[^1].Posicao) : null));
    }

    private static RankingLido Vazio(EscopoDoRanking escopo, PeriodoDoRanking periodo, LeitorDoRanking leitor) => new(
        escopo,
        periodo,
        null,
        escopo == EscopoDoRanking.Curso ? leitor.Curso : null,
        escopo == EscopoDoRanking.Curso ? leitor.NomeDoCurso : null,
        null,
        new Pagina<LinhaLida>([], null));
}

// O cursor do ranking é opaco para o app: base64url de "r.<posição>", a última da página.
// A página seguinte começa na posição depois dela, pelo índice da visão.
public static class CursorDePosicao
{
    public static string Codificar(int posicao) =>
        Base64Url.EncodeToString(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"r.{posicao}")));

    // Zero para a primeira página.
    public static int Ler(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return 0;
        }

        try
        {
            var texto = Encoding.ASCII.GetString(Base64Url.DecodeFromChars(cursor));
            if (texto.StartsWith("r.", StringComparison.Ordinal)
                && int.TryParse(texto.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var posicao)
                && posicao >= 1)
            {
                return posicao;
            }
        }
        catch (FormatException)
        {
        }

        throw new RecusaException(TipoDeRecusa.Invalido, CodigosDaPaginacao.CursorInvalido, "Cursor malformado.");
    }
}
