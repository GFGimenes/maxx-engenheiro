using Maxx.Domain.Catalogo;

namespace Maxx.Domain.Conquistas;

// O avaliador único das regras declarativas (ADR 0004, regras-conquista.md). Lê a regra do
// catálogo e decide, para uma instância de período, quantos fatos contam, qual é o alvo e se
// a conquista está satisfeita. Não sabe quando roda nem grava nada: quem chama escolhe a
// instância, carrega os fatos do aluno e decide conceder, remover ou só atualizar o
// progresso.
//
// É um mini-interpretador, e um erro aqui quebra todas as conquistas de uma vez: por isso os
// testes cobrem forma, item e fronteira de período separadamente.
public sealed class AvaliadorDeConquista
{
    private readonly Dictionary<string, IRegraCustomizada> _customizadas;

    public AvaliadorDeConquista(IEnumerable<IRegraCustomizada>? customizadas = null)
    {
        _customizadas = new Dictionary<string, IRegraCustomizada>(StringComparer.Ordinal);
        foreach (var regra in customizadas ?? [])
        {
            if (!_customizadas.TryAdd(regra.Nome, regra))
            {
                throw new DomainException($"A regra customizada {regra.Nome} está registrada duas vezes.");
            }
        }
    }

    // fatos: os fatos contáveis do aluno; os de outra instância ou que a regra não pede são
    // ignorados aqui.
    public Avaliacao Avaliar(
        ItemConquista conquista,
        InstanciaDePeriodo instancia,
        IEnumerable<FatoContavel> fatos,
        ContextoDaAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(conquista);
        ArgumentNullException.ThrowIfNull(instancia);
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(contexto);

        if (instancia.Tipo != conquista.Periodo)
        {
            throw new DomainException(
                $"A conquista {conquista.Codigo} é de {conquista.Periodo}, não de {instancia.Tipo}.");
        }

        if (!SeAplica(conquista, contexto))
        {
            return Avaliacao.NaoAplicavel;
        }

        // Um fato só conta uma vez, mesmo que quem chama o tenha carregado duas.
        var daInstancia = fatos
            .Where(fato => fato.PertenceA(instancia))
            .DistinctBy(fato => (fato.Origem, fato.Id))
            .ToList();

        var regra = conquista.Regra;
        return regra.Forma switch
        {
            FormaDeContagem.Customizada => Customizada(conquista, instancia, daInstancia, contexto),
            FormaDeContagem.Contagem => Contar(conquista, daInstancia.Where(fato => Conta(regra, fato)).ToList(), contexto),
            FormaDeContagem.ContagemDistinta => ContarDistintos(conquista, daInstancia.Where(fato => Conta(regra, fato)).ToList(), contexto),
            _ => throw new DomainException($"A conquista {conquista.Codigo} tem forma desconhecida: {regra.Forma}."),
        };
    }

    // O fato é da fonte pedida e, havendo filtro, de um dos itens dele (regras-conquista.md 3).
    // Serve também para quem chama saber quais conquistas um fato novo pode mexer.
    public static bool Conta(RegraDeConquista regra, FatoContavel fato)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(fato);

        var daFonte = regra.Fonte switch
        {
            FonteDeContagem.Medalha => fato.Origem == OrigemDoFato.Medalha,
            FonteDeContagem.PremioParticipacao => fato.Origem == OrigemDoFato.PremioParticipacao,
            FonteDeContagem.Ambos => true,
            _ => false,
        };

