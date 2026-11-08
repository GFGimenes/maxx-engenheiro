using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Tests.Catalogo;
using static Maxx.Domain.Tests.Academico.Calendario2026;
using static Maxx.Domain.Tests.Conquistas.Fatos;

namespace Maxx.Domain.Tests.Conquistas;

// O fim e os semestres de uma instância, que o worker usa para a vigência e para o alvo
// dinâmico, e as notificações de conquista.
public class InstanciaENotificacaoTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);

    private readonly Fatos _fatos = new();

    [Fact]
    public void Fim_da_instancia_e_o_ultimo_dia_do_bimestre_do_semestre_ou_do_ano()
    {
        Assert.Equal(Data(9, 26), _fatos.InstanciaDoBimestre(3).Fim(_fatos.Calendario));
        Assert.Equal(Data(6, 27), _fatos.InstanciaDoSemestre(1).Fim(_fatos.Calendario));
        Assert.Equal(Data(12, 5), _fatos.InstanciaDoAno.Fim(_fatos.Calendario));
    }

    [Fact]
    public void Periodo_curso_e_referencia_desconhecida_nao_tem_fim()
    {
        Assert.Null(InstanciaDoCurso.Fim(_fatos.Calendario));
        Assert.Null(new InstanciaDePeriodo(Periodo.Bimestre, Guid.CreateVersion7()).Fim(_fatos.Calendario));
    }

    [Fact]
    public void Semestres_da_instancia_sao_os_que_ela_cobre()
    {
        Assert.Equal([_fatos.Semestre(2)], _fatos.InstanciaDoBimestre(4).Semestres(_fatos.Calendario));
        Assert.Equal([_fatos.Semestre(1)], _fatos.InstanciaDoSemestre(1).Semestres(_fatos.Calendario));
        Assert.Equal([_fatos.Semestre(1), _fatos.Semestre(2)], _fatos.InstanciaDoAno.Semestres(_fatos.Calendario));
        Assert.Empty(InstanciaDoCurso.Semestres(_fatos.Calendario));
    }

    private Conquista Concedida()
    {
        var instancia = _fatos.InstanciaDoBimestre(4);
        var avaliacao = new AvaliadorDeConquista().Avaliar(CatalogoDeTeste.C02, instancia, _fatos.Medalhas("M01", 4, 3), Contexto());
        return Conquista.Conceder(CatalogoDeTeste.C02, 1, Aluno, instancia, avaliacao, _agora);
    }

    [Fact]
    public void Conquista_obtida_notifica_o_aluno_com_a_conquista_como_referencia()
    {
        var conquista = Concedida();

        var notificacao = Notificacao.ConquistaObtida(conquista, _agora.AddSeconds(1));

        Assert.Equal((Aluno, TipoDeNotificacao.ConquistaObtida, conquista.Id), (notificacao.UsuarioId, notificacao.Tipo, notificacao.ReferenciaId));
        Assert.Throws<DomainException>(() => Notificacao.ConquistaRemovida(conquista, _agora));
    }

    [Fact]
    public void Conquista_removida_notifica_e_ja_nao_notifica_como_obtida()
    {
        var conquista = Concedida();
        conquista.Recalcular(Avaliacao.De(0, 3, []), _agora.AddDays(1));

        var notificacao = Notificacao.ConquistaRemovida(conquista, _agora.AddDays(1));

        Assert.Equal((Aluno, TipoDeNotificacao.ConquistaRemovida, conquista.Id), (notificacao.UsuarioId, notificacao.Tipo, notificacao.ReferenciaId));
        Assert.Throws<DomainException>(() => Notificacao.ConquistaObtida(conquista, _agora));
    }
}
