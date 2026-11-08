using Maxx.Domain.Catalogo;

namespace Maxx.Application.Premios;

// O que a tela de Troféus lê para o próprio aluno: o catálogo inteiro, com o critério e a
// raridade de cada item, e o que ele já obteve. Faltantes só para o dono (produto.md 9.1):
// não há rota que mostre o catálogo de outro aluno. Só consulta, sem regra.
public interface IConsultaDePremios
{
    // Nulo se o usuário não é aluno.
    Task<PremiosDoAluno?> DoAlunoAsync(Guid alunoId, CancellationToken cancellationToken);
}

// Pontos: a soma do que pontua hoje (as medalhas ativas, no passo 6).
public sealed record PremiosDoAluno(int Pontos, IReadOnlyList<FamiliaDoCatalogo> Familias, IReadOnlyList<ItemDoAluno> Itens);

public sealed record FamiliaDoCatalogo(string Codigo, string Nome, string Descricao, int Passos);

// Itens restritos a outro curso ficam de fora, salvo se o aluno já tem um (trocou de curso).
// Raridade nula: o worker ainda não calculou.
public sealed record ItemDoAluno(
    string Codigo,
    TipoDeItem Tipo,
    string Nome,
    string Criterio,
    Tier Tier,
    int Pontos,
    string? Familia,
    int? OrdemNaFamilia,
    RaridadeAtual? Raridade,
    IReadOnlyList<PremioObtido> Obtidos);

public sealed record RaridadeAtual(Raridade Nivel, int AlunosComItem, int AlunosElegiveis)
{
    // "x% dos alunos", com uma casa. Ninguém elegível é 0%.
    public double Percentual => AlunosElegiveis == 0 ? 0 : Math.Round(100.0 * AlunosComItem / AlunosElegiveis, 1);
}

// Tier e pontos do momento da concessão (ADR 0007), que podem diferir do catálogo de hoje.
// PostId: o post não removido que o prêmio rendeu, se houver (um prêmio rende um post).
public sealed record PremioObtido(Guid Id, Tier Tier, int Pontos, string? Materia, int? Bimestre, DateTimeOffset ObtidoEm, Guid? PostId);
