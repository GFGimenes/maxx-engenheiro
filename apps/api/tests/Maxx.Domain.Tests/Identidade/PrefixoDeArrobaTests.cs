using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Identidade;

public class PrefixoDeArrobaTests
{
    private static readonly Func<string, bool> _tudoLivre = _ => true;

    [Theory]
    [InlineData("Guilherme Gimenes", "guilhermeGimenes")]
    [InlineData("Ana Souza", "anaSouza")]
    [InlineData("  ANA   SOUZA  ", "anaSouza")]
    [InlineData("João Conceição", "joaoConceicao")]
    [InlineData("Ângela Müller", "angelaMuller")]
    [InlineData("Maria da Silva", "mariaSilva")]
    [InlineData("Pedro dos Santos e Souza", "pedroSouza")]
    [InlineData("Lucas De Oliveira", "lucasOliveira")]
    [InlineData("Luiza D'Ávila", "luizaDavila")]
    [InlineData("Coordenação UniMax", "coordenacaoUnimax")]
    public void Sugestao_usa_primeiro_e_ultimo_nome_sem_acento_e_sem_particula(string nome, string esperado)
    {
        Assert.Equal(esperado, PrefixoDeArroba.Sugerir(nome, _tudoLivre));
    }

    [Fact]
    public void Em_colisao_a_sugestao_inclui_o_primeiro_nome_do_meio()
    {
        var ocupados = Ocupados("mariaSilva");

        Assert.Equal("mariaClaraSilva", PrefixoDeArroba.Sugerir("Maria Clara Fernandes da Silva", ocupados));
    }

    [Fact]
    public void Particula_nao_conta_como_nome_do_meio()
    {
        var ocupados = Ocupados("mariaSilva");

        Assert.Equal("mariaSilva2", PrefixoDeArroba.Sugerir("Maria da Silva", ocupados));
    }

    [Fact]
    public void Sem_nome_do_meio_a_colisao_vai_direto_ao_numero()
    {
        var ocupados = Ocupados("anaSouza");

        Assert.Equal("anaSouza2", PrefixoDeArroba.Sugerir("Ana Souza", ocupados));
    }

    [Fact]
    public void Com_o_nome_do_meio_tambem_ocupado_o_numero_segue_do_primeiro_e_ultimo()
    {
        var ocupados = Ocupados("mariaSilva", "mariaClaraSilva", "mariaSilva2");

        Assert.Equal("mariaSilva3", PrefixoDeArroba.Sugerir("Maria Clara Silva", ocupados));
    }

    [Fact]
    public void Colisao_nao_diferencia_maiusculas()
    {
        var ocupados = Ocupados("ANASOUZA");

        Assert.Equal("anaSouza2", PrefixoDeArroba.Sugerir("Ana Souza", ocupados));
    }

    [Fact]
    public void Nome_de_uma_palavra_so_vira_o_prefixo_inteiro()
    {
        Assert.Equal("pele", PrefixoDeArroba.Sugerir("Pelé", _tudoLivre));
    }

    [Fact]
    public void Nome_curto_demais_ganha_numero_ate_chegar_ao_minimo()
    {
        Assert.Equal("li2", PrefixoDeArroba.Sugerir("Li", _tudoLivre));
    }

    [Fact]
    public void Sugestao_longa_e_cortada_em_30_e_o_numero_cabe_dentro_do_limite()
    {
        var nome = "Maximiliano Albuquerquecavalcantimonteiro";

        var primeira = PrefixoDeArroba.Sugerir(nome, _tudoLivre);
        var segunda = PrefixoDeArroba.Sugerir(nome, Ocupados(primeira));

        Assert.Equal("maximilianoAlbuquerquecavalcan", primeira);
        Assert.Equal(30, segunda.Length);
        Assert.EndsWith("2", segunda, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("da de dos")]
    [InlineData("!!!")]
    public void Nome_sem_letra_aproveitavel_e_recusado(string nome)
    {
        Assert.Throws<DomainException>(() => PrefixoDeArroba.Sugerir(nome, _tudoLivre));
    }

    [Fact]
    public void Toda_sugestao_passa_na_validacao()
    {
        var nomes = new[] { "Guilherme Gimenes", "Li", "Maria Clara da Silva", "Maximiliano Albuquerquecavalcantimonteiro" };

        foreach (var sugestao in nomes.SelectMany(nome => PrefixoDeArroba.Sugestoes(nome).Take(15)))
        {
            Assert.Equal(sugestao, PrefixoDeArroba.Validar(sugestao));
        }
    }

    [Theory]
    [InlineData("anaSouza")]
    [InlineData("ANA")]
    [InlineData("a1b")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234")]
    public void Prefixo_valido_e_aceito_como_foi_escrito(string prefixo)
    {
        Assert.Equal(prefixo, PrefixoDeArroba.Validar(prefixo));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("abcdefghijklmnopqrstuvwxyz12345")]
    [InlineData("1ana")]
    [InlineData("ana souza")]
    [InlineData("ana-souza")]
    [InlineData("ana.souza")]
    [InlineData("ana_souza")]
    [InlineData("joão")]
    [InlineData("conceição")]
    [InlineData("@anaSouza")]
    public void Prefixo_fora_do_formato_e_recusado(string prefixo)
    {
        Assert.Throws<DomainException>(() => PrefixoDeArroba.Validar(prefixo));
    }

    [Fact]
    public void Mesmo_prefixo_nao_diferencia_maiusculas()
    {
        Assert.True(PrefixoDeArroba.Mesmo("anaSouza", "ANASOUZA"));
        Assert.False(PrefixoDeArroba.Mesmo("anaSouza", "anaSouza2"));
    }

    private static Func<string, bool> Ocupados(params string[] prefixos)
    {
        var ocupados = new HashSet<string>(prefixos, StringComparer.OrdinalIgnoreCase);
        return prefixo => !ocupados.Contains(prefixo);
    }
}
