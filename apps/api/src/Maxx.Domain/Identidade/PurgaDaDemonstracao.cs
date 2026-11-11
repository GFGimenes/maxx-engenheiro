namespace Maxx.Domain.Identidade;

// O registro da purga de um lote da demonstração (produto.md 11, passo 12): quem purgou,
// quando, qual lote, quantas contas saíram e quantas fotos. Os dados do lote somem; o
// registro fica (P5). Desde o passo 14, o lote também tem professores e coordenações, que a
// purga encerra: basta uma conta de qualquer papel para haver o que purgar.
public sealed class PurgaDaDemonstracao
{
    // Para o EF Core materializar do banco.
    private PurgaDaDemonstracao() => Lote = null!;

    public PurgaDaDemonstracao(
        string lote, int alunos, int professores, int coordenacoes, int fotos, Guid purgadoPorId, DateTimeOffset agora)
    {
        if (alunos < 0 || professores < 0 || coordenacoes < 0)
        {
            throw new DomainException("A purga não conta um número negativo de contas.");
        }

        if (alunos + professores + coordenacoes < 1)
        {
            throw new DomainException($"O lote {lote} não tem conta de demonstração para purgar.");
        }

        if (fotos < 0)
        {
            throw new DomainException("A purga não remove um número negativo de fotos.");
        }

        Id = Guid.CreateVersion7();
        Lote = Texto.Obrigatorio(lote, "O lote da demonstração");
        Alunos = alunos;
        Professores = professores;
        Coordenacoes = coordenacoes;
        Fotos = fotos;
        PurgadoPorId = purgadoPorId;
        PurgadoEm = agora;
    }

    public Guid Id { get; private set; }

    public string Lote { get; private set; }

    // Apagados.
    public int Alunos { get; private set; }

    // Encerrados (passo 14, P1).
    public int Professores { get; private set; }

    public int Coordenacoes { get; private set; }

    // Objetos do bucket (cada derivada conta uma) que a purga mandou remover.
    public int Fotos { get; private set; }

    public Guid PurgadoPorId { get; private set; }

    public DateTimeOffset PurgadoEm { get; private set; }
}
