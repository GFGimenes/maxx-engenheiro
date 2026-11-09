namespace Maxx.Domain.Identidade;

// O registro da purga de um lote da demonstração (produto.md 11, passo 12): quem purgou,
// quando, qual lote, quantos alunos e quantas fotos saíram. Os dados do lote somem; o
// registro fica (P5).
public sealed class PurgaDaDemonstracao
{
    // Para o EF Core materializar do banco.
    private PurgaDaDemonstracao() => Lote = null!;

    public PurgaDaDemonstracao(string lote, int alunos, int fotos, Guid purgadoPorId, DateTimeOffset agora)
    {
        if (alunos < 1)
        {
            throw new DomainException($"O lote {lote} não tem aluno de demonstração para purgar.");
        }

        if (fotos < 0)
        {
            throw new DomainException("A purga não remove um número negativo de fotos.");
        }

        Id = Guid.CreateVersion7();
        Lote = Texto.Obrigatorio(lote, "O lote da demonstração");
        Alunos = alunos;
        Fotos = fotos;
        PurgadoPorId = purgadoPorId;
        PurgadoEm = agora;
    }

    public Guid Id { get; private set; }

    public string Lote { get; private set; }

    public int Alunos { get; private set; }

    // Objetos do bucket (cada derivada conta uma) que a purga mandou remover.
    public int Fotos { get; private set; }

    public Guid PurgadoPorId { get; private set; }

    public DateTimeOffset PurgadoEm { get; private set; }
}
