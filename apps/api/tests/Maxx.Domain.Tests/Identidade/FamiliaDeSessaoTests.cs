using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Identidade;

public class FamiliaDeSessaoTests
{
    private static readonly DateTimeOffset _login = new(2026, 11, 3, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid _usuarioId = Guid.CreateVersion7();

    private static FamiliaDeSessao Abrir() => FamiliaDeSessao.Abrir(_usuarioId, "R1", _login);

    [Fact]
    public void Login_abre_a_familia_com_o_primeiro_refresh_valido_por_30_dias()
    {
        var familia = Abrir();

        Assert.Equal(_usuarioId, familia.UsuarioId);
        Assert.True(familia.EstaAtiva);
        var r1 = Assert.Single(familia.Tokens);
        Assert.Equal("R1", r1.Hash);
        Assert.Equal(familia.Id, r1.FamiliaId);
        Assert.Equal(_login.AddDays(30), r1.ExpiraEm);
        Assert.Null(r1.RotacionadoEm);
    }

    [Fact]
    public void Refresh_valido_rotaciona_e_o_novo_vale_30_dias_a_partir_da_rotacao()
    {
        var familia = Abrir();
        var depois = _login.AddDays(20);

        var resultado = familia.Rotacionar("R1", "R2", depois);

        Assert.Equal(ResultadoDoRefresh.Rotacionado, resultado);
        Assert.Equal(depois, familia.Tokens.Single(token => token.Hash == "R1").RotacionadoEm);
        var r2 = familia.Tokens.Single(token => token.Hash == "R2");
        Assert.Equal(depois.AddDays(30), r2.ExpiraEm);
        Assert.True(familia.EstaAtiva);
    }

    [Fact]
    public void Reuso_de_refresh_ja_rotacionado_revoga_a_familia_inteira()
    {
        // A sequência do ADR 0006: R1, R2, R1 de novo.
        var familia = Abrir();
        familia.Rotacionar("R1", "R2", _login.AddMinutes(15));
        var replay = _login.AddMinutes(16);

        var resultado = familia.Rotacionar("R1", "R3", replay);

        Assert.Equal(ResultadoDoRefresh.ReusoDetectado, resultado);
        Assert.False(familia.EstaAtiva);
        Assert.Equal(replay, familia.RevogadaEm);
        Assert.Equal(MotivoDaRevogacao.Reuso, familia.MotivoDaRevogacao);
        Assert.DoesNotContain(familia.Tokens, token => token.Hash == "R3");
    }

    [Fact]
    public void Depois_do_reuso_nem_o_refresh_mais_novo_vale()
    {
        var familia = Abrir();
        familia.Rotacionar("R1", "R2", _login.AddMinutes(15));
        familia.Rotacionar("R1", "R3", _login.AddMinutes(16));

        var resultado = familia.Rotacionar("R2", "R4", _login.AddMinutes(17));

        Assert.Equal(ResultadoDoRefresh.FamiliaRevogada, resultado);
        Assert.DoesNotContain(familia.Tokens, token => token.Hash == "R4");
    }

    [Fact]
    public void Refresh_expirado_e_recusado_sem_revogar_a_familia()
    {
        var familia = Abrir();

        var resultado = familia.Rotacionar("R1", "R2", _login.AddDays(30).AddSeconds(1));

        Assert.Equal(ResultadoDoRefresh.Expirado, resultado);
        Assert.Null(familia.RevogadaEm);
        Assert.Single(familia.Tokens);
    }

    [Fact]
    public void Refresh_no_ultimo_instante_da_validade_ainda_rotaciona()
    {
        var familia = Abrir();

        Assert.Equal(ResultadoDoRefresh.Rotacionado, familia.Rotacionar("R1", "R2", _login.AddDays(30)));
    }

    [Fact]
    public void Rotacao_renovada_mantem_a_sessao_viva_alem_dos_30_dias_do_login()
    {
        var familia = Abrir();
        familia.Rotacionar("R1", "R2", _login.AddDays(25));

        var resultado = familia.Rotacionar("R2", "R3", _login.AddDays(50));

        Assert.Equal(ResultadoDoRefresh.Rotacionado, resultado);
    }

    [Fact]
    public void Logout_revoga_a_familia_e_o_refresh_atual_deixa_de_valer()
    {
        var familia = Abrir();
        var saida = _login.AddHours(2);

        familia.Revogar(MotivoDaRevogacao.Logout, saida);

        Assert.False(familia.EstaAtiva);
        Assert.Equal(saida, familia.RevogadaEm);
        Assert.Equal(MotivoDaRevogacao.Logout, familia.MotivoDaRevogacao);
        Assert.Equal(ResultadoDoRefresh.FamiliaRevogada, familia.Rotacionar("R1", "R2", saida.AddSeconds(1)));
    }

    [Fact]
    public void Revogar_de_novo_mantem_o_primeiro_motivo_e_instante()
    {
        var familia = Abrir();
        familia.Revogar(MotivoDaRevogacao.Logout, _login.AddHours(1));

        familia.Revogar(MotivoDaRevogacao.SenhaRedefinida, _login.AddHours(2));

        Assert.Equal(MotivoDaRevogacao.Logout, familia.MotivoDaRevogacao);
        Assert.Equal(_login.AddHours(1), familia.RevogadaEm);
    }

    [Fact]
    public void Refresh_de_outra_familia_e_erro_de_quem_chama()
    {
        var familia = Abrir();

        Assert.Throws<ArgumentException>(() => familia.Rotacionar("de-outra", "R2", _login));
    }

    [Fact]
    public void Hash_novo_igual_a_um_ja_emitido_e_recusado()
    {
        var familia = Abrir();

        Assert.Throws<ArgumentException>(() => familia.Rotacionar("R1", "R1", _login));
    }
}