        return daFonte && (regra.FiltroItens is null || regra.FiltroItens.Contains(fato.ItemCodigo, StringComparer.Ordinal));
    }

    // Item restrito a outro curso não se aplica ao aluno; fora da vigência, também não.
    private static bool SeAplica(ItemConquista conquista, ContextoDaAvaliacao contexto)
    {
        if (conquista.RestricaoCurso is { } restricao
            && !string.Equals(restricao, contexto.SiglaDoCurso, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var data = contexto.DataDeReferencia;
        return !(conquista.Vigencia is { Inicio: { } inicio } && data < inicio)
            && !(conquista.Vigencia is { Fim: { } fim } && data > fim);
    }

    private static Avaliacao Contar(ItemConquista conquista, List<FatoContavel> contaveis, ContextoDaAvaliacao contexto)
    {
        var alvo = Alvo(conquista, contexto);
        return Avaliacao.De(contaveis.Count, alvo, contaveis);
    }

    // Quantos valores distintos do campo de agrupamento aparecem. Com o alvo dinâmico, só
    // contam as matérias em que o aluno está matriculado no período: o alvo é o número delas.
    private static Avaliacao ContarDistintos(ItemConquista conquista, List<FatoContavel> contaveis, ContextoDaAvaliacao contexto)
    {
        var campo = conquista.Regra.AgruparPor
            ?? throw new DomainException($"A conquista {conquista.Codigo} conta distintos sem agruparPor.");
        var alvo = Alvo(conquista, contexto);
        var dinamico = conquista.Regra.Alvo!.EhTodasAsMatriculasDoPeriodo;

        var agrupaveis = contaveis
            .Select(fato => (Fato: fato, Chave: Chave(campo, fato)))
            .Where(par => par.Chave is not null)
            .Where(par => !dinamico || contexto.MateriasDoPeriodo!.Contains(par.Fato.MateriaId!.Value))
            .ToList();

        var distintos = agrupaveis.Select(par => par.Chave).Distinct().Count();
        return Avaliacao.De(distintos, alvo, [.. agrupaveis.Select(par => par.Fato)]);
    }

    private static object? Chave(Agrupamento campo, FatoContavel fato) => campo switch
    {
        Agrupamento.Materia => fato.MateriaId,
        Agrupamento.Bimestre => fato.BimestreId,
        Agrupamento.TipoDeEvento => fato.TipoDeEvento,
        _ => null,
    };

    // Fixo, ou as matérias distintas das matrículas do aluno no período (decisão do passo 9).
    private static int Alvo(ItemConquista conquista, ContextoDaAvaliacao contexto)
    {
        var alvo = conquista.Regra.Alvo
            ?? throw new DomainException($"A conquista {conquista.Codigo} não tem alvo.");

        if (alvo.Fixo is { } fixo)
        {
            return fixo;
        }

        return contexto.MateriasDoPeriodo?.Count
            ?? throw new DomainException(
                $"A conquista {conquista.Codigo} tem alvo nas matrículas do período, e quem chama não as informou.");
    }

    private Avaliacao Customizada(
        ItemConquista conquista,
        InstanciaDePeriodo instancia,
        IReadOnlyList<FatoContavel> daInstancia,
        ContextoDaAvaliacao contexto)
    {
        var nome = conquista.Regra.Implementacao;
        if (nome is null || !_customizadas.TryGetValue(nome, out var regra))
        {
            throw new DomainException(
                $"A conquista {conquista.Codigo} pede a regra customizada {nome}, que não está registrada.");
        }

        return regra.Avaliar(conquista, instancia, daInstancia, contexto);
    }
}

// O que o avaliador precisa saber além dos fatos. DataDeReferencia decide a vigência do item:
// o fim da instância, ou hoje no período curso. MateriasDoPeriodo: as matérias distintas das
// matrículas do aluno no período, obrigatórias só no alvo dinâmico.
public sealed record ContextoDaAvaliacao(
    DateOnly DataDeReferencia,
    string? SiglaDoCurso,
    IReadOnlySet<Guid>? MateriasDoPeriodo = null);

// Contagem e alvo são o progresso ("2 de 3"). Satisfeita só com alvo positivo: o aluno sem
// matrícula no período tem alvo zero e não ganha a conquista de graça. Evidências: os fatos
// que entraram na contagem.
public sealed record Avaliacao(bool Aplicavel, int Contagem, int Alvo, bool Satisfeita, IReadOnlyList<FatoContavel> Evidencias)
{
    public static Avaliacao NaoAplicavel { get; } = new(false, 0, 0, false, []);

    public static Avaliacao De(int contagem, int alvo, IReadOnlyList<FatoContavel> evidencias) =>
        new(true, contagem, alvo, alvo > 0 && contagem >= alvo, evidencias);
}

// O escape para regra em C# (regras-conquista.md 7): a conquista com forma customizada
// delega à implementação registrada com o nome que o catálogo declara. Nenhuma conquista da
// fase 1 usa. Recebe só os fatos da instância.
public interface IRegraCustomizada
{
    string Nome { get; }

    Avaliacao Avaliar(
        ItemConquista conquista,
        InstanciaDePeriodo instancia,
        IReadOnlyList<FatoContavel> fatosDaInstancia,
        ContextoDaAvaliacao contexto);
}
