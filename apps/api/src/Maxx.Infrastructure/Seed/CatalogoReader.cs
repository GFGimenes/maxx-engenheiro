using System.Text.Json;
using Maxx.Domain.Catalogo;

namespace Maxx.Infrastructure.Seed;

// Erros vazios: o catálogo foi lido e passou no validador do domínio.
public sealed record CatalogoReadResult(Catalogo? Catalogo, IReadOnlyList<ErroDeCatalogo> Erros)
{
    public bool Valido => Catalogo is not null && Erros.Count == 0;
}

// Lê os quatro arquivos de data/catalogo/ para o modelo do domínio e roda o validador.
// Estrito de propósito: campo desconhecido, valor de enumeração fora da lista e tipo de
// item trocado de arquivo são erro, porque um erro de digitação no JSON vira regra errada.
public static partial class CatalogoReader
{
    public const string ArquivoDeMedalhas = "medalhas.json";
    public const string ArquivoDePremiosParticipacao = "premios-participacao.json";
    public const string ArquivoDeConquistas = "conquistas.json";
    public const string ArquivoDeFamilias = "familias.json";

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    public static CatalogoReadResult Ler(string diretorio)
    {
        var erros = new List<ErroDeCatalogo>();
        var itens = new List<ItemCatalogo>();

        foreach (var (arquivo, tipo) in new[]
        {
            (ArquivoDeMedalhas, TipoDeItem.Medalha),
            (ArquivoDePremiosParticipacao, TipoDeItem.PremioParticipacao),
            (ArquivoDeConquistas, TipoDeItem.Conquista),
        })
        {
            var conteudo = LerArquivo<ItemJson>(diretorio, arquivo, Camel(tipo.ToString()), erros);
            foreach (var item in conteudo)
            {
                if (MapearItem(item, tipo, arquivo, erros) is { } mapeado)
                {
                    itens.Add(mapeado);
                }
            }
        }

        var familias = LerArquivo<FamiliaJson>(diretorio, ArquivoDeFamilias, "familia", erros)
            .Select(familia => MapearFamilia(familia, erros))
            .OfType<Familia>()
            .ToList();

        if (erros.Count > 0)
        {
            return new(null, erros);
        }

        var catalogo = new Catalogo(itens, familias);
        return new(catalogo, CatalogoValidator.Validar(catalogo));
    }

    private static List<T> LerArquivo<T>(string diretorio, string arquivo, string tipoEsperado, List<ErroDeCatalogo> erros)
    {
        var caminho = Path.Combine(diretorio, arquivo);
        if (!File.Exists(caminho))
        {
            erros.Add(new(arquivo, $"arquivo não encontrado em {diretorio}."));
            return [];
        }

        ArquivoJson<T>? conteudo;
        try
        {
            conteudo = JsonSerializer.Deserialize<ArquivoJson<T>>(File.ReadAllText(caminho), _json);
        }
        catch (JsonException ex)
        {
            erros.Add(new(arquivo, ex.Message));
            return [];
        }

        if (conteudo?.Tipo != tipoEsperado)
        {
            erros.Add(new(arquivo, $"tipo do arquivo é \"{conteudo?.Tipo}\", esperado \"{tipoEsperado}\"."));
            return [];
        }

        if (conteudo.Itens is not { Count: > 0 } lidos)
        {
            erros.Add(new(arquivo, "arquivo sem itens."));
            return [];
        }

        return lidos;
    }

    private static ItemCatalogo? MapearItem(ItemJson json, TipoDeItem tipo, string arquivo, List<ErroDeCatalogo> erros)
    {
        var campos = new Campos(json.Codigo ?? arquivo, erros);
        var codigo = campos.Texto(json.Codigo, "codigo");

        if (campos.Enum<TipoDeItem>(json.Tipo, "tipo") != tipo && !campos.Falhou)
        {
            campos.Erro($"item do tipo {json.Tipo} no arquivo {arquivo}.");
        }

        var nome = campos.Texto(json.Nome, "nome");
        var descricao = campos.Texto(json.Descricao, "descricao");
        var tier = campos.Enum<Tier>(json.Tier, "tier");
        var pontos = campos.Exigir(json.Pontos, "pontos");
        var vigencia = new Vigencia(json.Vigencia?.Inicio, json.Vigencia?.Fim);

        ItemCatalogo item = tipo switch
        {
            TipoDeItem.Medalha => new ItemMedalha
            {
                Codigo = codigo,
                Nome = nome,
                Descricao = descricao,
                Tier = tier,
                Pontos = pontos,
                Base = campos.Enum<BaseDaMedalha>(json.Base, "base"),
                PeriodoDeReferencia = campos.Enum<Periodo>(json.PeriodoDeReferencia, "periodoDeReferencia"),
                Repetivel = campos.Exigir(json.Repetivel, "repetivel"),
                LimitePorPeriodo = campos.Exigir(json.LimitePorPeriodo, "limitePorPeriodo"),
                ChaveDeDeduplicacao = campos.Enums<CampoDeDeduplicacao>(json.ChaveDeDeduplicacao, "chaveDeDeduplicacao"),
            },
            TipoDeItem.PremioParticipacao => new ItemPremioParticipacao
            {
                Codigo = codigo,
                Nome = nome,
                Descricao = descricao,
                Tier = tier,
                Pontos = pontos,
                TipoDeEvento = campos.Texto(json.TipoDeEvento, "tipoDeEvento"),
                FormaDoEvento = campos.Enum<FormaDoEvento>(json.FormaDoEvento, "formaDoEvento"),
                ExigeFoto = campos.Exigir(json.ExigeFoto, "exigeFoto"),
                PeriodoDeReferencia = campos.Enum<Periodo>(json.PeriodoDeReferencia, "periodoDeReferencia"),
                ChaveDeDeduplicacao = campos.Enums<CampoDeDeduplicacao>(json.ChaveDeDeduplicacao, "chaveDeDeduplicacao"),
            },
            _ => new ItemConquista
            {
                Codigo = codigo,
                Nome = nome,
                Descricao = descricao,
                Tier = tier,
                Pontos = pontos,
                Periodo = campos.Enum<Periodo>(json.Periodo, "periodo"),
                Gatilho = campos.Enum<Gatilho>(json.Gatilho, "gatilho"),
                Regra = MapearRegra(json.Regra, campos),
            },
        };

        item = item with
        {
            Familia = json.Familia,
            OrdemNaFamilia = json.OrdemNaFamilia,
            RestricaoCurso = json.RestricaoCurso,
            Vigencia = vigencia,
        };

        return campos.Falhou ? null : item;
    }

