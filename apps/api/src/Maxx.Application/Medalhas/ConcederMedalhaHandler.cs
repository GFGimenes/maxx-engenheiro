using Maxx.Application.Autenticacao;
using Maxx.Application.Autorizacao;
using Maxx.Application.Conquistas;
using Maxx.Application.Fila;
using Maxx.Domain.Premios;

namespace Maxx.Application.Medalhas;

// Concessão de medalha pelo professor (fluxos/03-concessao-de-medalha.md): escopo de turma
// no banco, aluno com conta matriculado na turma, item do catálogo, limite por período e
// chave de deduplicação. A medalha, quem concedeu e quando, a notificação e o recálculo de
// raridade gravam juntos.
public sealed class ConcederMedalhaHandler(
    IEscopoDeTurma escopo,
    IUsuarioRepository usuarios,
    IMedalhaRepository medalhas,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<Medalha> ConcederAsync(
        Guid professorId, Guid turmaId, Guid alunoId, string itemCodigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(itemCodigo);

        try
        {
            return await TentarAsync(professorId, turmaId, alunoId, itemCodigo.Trim(), cancellationToken);
        }
        catch (ConflitoDeUnicidadeException conflito) when (conflito.Unicidade == Unicidade.MedalhaAtiva)
        {
            // Outra concessão do mesmo item ao mesmo aluno gravou a mesma vaga entre a
            // leitura e a gravação. Relida, a próxima vaga livre, se houver, é outra.
            unitOfWork.Descartar();
            try
            {
                return await TentarAsync(professorId, turmaId, alunoId, itemCodigo.Trim(), cancellationToken);
            }
            catch (ConflitoDeUnicidadeException falha) when (falha.Unicidade == Unicidade.MedalhaAtiva)
            {
                throw new RecusaException(
                    TipoDeRecusa.Conflito, CodigosDaMedalha.LimiteAtingido, $"Concessões simultâneas: {falha.Message}");
            }
        }
    }

    private async Task<Medalha> TentarAsync(
        Guid professorId, Guid turmaId, Guid alunoId, string itemCodigo, CancellationToken cancellationToken)
    {
        if (!await escopo.ProfessorLecionaAsync(professorId, turmaId, cancellationToken))
        {
            throw new RecusaException(
                TipoDeRecusa.Proibido, CodigosDaMedalha.ForaDoEscopo, $"Professor {professorId} fora da turma {turmaId}.");
        }

        var professor = await usuarios.BuscarAsync(professorId, cancellationToken)
            ?? throw new InvalidOperationException($"O professor {professorId} tem escopo e não existe.");
        var turma = await medalhas.TurmaAsync(turmaId, cancellationToken)
            ?? throw new InvalidOperationException($"A turma {turmaId} está no escopo e não existe.");
        var aluno = await medalhas.AlunoMatriculadoAsync(turmaId, alunoId, cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosDaMedalha.AlunoForaDaTurma, $"Aluno {alunoId} sem conta ou fora da turma {turmaId}.");
        var item = await medalhas.ItemAsync(itemCodigo, cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosDaMedalha.ItemNaoEncontrado, $"Medalha {itemCodigo} não está no catálogo.");

        var resultado = Medalha.Conceder(
            item.Item,
            item.Versao,
            aluno.Aluno,
            aluno.SiglaDoCurso,
            turma.Turma,
            turma.Semestre,
            professor,
            await medalhas.DoAlunoNoItemAsync(alunoId, item.Item.Codigo, cancellationToken),
            relogio.GetUtcNow());

        if (resultado.Medalha is not { } medalha)
        {
            throw Recusa(resultado.Recusa!.Value, item.Item.Codigo, alunoId);
        }

        medalhas.Adicionar(medalha);
        fila.Enfileirar(new NotificarMedalhaRecebida(medalha.Id));
        fila.Enfileirar(ReavaliarConquistas.PelaMedalha(medalha.AlunoId, medalha.Id));
        fila.Enfileirar(new RecalcularRaridade());
        await unitOfWork.SalvarAsync(cancellationToken);
        return medalha;
    }

    private static RecusaException Recusa(RecusaDaConcessao recusa, string item, Guid alunoId)
    {
        var codigo = recusa switch
        {
            RecusaDaConcessao.ExAluno => CodigosDaMedalha.ExAluno,
            RecusaDaConcessao.ForaDoSemestreDaTurma or RecusaDaConcessao.ForaDeBimestre => CodigosDaMedalha.ForaDoPeriodo,
            RecusaDaConcessao.ItemForaDeVigencia => CodigosDaMedalha.ItemForaDeVigencia,
            RecusaDaConcessao.ItemDeOutroCurso => CodigosDaMedalha.ItemDeOutroCurso,
            RecusaDaConcessao.LimiteAtingido => CodigosDaMedalha.LimiteAtingido,
            _ => throw new ArgumentOutOfRangeException(nameof(recusa), recusa, null),
        };
        return new RecusaException(TipoDeRecusa.Conflito, codigo, $"Medalha {item} para {alunoId} recusada: {recusa}.");
    }
}
