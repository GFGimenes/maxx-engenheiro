namespace Maxx.Domain.Mural;

// A leitura de um aviso por um usuário (produto.md 7): é o que apaga a bolinha dourada da
// aba Mural. Uma por usuário e aviso; ler de novo não muda o instante da primeira leitura.
public sealed class LeituraDeAviso
{
    // Para o EF Core materializar do banco.
    private LeituraDeAviso()
    {
    }

    public LeituraDeAviso(Guid usuarioId, Guid avisoId, DateTimeOffset lidaEm)
    {
        UsuarioId = usuarioId;
        AvisoId = avisoId;
        LidaEm = lidaEm;
    }

    public Guid UsuarioId { get; private set; }

    public Guid AvisoId { get; private set; }

    public DateTimeOffset LidaEm { get; private set; }
}
