namespace Maxx.Infrastructure.Seed;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    // Onde estão catalogo/ e calendario/. Relativo ao diretório de trabalho: "data" na
    // raiz do repositório e em /app/data na imagem.
    public string DiretorioDeDados { get; set; } = "data";
}

public enum PerfilDeSeed
{
    // Estrutura: catálogo e calendário.
    Base,

    // O mundo fictício: cursos, matérias, turmas e pessoas. Exige o base.
    Demo,
}

public sealed record ResultadoDoSeed(
    int ItensNovos,
    int ItensAtualizados,
    int ItensInalterados,
    int FamiliasNovas,
    int FamiliasAtualizadas,
    int AnosNovos,
    int AnosAtualizados);
