namespace Maxx.Domain.Catalogo;

// Referencia é o código do item ou da família com problema.
public sealed record ErroDeCatalogo(string Referencia, string Mensagem)
{
    public override string ToString() => $"{Referencia}: {Mensagem}";
}

// O catálogo é dado, e perde o que o compilador daria de graça (ADR 0004). Este
// validador é a compensação: roda na CI e antes de o seeder gravar qualquer coisa.
// Devolve todos os erros de uma vez, para uma correção não revelar a próxima.
public static class CatalogoValidator
{
    private static readonly HashSet<CampoDeDeduplicacao> _camposDeMedalha =
    [
        CampoDeDeduplicacao.Aluno, CampoDeDeduplicacao.Item, CampoDeDeduplicacao.Materia,
        CampoDeDeduplicacao.Bimestre, CampoDeDeduplicacao.Semestre, CampoDeDeduplicacao.Ocorrencia,
    ];

    private static readonly HashSet<CampoDeDeduplicacao> _camposDePremioParticipacao =
    [
        CampoDeDeduplicacao.Aluno, CampoDeDeduplicacao.Item, CampoDeDeduplicacao.Evento,
        CampoDeDeduplicacao.DataDoEvento, CampoDeDeduplicacao.Materia,
    ];

    public static IReadOnlyList<ErroDeCatalogo> Validar(Catalogo catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        var erros = new List<ErroDeCatalogo>();
        var itensPorCodigo = new Dictionary<string, ItemCatalogo>(StringComparer.Ordinal);

        foreach (var item in catalogo.Itens)
        {
            if (!itensPorCodigo.TryAdd(item.Codigo, item))
            {
                erros.Add(new(item.Codigo, "código repetido no catálogo."));
            }
        }

        foreach (var item in catalogo.Itens)
        {
            ValidarComum(item, erros);

            switch (item)
            {
                case ItemMedalha medalha:
                    ValidarMedalha(medalha, erros);
                    break;
                case ItemPremioParticipacao premio:
                    ValidarPremioParticipacao(premio, erros);
                    break;
                case ItemConquista conquista:
                    ValidarConquista(conquista, itensPorCodigo, erros);
                    break;
            }
        }

        ValidarFamilias(catalogo, erros);

        return erros;
    }

    private static void ValidarComum(ItemCatalogo item, List<ErroDeCatalogo> erros)
    {
        var prefixo = item.Tipo switch
        {
            TipoDeItem.Medalha => 'M',
            TipoDeItem.PremioParticipacao => 'P',
            _ => 'C',
        };

        if (item.Codigo.Length < 2 || item.Codigo[0] != prefixo || !item.Codigo[1..].All(char.IsAsciiDigit))
        {
            erros.Add(new(item.Codigo, $"código de {item.Tipo} precisa ser {prefixo} seguido de dígitos."));
        }

        if (string.IsNullOrWhiteSpace(item.Nome))
        {
            erros.Add(new(item.Codigo, "nome vazio."));
        }

        if (string.IsNullOrWhiteSpace(item.Descricao))
        {
            erros.Add(new(item.Codigo, "descrição vazia."));
        }

        var faixa = Tiers.Faixa(item.Tier);
        if (!faixa.Contem(item.Pontos))
        {
            erros.Add(new(item.Codigo, $"{item.Pontos} pontos fora da faixa {item.Tier} ({faixa})."));
        }

        if ((item.Familia is null) != (item.OrdemNaFamilia is null))
        {
            erros.Add(new(item.Codigo, "família e ordem na família vêm juntas, ou nenhuma das duas."));
        }

        if (item.Vigencia is { Inicio: { } inicio, Fim: { } fim } && fim < inicio)
        {
            erros.Add(new(item.Codigo, "vigência termina antes de começar."));
        }
    }

