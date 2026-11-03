namespace Maxx.Domain;

// Regra de produto violada. A borda traduz para o formato único de erro; a mensagem é
// para quem lê o log, não para o usuário final.
public sealed class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
