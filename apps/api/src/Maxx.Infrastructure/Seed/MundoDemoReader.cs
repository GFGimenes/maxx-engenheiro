using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maxx.Infrastructure.Seed;

// data/demo/mundo.json. Aqui só a coerência interna do arquivo (e-mail e RA únicos,
// matérias citadas existentes). As regras de cada entidade são as do domínio, aplicadas
// quando o seeder constrói os objetos.
public static class MundoDemoReader
{
    public const string Arquivo = "mundo.json";

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static MundoDemo Ler(string diretorio)
    {
        var caminho = Path.Combine(diretorio, Arquivo);
        if (!File.Exists(caminho))
        {
            throw new SeedException($"Mundo de demonstração não encontrado: {caminho}.");
        }

        MundoDemo mundo;
        try
        {
            mundo = JsonSerializer.Deserialize<MundoDemo>(File.ReadAllText(caminho), _json)
                ?? throw new SeedException($"{Arquivo}: arquivo vazio.");
        }
        catch (JsonException ex)
        {
            throw new SeedException($"{Arquivo}: {ex.Message}", ex);
        }

        var erros = Validar(mundo);
        if (erros.Count > 0)
        {
            throw new SeedException(
                $"{Arquivo} inválido, nada foi gravado:" + Environment.NewLine +
                string.Join(Environment.NewLine, erros.Select(erro => $"  {erro}")));
        }

        return mundo;
    }

    private static List<string> Validar(MundoDemo mundo)
    {
        var erros = new List<string>();

        Repetidos(mundo.Cursos.Select(curso => curso.Sigla), "sigla de curso", erros);

        var pessoas = mundo.Instituicao.Select(pessoa => pessoa.Email)
            .Concat(mundo.Cursos.SelectMany(curso => curso.Professores.Select(professor => professor.Email)))
            .Concat(mundo.Cursos.SelectMany(curso => curso.Alunos.Select(aluno => aluno.Email)));
        Repetidos(pessoas, "e-mail", erros, StringComparer.OrdinalIgnoreCase);
        Repetidos(mundo.Cursos.SelectMany(curso => curso.Alunos.Select(aluno => aluno.Ra)), "RA", erros);

        foreach (var curso in mundo.Cursos)
        {
            Repetidos(curso.Materias.Select(materia => materia.Nome), $"matéria em {curso.Sigla}", erros);

            var materias = curso.Materias.Select(materia => materia.Nome).ToHashSet(StringComparer.Ordinal);
            var citadas = curso.Professores.SelectMany(professor => professor.Materias.Select(nome => (professor.Email, nome)))
                .Concat(curso.Alunos.SelectMany(aluno => aluno.Materias.Select(nome => (aluno.Email, nome))));
            foreach (var (email, nome) in citadas.Where(citada => !materias.Contains(citada.nome)))
            {
                erros.Add($"{email} cita a matéria \"{nome}\", que não existe em {curso.Sigla}.");
            }

            var semestres = curso.Materias.SelectMany(materia => materia.Turmas).ToHashSet(StringComparer.Ordinal);
            foreach (var aluno in curso.Alunos.Where(aluno => aluno.SemestresLetivos is not null))
            {
                if (aluno.SemestresLetivos!.Count == 0)
                {
                    erros.Add($"{aluno.Email} tem a lista de semestres letivos vazia.");
                }

                foreach (var codigo in aluno.SemestresLetivos.Where(codigo => !semestres.Contains(codigo)))
                {
                    erros.Add($"{aluno.Email} cursa {codigo}, que não tem turma em {curso.Sigla}.");
                }

                Repetidos(aluno.SemestresLetivos, $"semestre letivo de {aluno.Email}", erros);
            }
        }

        return erros;
    }

    private static void Repetidos(
        IEnumerable<string> valores, string oQue, List<string> erros, StringComparer? comparador = null)
    {
        foreach (var repetido in valores.GroupBy(valor => valor, comparador ?? StringComparer.Ordinal)
                     .Where(grupo => grupo.Count() > 1))
        {
            erros.Add($"{oQue} repetido: {repetido.Key}.");
        }
    }
}

public sealed class MundoDemo
{
    public string? Observacao { get; init; }

    public required List<PessoaDemo> Instituicao { get; init; }

    public required List<CursoDemo> Cursos { get; init; }
}

public sealed class CursoDemo
{
    public required string Sigla { get; init; }

    public required string Nome { get; init; }

    public required int DuracaoEmSemestres { get; init; }

    public required List<MateriaDemo> Materias { get; init; }

    public required List<ProfessorDemo> Professores { get; init; }

    public required List<AlunoDemo> Alunos { get; init; }
}

public sealed class MateriaDemo
{
    public required string Nome { get; init; }

    // Códigos de semestre letivo ("2026/2"): uma turma por semestre.
    public required List<string> Turmas { get; init; }
}

public class PessoaDemo
{
    public required string Email { get; init; }

    public required string NomeCompleto { get; init; }
}

public sealed class ProfessorDemo : PessoaDemo
{
    public required List<string> Materias { get; init; }
}

public sealed class AlunoDemo : PessoaDemo
{
    public required string Ra { get; init; }

    public required int Semestre { get; init; }

    public required List<string> Materias { get; init; }

    // Os semestres letivos ("2026/1") em que cursou as matérias. Sem a lista, todos os das
    // turmas delas (passo 12).
    public List<string>? SemestresLetivos { get; init; }

    // Ex-aluno: a instituição o marca no dia seguinte ao fim do último semestre que cursou.
    public bool ExAluno { get; init; }
}