    private static void ValidarMedalha(ItemMedalha medalha, List<ErroDeCatalogo> erros)
    {
        if (medalha.PeriodoDeReferencia is not (Periodo.Bimestre or Periodo.Semestre))
        {
            erros.Add(new(medalha.Codigo, $"medalha é de bimestre ou de semestre, não de {medalha.PeriodoDeReferencia}."));
        }

        // Fato isolado em Bronze; só as de fechamento de matéria (semestre) passam disso.
        if (medalha.Tier != Tier.Bronze && medalha.PeriodoDeReferencia != Periodo.Semestre)
        {
            erros.Add(new(medalha.Codigo, $"medalha {medalha.Tier} só existe como fechamento de matéria, no semestre."));
        }

        var chave = medalha.ChaveDeDeduplicacao;
        ValidarChave(medalha.Codigo, chave, _camposDeMedalha, erros);

        if (!chave.Contains(CampoDeDeduplicacao.Item) || !chave.Contains(CampoDeDeduplicacao.Materia))
        {
            erros.Add(new(medalha.Codigo, "a chave de deduplicação da medalha precisa de item e matéria."));
        }

        var campoDoPeriodo = medalha.PeriodoDeReferencia == Periodo.Semestre
            ? CampoDeDeduplicacao.Semestre
            : CampoDeDeduplicacao.Bimestre;
        if (!chave.Contains(campoDoPeriodo))
        {
            erros.Add(new(medalha.Codigo, $"a chave de deduplicação precisa do período de referência ({campoDoPeriodo})."));
        }

        // Nos itens repetíveis, a ocorrência entra na chave e é o que o limite controla.
        if (medalha.Repetivel != chave.Contains(CampoDeDeduplicacao.Ocorrencia))
        {
            erros.Add(new(medalha.Codigo, "ocorrência está na chave se, e só se, a medalha é repetível."));
        }

        if (medalha.Repetivel && medalha.LimitePorPeriodo < 2)
        {
            erros.Add(new(medalha.Codigo, "medalha repetível precisa de limite por período de ao menos 2."));
        }

        if (!medalha.Repetivel && medalha.LimitePorPeriodo != 1)
        {
            erros.Add(new(medalha.Codigo, "medalha não repetível tem limite por período 1."));
        }
    }

    private static void ValidarPremioParticipacao(ItemPremioParticipacao premio, List<ErroDeCatalogo> erros)
    {
        if (premio.PeriodoDeReferencia != Periodo.Evento)
        {
            erros.Add(new(premio.Codigo, "prêmio de participação conta no semestre do evento: período de referência é evento."));
        }

        if (string.IsNullOrWhiteSpace(premio.TipoDeEvento))
        {
            erros.Add(new(premio.Codigo, "tipo de evento vazio."));
        }

        var chave = premio.ChaveDeDeduplicacao;
        ValidarChave(premio.Codigo, chave, _camposDePremioParticipacao, erros);

        if (!chave.Contains(CampoDeDeduplicacao.Evento) && !chave.Contains(CampoDeDeduplicacao.DataDoEvento))
        {
            erros.Add(new(premio.Codigo, "a chave de deduplicação precisa do evento ou da data do evento."));
        }
    }

    private static void ValidarChave(
        string codigo,
        IReadOnlyList<CampoDeDeduplicacao> chave,
        HashSet<CampoDeDeduplicacao> permitidos,
        List<ErroDeCatalogo> erros)
    {
        if (!chave.Contains(CampoDeDeduplicacao.Aluno))
        {
            erros.Add(new(codigo, "a chave de deduplicação precisa do aluno."));
        }

        foreach (var campo in chave.Where(campo => !permitidos.Contains(campo)).Distinct())
        {
            erros.Add(new(codigo, $"{campo} não pertence à chave de deduplicação deste tipo."));
        }

        if (chave.Distinct().Count() != chave.Count)
        {
            erros.Add(new(codigo, "campo repetido na chave de deduplicação."));
        }
    }

    private static void ValidarConquista(
        ItemConquista conquista, Dictionary<string, ItemCatalogo> itensPorCodigo, List<ErroDeCatalogo> erros)
    {
        var codigo = conquista.Codigo;
        var regra = conquista.Regra;

        if (conquista.Periodo == Periodo.Evento)
        {
            erros.Add(new(codigo, "conquista é de bimestre, semestre, ano ou curso, não de evento."));
        }

        // A instância do período curso é o próprio aluno, sem fronteira de data: nunca fecha.
        if (conquista.Periodo == Periodo.Curso && conquista.Gatilho == Gatilho.FechamentoDePeriodo)
        {
            erros.Add(new(codigo, "período curso não tem fechamento: o gatilho é porFato."));
        }

        if (regra.Forma == FormaDeContagem.Customizada)
        {
            if (string.IsNullOrWhiteSpace(regra.Implementacao))
            {
                erros.Add(new(codigo, "regra customizada precisa do nome da implementação."));
            }

            return;
        }

        if (regra.Implementacao is not null)
        {
            erros.Add(new(codigo, "implementação só existe na regra customizada."));
        }

        if (regra.Fonte is not { } fonte)
        {
            erros.Add(new(codigo, "regra sem fonte."));
            return;
        }

        ValidarFiltro(codigo, fonte, regra.FiltroItens, itensPorCodigo, erros);
        ValidarAgrupamento(codigo, regra, fonte, erros);
        ValidarAlvo(conquista, erros);
    }

