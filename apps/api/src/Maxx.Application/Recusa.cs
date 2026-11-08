namespace Maxx.Application;

// Um pedido que a aplicação recusa por regra, não por falha: a borda traduz o tipo em
// status HTTP e devolve o código no formato único de erro. A mensagem é só para o log.
public sealed class RecusaException : Exception
{
    public RecusaException()
    {
        Codigo = string.Empty;
    }

    public RecusaException(string message) : base(message)
    {
        Codigo = string.Empty;
    }

    public RecusaException(string message, Exception innerException) : base(message, innerException)
    {
        Codigo = string.Empty;
    }

    public RecusaException(TipoDeRecusa tipo, string codigo, string? motivoParaLog = null)
        : base(motivoParaLog ?? codigo)
    {
        Tipo = tipo;
        Codigo = codigo;
    }

    public TipoDeRecusa Tipo { get; }

    // Estável e em kebab-case: é o que o cliente usa para escolher a mensagem amigável.
    public string Codigo { get; }
}

public enum TipoDeRecusa
{
    Invalido,
    NaoEncontrado,
    Conflito,
    NaoAutorizado,

    // Autenticado, mas sem direito sobre o recurso: o escopo de turma do professor.
    Proibido,
}

// Gravação recusada por uma unicidade do banco: a corrida que a checagem prévia não
// consegue fechar sozinha.
public sealed class ConflitoDeUnicidadeException : Exception
{
    public ConflitoDeUnicidadeException()
    {
    }

    public ConflitoDeUnicidadeException(string message) : base(message)
    {
    }

    public ConflitoDeUnicidadeException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ConflitoDeUnicidadeException(Unicidade unicidade, Exception innerException)
        : base($"Unicidade violada: {unicidade}.", innerException)
    {
        Unicidade = unicidade;
    }

    public Unicidade Unicidade { get; }
}

public enum Unicidade
{
    Outra,
    ContaPorCadastro,
    Prefixo,

    // Duas medalhas ativas com a mesma chave de deduplicação: a mesma vaga do período.
    MedalhaAtiva,

    // Dois posts não removidos para o mesmo prêmio: um prêmio rende um post.
    PostPorPremio,

    // A mesma pessoa curtindo o mesmo post duas vezes ao mesmo tempo.
    Curtida,

    // O mesmo seguimento gravado duas vezes ao mesmo tempo.
    Seguimento,
}

// Gravação recusada porque a linha mudou desde a leitura (xmin): outro pedido chegou
// antes. Quem chama decide se relê e tenta de novo.
public sealed class ConflitoDeConcorrenciaException : Exception
{
    public ConflitoDeConcorrenciaException()
    {
    }

    public ConflitoDeConcorrenciaException(string message) : base(message)
    {
    }

    public ConflitoDeConcorrenciaException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public interface IUnitOfWork
{
    // Lança ConflitoDeUnicidadeException quando o banco recusa por unicidade e
    // ConflitoDeConcorrenciaException quando a linha mudou desde a leitura.
    Task SalvarAsync(CancellationToken cancellationToken);

    // Esquece o que o escopo leu e alterou, para reler do banco.
    void Descartar();
}
