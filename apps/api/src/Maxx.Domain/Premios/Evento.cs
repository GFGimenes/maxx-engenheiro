using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Premios;

// O evento a que um prêmio de participação se amarra (produto.md 4.6): nome, data de
// início e data de fim. Atividade de duração (iniciação científica, monitoria) é evento com
// período, não com data única.
//
// Vem do seed, com os itens do catálogo que aceita e, quando for o caso, o curso a que se
// restringe; ou é digitado pelo aluno na reivindicação, quando não houver, e aí aceita
// qualquer item. O item que deduplica por matéria (P17, apresentação em sala) vai sempre
// por texto livre: não há evento semeado para ele.
public sealed class Evento
{
    public const int TamanhoMinimoDoNome = 3;
    public const int TamanhoMaximoDoNome = 120;
    public const int TamanhoMaximoDoCodigo = 60;

    // Para o EF Core materializar do banco.
    private Evento()
    {
        Nome = null!;
        ItensAceitos = [];
    }

    private Evento(
        string? codigo,
        string nome,
        DateOnly inicio,
        DateOnly fim,
        OrigemDoEvento origem,
        string[] itensAceitos,
        Guid? cursoId,
        Guid? criadoPorId)
    {
        nome = Texto.Obrigatorio(nome, "O nome do evento");
        var tamanho = nome.EnumerateRunes().Count();
        if (tamanho is < TamanhoMinimoDoNome or > TamanhoMaximoDoNome)
        {
            throw new DomainException(
                $"O nome do evento tem de ter de {TamanhoMinimoDoNome} a {TamanhoMaximoDoNome} caracteres, não {tamanho}.");
        }

        if (fim < inicio)
        {
            throw new DomainException($"O evento \"{nome}\" termina ({fim:yyyy-MM-dd}) antes de começar ({inicio:yyyy-MM-dd}).");
        }

        Id = Guid.CreateVersion7();
        Codigo = codigo;
        Nome = nome;
        Inicio = inicio;
        Fim = fim;
        Origem = origem;
        ItensAceitos = itensAceitos;
        CursoId = cursoId;
        CriadoPorId = criadoPorId;
    }

    public Guid Id { get; private set; }

    // Chave natural do evento semeado, a que o seed usa para não duplicar. Nulo no texto livre.
    public string? Codigo { get; private set; }

    public string Nome { get; private set; }

    public DateOnly Inicio { get; private set; }

    public DateOnly Fim { get; private set; }

    public OrigemDoEvento Origem { get; private set; }

    // Os códigos dos itens que o evento semeado aceita. Vazio no texto livre, que aceita todos.
    public IReadOnlyList<string> ItensAceitos { get; private set; }

    // Só no semeado: o evento é de um curso, e só os alunos dele o veem e reivindicam.
    public Guid? CursoId { get; private set; }

    // Só no texto livre: o aluno que o digitou.
    public Guid? CriadoPorId { get; private set; }

    public static Evento Semeado(
        string codigo,
        string nome,
        DateOnly inicio,
        DateOnly fim,
        IReadOnlyCollection<ItemPremioParticipacao> itensAceitos,
        Curso? curso)
    {
        ArgumentNullException.ThrowIfNull(itensAceitos);

        codigo = Texto.Obrigatorio(codigo, "O código do evento");
        if (codigo.Length > TamanhoMaximoDoCodigo
            || !char.IsAsciiLetterLower(codigo[0])
            || !codigo.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')
            || codigo.EndsWith('-')
            || codigo.Contains("--", StringComparison.Ordinal))
        {
            throw new DomainException(
                $"O código do evento ({codigo}) tem de ser minúsculas sem acento, dígitos e hífen, começando por letra, " +
                $"com até {TamanhoMaximoDoCodigo} caracteres.");
        }

        if (itensAceitos.Count == 0)
        {
            throw new DomainException($"O evento {codigo} não aceita item nenhum.");
        }

        if (itensAceitos.FirstOrDefault(SoPorTextoLivre) is { } porMateria)
        {
            throw new DomainException(
                $"O evento {codigo} aceita {porMateria.Codigo}, que deduplica por matéria e vai sempre por texto livre.");
        }

        var codigos = itensAceitos.Select(item => item.Codigo).ToList();
        if (codigos.Distinct(StringComparer.Ordinal).Count() != codigos.Count)
        {
            throw new DomainException($"O evento {codigo} repete item aceito.");
        }

        return new Evento(codigo, nome, inicio, fim, OrigemDoEvento.Semeado, [.. codigos.Order(StringComparer.Ordinal)], curso?.Id, null);
    }

    public static Evento TextoLivre(string nome, DateOnly inicio, DateOnly fim, Usuario autor)
    {
        ArgumentNullException.ThrowIfNull(autor);

        return new Evento(null, nome, inicio, fim, OrigemDoEvento.TextoLivre, [], null, autor.Id);
    }

    // Apresentação em sala (P17) deduplica por item, data e matéria (produto.md 4.6): o
    // evento é a própria aula, digitado pelo aluno com a matéria.
    public static bool SoPorTextoLivre(ItemPremioParticipacao item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.ChaveDeDeduplicacao.Contains(CampoDeDeduplicacao.Materia);
    }

    public bool Aceita(ItemPremioParticipacao item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return Origem == OrigemDoEvento.TextoLivre
            || (!SoPorTextoLivre(item) && ItensAceitos.Contains(item.Codigo, StringComparer.Ordinal));
    }

    public bool AbertoAoCurso(Guid? cursoId) => CursoId is null || CursoId == cursoId;

    // Quando o evento pode ser reivindicado (produto.md 4.6): já começou, em São Paulo, e
    // começou depois do fim do último semestre letivo encerrado. O ranking corta por semestre
    // letivo, e uma reivindicação retroativa pontuaria num semestre a que não pertence. O
    // limite é o fim do semestre anterior, e não o início do vigente, para o evento do
    // recesso continuar reivindicável (conta para o semestre seguinte) e a reivindicação não
    // parar no recesso. Sem semestre encerrado antes de hoje, não há limite inferior. O fim
    // pode estar adiante: a atividade de duração é reivindicada enquanto corre.
    public RecusaDoEvento? RecusaParaReivindicarEm(DateOnly hoje, SemestreLetivo? ultimoEncerrado)
    {
        if (ultimoEncerrado is not null && ultimoEncerrado.Fim >= hoje)
        {
            throw new DomainException(
                $"O semestre {ultimoEncerrado.Codigo} termina em {ultimoEncerrado.Fim:yyyy-MM-dd} e não está encerrado em {hoje:yyyy-MM-dd}.");
        }

        if (Inicio > hoje)
        {
            return RecusaDoEvento.NaoComecou;
        }

        return ultimoEncerrado is not null && Inicio <= ultimoEncerrado.Fim ? RecusaDoEvento.DeSemestreEncerrado : null;
    }
}

public enum OrigemDoEvento
{
    Semeado,
    TextoLivre,
}

public enum RecusaDoEvento
{
    NaoComecou,

    // Começou até o último dia de um semestre letivo que já terminou.
    DeSemestreEncerrado,
}
