namespace Maxx.Infrastructure.Seed;

// O seed recusou os dados. Nada foi gravado: o seed roda numa transação só.
public sealed class SeedException : Exception
{
    public SeedException()
    {
    }

    public SeedException(string message) : base(message)
    {
    }

    public SeedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
