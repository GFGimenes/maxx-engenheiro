using System.Text.Json.Serialization;
using Maxx.Application.Fila;
using Maxx.Application.Ranking;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;

namespace Maxx.Application.Conquistas;

// A verificação recorrente do worker (D7 do passo 9): fecha as instâncias encerradas que
// ainda não foram fechadas. Se reenfileira; o seed base garante que exista uma.
public sealed record VerificarFechamentos : ICargaDeTrabalho
{
    public static string Tipo => "verificar-fechamentos";
}

// O tipo de período vai na carga pelo nome do enum ("Bimestre"), não pelo número: reordenar
// o enum com trabalho pendente na fila não muda o que o trabalho quer dizer.
//
// Uma instância encerrada que a verificação acabou de registrar como fechada: abre um
// trabalho por aluno matriculado no período.
public sealed record FecharPeriodo(
    [property: JsonConverter(typeof(JsonStringEnumConverter<Periodo>))] Periodo TipoDePeriodo,
    Guid ReferenciaDoPeriodo) : ICargaDeTrabalho
{
    public static string Tipo => "fechar-periodo";

    [JsonIgnore]
    public InstanciaDePeriodo Instancia => new(TipoDePeriodo, ReferenciaDoPeriodo);
}

// As conquistas de fechamento daquele período, para um aluno.
public sealed record AvaliarFechamentoDoAluno(
    [property: JsonConverter(typeof(JsonStringEnumConverter<Periodo>))] Periodo TipoDePeriodo,
    Guid ReferenciaDoPeriodo,
    Guid AlunoId) : ICargaDeTrabalho
{
    public static string Tipo => "avaliar-fechamento-do-aluno";

    [JsonIgnore]
    public InstanciaDePeriodo Instancia => new(TipoDePeriodo, ReferenciaDoPeriodo);
}

// Um trabalho por aluno, em vez de todos numa transação só: com a base inteira, um
// fechamento só passaria do limite de 60 s por trabalho, e um aluno que falha não segura os
// outros. Os alunos são os com conta e matrícula numa turma dos semestres que a instância
// cobre, ex-alunos inclusive (D10): os pontos deles seguem no histórico.
public sealed class FecharPeriodoHandler(
    IConquistaRepository conquistas,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork) : ITrabalhoHandler<FecharPeriodo>
{
    public async Task ExecutarAsync(FecharPeriodo carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);

        var calendario = await conquistas.CalendarioAsync(cancellationToken);
        var alunos = await conquistas.AlunosDoPeriodoAsync(carga.Instancia.Semestres(calendario), cancellationToken);
        foreach (var aluno in alunos)
        {
            fila.Enfileirar(new AvaliarFechamentoDoAluno(carga.TipoDePeriodo, carga.ReferenciaDoPeriodo, aluno));
        }

        await unitOfWork.SalvarAsync(cancellationToken);
    }
}

// No fechamento, as conquistas de gatilho fechamentoDePeriodo do tipo daquele período:
// grava o progresso e concede as satisfeitas. O primeiro fechamento roda mesmo depois do
// congelamento (D8): o limite vale para a reavaliação por fato, não para o fechamento.
// Idempotente, como a reavaliação: o que já foi concedido na instância não se repete.
public sealed class AvaliarFechamentoDoAlunoHandler(
    IConquistaRepository conquistas,
    AvaliacaoDeConquistas avaliacao,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio) : ITrabalhoHandler<AvaliarFechamentoDoAluno>
{
    public async Task ExecutarAsync(AvaliarFechamentoDoAluno carga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carga);

        await conquistas.TravarAlunoAsync(carga.AlunoId, cancellationToken);
        var aluno = await conquistas.AlunoAsync(carga.AlunoId, cancellationToken);
        if (aluno is null)
        {
            return;
        }

        var calendario = await conquistas.CalendarioAsync(cancellationToken);
        var fatos = await conquistas.FatosAsync(aluno.Id, calendario, cancellationToken);
        var ativas = await conquistas.AtivasAsync(aluno.Id, cancellationToken);
        var agora = relogio.GetUtcNow();
        var concedeu = false;

        var doFechamento = (await conquistas.CatalogoAsync(cancellationToken))
            .Where(conquista => conquista.Item.Gatilho == Gatilho.FechamentoDePeriodo && conquista.Item.Periodo == carga.TipoDePeriodo);
        foreach (var conquista in doFechamento)
        {
            concedeu |= await avaliacao.AvaliarAsync(
                conquista, carga.Instancia, aluno, calendario, fatos, ativas, podeConceder: true, agora, cancellationToken);
        }

        if (concedeu)
        {
            fila.PedirRecalculos();
        }

        await unitOfWork.SalvarAsync(cancellationToken);
    }
}
