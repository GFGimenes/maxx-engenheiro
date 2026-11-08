using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;

namespace Maxx.Application.Conquistas;

// O contexto que o avaliador pede, igual para quem concede e para quem recalcula. A data de
// referência da vigência é o fim da instância; no período curso, hoje. As matérias do
// período só no alvo dinâmico.
public sealed class ContextoDasConquistas(IConquistaRepository conquistas)
{
    public async Task<ContextoDaAvaliacao> MontarAsync(
        ItemConquista item,
        InstanciaDePeriodo instancia,
        AlunoDasConquistas aluno,
        Calendario calendario,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(instancia);
        ArgumentNullException.ThrowIfNull(aluno);
        ArgumentNullException.ThrowIfNull(calendario);

        var referencia = instancia.Fim(calendario) ?? FusoHorario.DataLocal(agora);
        var materias = item.Regra.Alvo is { EhTodasAsMatriculasDoPeriodo: true }
            ? await conquistas.MateriasDoPeriodoAsync(aluno.Id, instancia.Semestres(calendario), cancellationToken)
            : null;
        return new ContextoDaAvaliacao(referencia, aluno.SiglaDoCurso, materias);
    }
}
