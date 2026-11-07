namespace Maxx.Application.Fila;

// A carga de um trabalho da fila (ADR 0008). O tipo é o nome gravado na linha, estável:
// trocar o nome de um tipo com trabalho pendente deixa esse trabalho sem quem o execute.
// A carga vai em JSON para o banco e não leva segredo: o banco não é lugar de segredo em
// repouso, e o que precisa de um o gera no worker.
public interface ICargaDeTrabalho
{
    static abstract string Tipo { get; }
}

// Enfileira na unidade de trabalho de quem chama: o trabalho só existe se a escrita que o
// originou gravar, no mesmo IUnitOfWork.SalvarAsync. Nada sai daqui sozinho.
public interface IFilaDeTrabalhos
{
    void Enfileirar<TCarga>(TCarga carga)
        where TCarga : ICargaDeTrabalho;
}

// Executa um tipo de trabalho no worker. Roda dentro da transação que segura o trabalho:
// o que o handler grava pelo IUnitOfWork só vale se o trabalho terminar. Pode rodar mais
// de uma vez (retentativa, queda do worker no meio), então precisa ser idempotente.
// Lançar exceção é falhar: o trabalho volta com recuo, até o limite de tentativas.
public interface ITrabalhoHandler<in TCarga>
    where TCarga : ICargaDeTrabalho
{
    Task ExecutarAsync(TCarga carga, CancellationToken cancellationToken);
}
