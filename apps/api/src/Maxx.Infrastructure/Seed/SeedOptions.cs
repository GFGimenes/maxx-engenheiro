namespace Maxx.Infrastructure.Seed;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    // Onde estão catalogo/, calendario/ e termos/. Relativo ao diretório de trabalho: "data" na
    // raiz do repositório e em /app/data na imagem.
    public string DiretorioDeDados { get; set; } = "data";
}

public enum PerfilDeSeed
{
    // Estrutura: catálogo, calendário e termo de uso.
    Base,

    // O mundo fictício: cursos, matérias, turmas, pessoas e eventos. Exige o base.
    Demo,
}

public sealed record ResultadoDoSeedBase(
    int ItensNovos,
    int ItensAtualizados,
    int ItensInalterados,
    int FamiliasNovas,
    int FamiliasAtualizadas,
    int AnosNovos,
    int AnosAtualizados,
    int TermosNovos);

// Demo só acrescenta: o que já existe e bate com o arquivo fica como está.
public sealed record ResultadoDoSeedDemo(
    int CursosNovos,
    int MateriasNovas,
    int TurmasNovas,
    int CadastrosNovos,
    int MatriculasNovas,
    int VinculosNovos,
    int ContasNovas,
    int EventosNovos,
    int ContasDeAlunoNovas,
    int ExAlunosNovos);
