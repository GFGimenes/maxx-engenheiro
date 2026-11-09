using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Identidade;

// O registro da purga de um lote da demonstração (passo 12, P5).
public class PurgaDaDemonstracaoTests
{
    private static readonly DateTimeOffset _agora = new(2026, 12, 6, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid _coordenacao = Guid.CreateVersion7();

    [Fact]
    public void Registra_lote_contagens_quem_e_quando()
    {
        var purga = new PurgaDaDemonstracao(" banca-1 ", 12, 30, _coordenacao, _agora);

        Assert.Equal(("banca-1", 12, 30, _coordenacao, _agora), (purga.Lote, purga.Alunos, purga.Fotos, purga.PurgadoPorId, purga.PurgadoEm));
    }

    [Fact]
    public void Lote_vazio_sem_aluno_ou_com_fotos_negativas_e_recusado()
    {
        Assert.Throws<DomainException>(() => new PurgaDaDemonstracao(" ", 1, 0, _coordenacao, _agora));
        Assert.Throws<DomainException>(() => new PurgaDaDemonstracao("banca-1", 0, 0, _coordenacao, _agora));
        Assert.Throws<DomainException>(() => new PurgaDaDemonstracao("banca-1", 1, -1, _coordenacao, _agora));
    }
}
