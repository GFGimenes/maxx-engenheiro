using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Identidade;

// O registro da purga de um lote da demonstração (passo 12, P5; passo 14, contagens por papel).
public class PurgaDaDemonstracaoTests
{
    private static readonly DateTimeOffset _agora = new(2026, 12, 6, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid _coordenacao = Guid.CreateVersion7();

    [Fact]
    public void Registra_lote_contagens_quem_e_quando()
    {
        var purga = new PurgaDaDemonstracao(" banca-1 ", 12, 3, 2, 30, _coordenacao, _agora);

        Assert.Equal(
            ("banca-1", 12, 3, 2, 30, _coordenacao, _agora),
            (purga.Lote, purga.Alunos, purga.Professores, purga.Coordenacoes, purga.Fotos, purga.PurgadoPorId, purga.PurgadoEm));
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 0, 1)]
    public void Basta_uma_conta_de_qualquer_papel(int alunos, int professores, int coordenacoes)
    {
        var purga = new PurgaDaDemonstracao("banca-1", alunos, professores, coordenacoes, 0, _coordenacao, _agora);

        Assert.Equal(1, purga.Alunos + purga.Professores + purga.Coordenacoes);
    }

    [Fact]
    public void Lote_vazio_sem_conta_ou_com_contagens_negativas_e_recusado()
    {
        Assert.Throws<DomainException>(() => new PurgaDaDemonstracao(" ", 1, 0, 0, 0, _coordenacao, _agora));
        Assert.Throws<DomainException>(() => new PurgaDaDemonstracao("banca-1", 0, 0, 0, 0, _coordenacao, _agora));
        Assert.Throws<DomainException>(() => new PurgaDaDemonstracao("banca-1", 2, -1, 0, 0, _coordenacao, _agora));
        Assert.Throws<DomainException>(() => new PurgaDaDemonstracao("banca-1", 2, 0, -1, 0, _coordenacao, _agora));
        Assert.Throws<DomainException>(() => new PurgaDaDemonstracao("banca-1", 1, 0, 0, -1, _coordenacao, _agora));
    }
}