    private static void ValidarFiltro(
        string codigo,
        FonteDeContagem fonte,
        IReadOnlyList<string>? filtro,
        Dictionary<string, ItemCatalogo> itensPorCodigo,
        List<ErroDeCatalogo> erros)
    {
        if (filtro is null)
        {
            return;
        }

        if (filtro.Count == 0)
        {
            erros.Add(new(codigo, "filtroItens vazio: use nulo para qualquer item da fonte."));
        }

        foreach (var filtrado in filtro)
        {
            if (!itensPorCodigo.TryGetValue(filtrado, out var item))
            {
                erros.Add(new(codigo, $"filtroItens aponta para {filtrado}, que não existe."));
                continue;
            }

            if (item.Tipo == TipoDeItem.Conquista)
            {
                erros.Add(new(codigo, $"filtroItens aponta para a conquista {filtrado}: conquista nunca conta outra conquista."));
                continue;
            }

            var cabeNaFonte = fonte switch
            {
                FonteDeContagem.Medalha => item.Tipo == TipoDeItem.Medalha,
                FonteDeContagem.PremioParticipacao => item.Tipo == TipoDeItem.PremioParticipacao,
                _ => true,
            };
            if (!cabeNaFonte)
            {
                erros.Add(new(codigo, $"filtroItens aponta para {filtrado}, que não é da fonte {fonte}."));
            }
        }
    }

    private static void ValidarAgrupamento(
        string codigo, RegraDeConquista regra, FonteDeContagem fonte, List<ErroDeCatalogo> erros)
    {
        switch (regra.Forma, regra.AgruparPor)
        {
            case (FormaDeContagem.Contagem, not null):
                erros.Add(new(codigo, "contagem simples não agrupa: agruparPor precisa ser nulo."));
                return;
            case (FormaDeContagem.ContagemDistinta, null):
                erros.Add(new(codigo, "contagem distinta precisa de agruparPor."));
                return;
            case (_, null):
                return;
        }

        var fonteDoCampo = regra.AgruparPor == Agrupamento.TipoDeEvento
            ? FonteDeContagem.PremioParticipacao
            : FonteDeContagem.Medalha;
        if (fonte != fonteDoCampo)
        {
            erros.Add(new(codigo, $"agruparPor {regra.AgruparPor} só existe na fonte {fonteDoCampo}."));
        }
    }

    private static void ValidarAlvo(ItemConquista conquista, List<ErroDeCatalogo> erros)
    {
        var codigo = conquista.Codigo;

        switch (conquista.Regra.Alvo)
        {
            case null:
                erros.Add(new(codigo, "regra sem alvo."));
                break;
            case { Fixo: { } fixo } when fixo < 1:
                erros.Add(new(codigo, $"alvo {fixo}: precisa ser ao menos 1."));
                break;
            case { EhTodasAsMatriculasDoPeriodo: true }:
                if (conquista.Regra.AgruparPor != Agrupamento.Materia)
                {
                    erros.Add(new(codigo, "alvo todasAsMatriculasDoPeriodo só faz sentido agrupando por matéria."));
                }

                // O alvo só existe completo no fim do período.
                if (conquista.Gatilho != Gatilho.FechamentoDePeriodo)
                {
                    erros.Add(new(codigo, "alvo todasAsMatriculasDoPeriodo exige gatilho fechamentoDePeriodo."));
                }

                break;
        }
    }

    private static void ValidarFamilias(Catalogo catalogo, List<ErroDeCatalogo> erros)
    {
        var familias = new Dictionary<string, Familia>(StringComparer.Ordinal);
        foreach (var familia in catalogo.Familias)
        {
            if (!familias.TryAdd(familia.Codigo, familia))
            {
                erros.Add(new(familia.Codigo, "família repetida."));
            }

            if (string.IsNullOrWhiteSpace(familia.Nome))
            {
                erros.Add(new(familia.Codigo, "família sem nome."));
            }
        }

        foreach (var item in catalogo.Itens.Where(item => item.Familia is not null && !familias.ContainsKey(item.Familia)))
        {
            erros.Add(new(item.Codigo, $"família {item.Familia} não existe."));
        }

        foreach (var familia in familias.Values)
        {
            var passos = catalogo.Itens
                .Where(item => item.Familia == familia.Codigo && item.OrdemNaFamilia is not null)
                .OrderBy(item => item.OrdemNaFamilia)
                .ToList();

            var ordens = passos.Select(item => item.OrdemNaFamilia!.Value);
            if (!ordens.SequenceEqual(Enumerable.Range(1, familia.Passos)))
            {
                erros.Add(new(
                    familia.Codigo,
                    $"declara {familia.Passos} passos, mas as ordens dos itens são [{string.Join(", ", ordens)}]."));
            }

            for (var i = 1; i < passos.Count; i++)
            {
                if (passos[i].Pontos <= passos[i - 1].Pontos)
                {
                    erros.Add(new(
                        familia.Codigo,
                        $"os pontos precisam subir a cada passo: {passos[i - 1].Codigo} vale {passos[i - 1].Pontos} " +
                        $"e {passos[i].Codigo} vale {passos[i].Pontos}."));
                }
            }
        }
    }
}
