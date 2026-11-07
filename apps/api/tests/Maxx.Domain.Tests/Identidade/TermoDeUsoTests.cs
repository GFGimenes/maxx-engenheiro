using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Identidade;

public class TermoDeUsoTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 6, 15, 0, 0, TimeSpan.Zero);
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);

    private static Usuario Ana() => Usuario.CadastrarAluno(
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3), _cc, "Ana", "anaSouza", "hash", _agora);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Versao_comeca_em_1(int versao)
    {
        Assert.Throws<DomainException>(() => new TermoDeUso(versao, "Texto.", _agora));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Termo_sem_texto_nao_existe(string texto)
    {
        Assert.Throws<DomainException>(() => new TermoDeUso(1, texto, _agora));
    }

    [Fact]
    public void Aceite_guarda_a_versao_e_o_instante()
    {
        var ana = Ana();

        ana.AceitarTermo(new TermoDeUso(3, "Texto.", _agora), _agora.AddMinutes(1));

        var aceite = Assert.Single(ana.AceitesDeTermo);
        Assert.Equal(ana.Id, aceite.UsuarioId);
        Assert.Equal(3, aceite.Versao);
        Assert.Equal(_agora.AddMinutes(1), aceite.AceitoEm);
    }

    [Fact]
    public void Aceitar_a_mesma_versao_de_novo_mantem_o_primeiro_registro()
    {
        var ana = Ana();
        var termo = new TermoDeUso(1, "Texto.", _agora);

        ana.AceitarTermo(termo, _agora);
        ana.AceitarTermo(termo, _agora.AddDays(1));

        Assert.Equal(_agora, Assert.Single(ana.AceitesDeTermo).AceitoEm);
    }

    [Fact]
    public void Versao_nova_entra_ao_lado_da_antiga()
    {
        var ana = Ana();

        ana.AceitarTermo(new TermoDeUso(1, "Texto.", _agora), _agora);
        ana.AceitarTermo(new TermoDeUso(2, "Texto novo.", _agora), _agora.AddDays(1));

        Assert.Equal([1, 2], ana.AceitesDeTermo.Select(aceite => aceite.Versao));
    }
}