    private static RegraDeConquista MapearRegra(RegraJson? json, Campos campos)
    {
        if (json is null)
        {
            campos.Erro("campo regra ausente.");
            return new() { Forma = FormaDeContagem.Contagem };
        }

        return new()
        {
            Forma = campos.Enum<FormaDeContagem>(json.Forma, "regra.forma"),
            Fonte = json.Fonte is null ? null : campos.Enum<FonteDeContagem>(json.Fonte, "regra.fonte"),
            FiltroItens = json.FiltroItens,
            AgruparPor = json.AgruparPor is null ? null : campos.Enum<Agrupamento>(json.AgruparPor, "regra.agruparPor"),
            Alvo = MapearAlvo(json.Alvo, campos),
            Implementacao = json.Implementacao,
        };
    }

    private static AlvoDeConquista? MapearAlvo(JsonElement? alvo, Campos campos)
    {
        switch (alvo)
        {
            case null or { ValueKind: JsonValueKind.Null }:
                return null;
            case { ValueKind: JsonValueKind.Number } numero when numero.TryGetInt32(out var fixo):
                return AlvoDeConquista.De(fixo);
            case { ValueKind: JsonValueKind.String } texto
                when texto.GetString() == Camel(nameof(AlvoDeConquista.TodasAsMatriculasDoPeriodo)):
                return AlvoDeConquista.TodasAsMatriculasDoPeriodo;
            default:
                campos.Erro($"regra.alvo {alvo} não é inteiro nem \"todasAsMatriculasDoPeriodo\".");
                return null;
        }
    }

    private static Familia? MapearFamilia(FamiliaJson json, List<ErroDeCatalogo> erros)
    {
        var campos = new Campos(json.Codigo ?? ArquivoDeFamilias, erros);
        var familia = new Familia
        {
            Codigo = campos.Texto(json.Codigo, "codigo"),
            Nome = campos.Texto(json.Nome, "nome"),
            Descricao = campos.Texto(json.Descricao, "descricao"),
            Passos = campos.Exigir(json.Passos, "passos"),
        };

        return campos.Falhou ? null : familia;
    }

    private static string Camel(string nome) => JsonNamingPolicy.CamelCase.ConvertName(nome);

    // Junta os erros de um item sem parar no primeiro.
    private sealed class Campos(string referencia, List<ErroDeCatalogo> erros)
    {
        public bool Falhou { get; private set; }

        public void Erro(string mensagem)
        {
            Falhou = true;
            erros.Add(new(referencia, mensagem));
        }

        public string Texto(string? valor, string campo)
        {
            if (valor is null)
            {
                Erro($"campo {campo} ausente.");
            }

            return valor ?? "";
        }

        public T Exigir<T>(T? valor, string campo)
            where T : struct
        {
            if (valor is null)
            {
                Erro($"campo {campo} ausente.");
            }

            return valor ?? default;
        }

        // Só o nome exato em camelCase: nada de número, nada de maiúscula trocada.
        public T Enum<T>(string? valor, string campo)
            where T : struct, Enum
        {
            if (valor is null)
            {
                Erro($"campo {campo} ausente.");
                return default;
            }

            foreach (var candidato in System.Enum.GetValues<T>())
            {
                if (Camel(candidato.ToString()) == valor)
                {
                    return candidato;
                }
            }

            Erro($"{campo} \"{valor}\" desconhecido.");
            return default;
        }

        public IReadOnlyList<T> Enums<T>(List<string>? valores, string campo)
            where T : struct, Enum
        {
            if (valores is null)
            {
                Erro($"campo {campo} ausente.");
                return [];
            }

            return [.. valores.Select(valor => Enum<T>(valor, campo))];
        }
    }
}
