namespace Maxx.Domain.Catalogo;

// O segundo eixo do medalhão, derivado da escassez (produto.md 4.3). Não é campo do
// catálogo: o worker recalcula, e o prêmio nunca a congela (ADR 0007).
public enum Raridade
{
    Comum,
    Raro,
    Epico,
    Lendario,
}

public static class Raridades
{
    // alunosComItem: alunos distintos com ao menos um prêmio do item, ativos ou ex-alunos.
    // alunosElegiveis: os que podiam ganhar, ativos e ex-alunos: o campus inteiro para item
    // aberto, o curso para item restrito. Lendário abaixo de 1%, Épico abaixo de 5%, Raro
    // abaixo de 20%, Comum de 20% para cima. Item que ninguém tem é Lendário com 0%, mesmo
    // sem ninguém elegível. A conta é em inteiros, sem arredondamento na fronteira.
    public static Raridade De(int alunosComItem, int alunosElegiveis)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(alunosComItem);
        ArgumentOutOfRangeException.ThrowIfNegative(alunosElegiveis);
        if (alunosComItem > alunosElegiveis)
        {
            throw new ArgumentOutOfRangeException(
                nameof(alunosComItem), alunosComItem, $"Mais alunos com o item que elegíveis ({alunosElegiveis}).");
        }

        var cem = 100L * alunosComItem;
        return cem < alunosElegiveis * 1L || alunosComItem == 0 ? Raridade.Lendario
            : cem < alunosElegiveis * 5L ? Raridade.Epico
            : cem < alunosElegiveis * 20L ? Raridade.Raro
            : Raridade.Comum;
    }
}
