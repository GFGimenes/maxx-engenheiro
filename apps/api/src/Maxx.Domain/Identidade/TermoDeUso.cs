namespace Maxx.Domain.Identidade;

// O termo que o aluno aceita ao criar a conta (produto.md 2.6). Uma versão publicada nunca
// muda: quem a aceitou, aceitou aquele texto. A vigente é a de número mais alto.
public sealed class TermoDeUso
{
    // Para o EF Core materializar do banco.
    private TermoDeUso()
    {
        Conteudo = null!;
    }

    public TermoDeUso(int versao, string conteudo, DateTimeOffset publicadoEm)
    {
        if (versao < 1)
        {
            throw new DomainException($"A versão do termo de uso começa em 1, não em {versao}.");
        }

        Versao = versao;
        Conteudo = Texto.Obrigatorio(conteudo, "O texto do termo de uso");
        PublicadoEm = publicadoEm;
    }

    public int Versao { get; private set; }

    public string Conteudo { get; private set; }

    public DateTimeOffset PublicadoEm { get; private set; }
}

// Quem aceitou qual versão, e quando.
public sealed class AceiteDeTermo
{
    internal AceiteDeTermo(Guid usuarioId, int versao, DateTimeOffset aceitoEm)
    {
        UsuarioId = usuarioId;
        Versao = versao;
        AceitoEm = aceitoEm;
    }

    public Guid UsuarioId { get; private set; }

    public int Versao { get; private set; }

    public DateTimeOffset AceitoEm { get; private set; }
}
