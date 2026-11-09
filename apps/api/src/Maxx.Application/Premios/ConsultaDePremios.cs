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

// Pontos: a soma do que pontua hoje: as medalhas ativas e os prêmios de participação
// válidos e definitivos, e as conquistas não removidas.
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
    IReadOnlyList<PremioObtido> Obtidos,
    bool ExigeMateria = false,
    ProgressoDaConquista? Progresso = null);

// O progresso ("2 de 3") de uma conquista na instância vigente do período dela (passo 9):
// só na conquista que o aluno ainda não obteve nessa instância, e só para o dono
// (produto.md 9.1). Nulo também no recesso, para bimestre e semestre, e quando o aluno não
// tem matrícula no período do alvo dinâmico.
// EmMaterias: o alvo é o das matérias do período (todasAsMatriculasDoPeriodo), e o app diz
// "1 de 2 matérias".
public sealed record ProgressoDaConquista(Periodo Periodo, int Contagem, int Alvo, bool EmMaterias = false);

public sealed record RaridadeAtual(Raridade Nivel, int AlunosComItem, int AlunosElegiveis)
{
    // "x% dos alunos", com uma casa. Ninguém elegível é 0%.
    public double Percentual => AlunosElegiveis == 0 ? 0 : Math.Round(100.0 * AlunosComItem / AlunosElegiveis, 1);
}

// Tier e pontos do momento da concessão (ADR 0007), que podem diferir do catálogo de hoje.
// PostId: o post não removido que o prêmio rendeu, se houver (um prêmio rende um post).
// Instancia: só na conquista, a instância do período em que foi obtida (passo 9).
public sealed record PremioObtido(
    Guid Id,
    Tier Tier,
    int Pontos,
    string? Materia,
    int? Bimestre,
    DateTimeOffset ObtidoEm,
    Guid? PostId,
    InstanciaObtida? Instancia = null);

// A instância de uma conquista obtida, em números para o app escrever: o bimestre (1 a 4) e
// o ano; o semestre (1 ou 2) e o ano; o ano; ou nada, no período curso.
public sealed record InstanciaObtida(Periodo Periodo, int? Ano, int? Semestre, int? Bimestre);
