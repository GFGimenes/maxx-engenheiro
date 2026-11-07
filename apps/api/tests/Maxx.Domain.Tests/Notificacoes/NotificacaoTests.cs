using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;
using Maxx.Domain.Tests.Academico;
using Maxx.Domain.Tests.Catalogo;

namespace Maxx.Domain.Tests.Notificacoes;

public class NotificacaoTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);

    private static Medalha Medalha()
    {
        var cc = new Curso("Ciência da Computação", "CC", 8);
        var semestre = Calendario2026.Segundo();
        var professor = Usuario.CriarPeloSeed(
            CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), "renataA", _agora);
        var aluna = Usuario.CadastrarAluno(
            CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", cc, 3), cc, "Ana", "anaSouza", "hash", _agora);
        return Domain.Premios.Medalha.Conceder(
            CatalogoDeTeste.M01, 1, aluna, "CC", new Turma(new Materia(cc, "Banco de Dados"), semestre), semestre, professor, [], _agora).Medalha!;
    }

    [Fact]
    public void Medalha_recebida_notifica_o_aluno_com_a_medalha_como_referencia()
    {
        var medalha = Medalha();

        var notificacao = Notificacao.MedalhaRecebida(medalha, _agora.AddSeconds(2));

        Assert.Equal(medalha.AlunoId, notificacao.UsuarioId);
        Assert.Equal(TipoDeNotificacao.MedalhaRecebida, notificacao.Tipo);
        Assert.Equal(medalha.Id, notificacao.ReferenciaId);
        Assert.Equal(_agora.AddSeconds(2), notificacao.CriadaEm);
        Assert.Null(notificacao.LidaEm);
    }

    [Fact]
    public void Medalha_anulada_nao_notifica()
    {
        var medalha = Medalha();
        medalha.Anular(Guid.CreateVersion7(), _agora.AddSeconds(1));

        Assert.Throws<DomainException>(() => Notificacao.MedalhaRecebida(medalha, _agora.AddSeconds(2)));
    }
}
