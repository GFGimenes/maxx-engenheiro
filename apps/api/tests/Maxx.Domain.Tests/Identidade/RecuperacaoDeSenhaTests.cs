using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Identidade;

public class RecuperacaoDeSenhaTests
{
    private static readonly DateTimeOffset _pedido = new(2026, 11, 3, 15, 0, 0, TimeSpan.Zero);
    private static readonly Curso _cc = new("Ciência da Computação", "CC", 8);

    private static Usuario Ana() => Usuario.CadastrarAluno(
        CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", _cc, 3),
        _cc,
        "Ana",
        "anaSouza",
        "hash-antigo",
        _pedido.AddDays(-60));

    [Fact]
    public void Pedido_nasce_aberto_e_sem_token()
    {
        var ana = Ana();

        var recuperacao = RecuperacaoDeSenha.Solicitar(ana.Id, _pedido);

        Assert.Equal(ana.Id, recuperacao.UsuarioId);
        Assert.Null(recuperacao.Hash);
        Assert.True(recuperacao.EstaAberta);
        Assert.Equal(_pedido, recuperacao.SolicitadaEm);
        Assert.Null(recuperacao.UsadaEm);
    }

    [Fact]
    public void Token_vale_30_minutos_contados_do_envio()
    {
        var recuperacao = RecuperacaoDeSenha.Solicitar(Ana().Id, _pedido);
        var envio = _pedido.AddMinutes(2);

        recuperacao.EmitirToken("T", envio);

        Assert.Equal("T", recuperacao.Hash);
        Assert.Equal(_pedido, recuperacao.SolicitadaEm);
        Assert.Equal(envio.AddMinutes(30), recuperacao.ExpiraEm);
    }

    [Fact]
    public void Nova_emissao_troca_o_token_e_o_prazo()
    {
        var recuperacao = ComToken(Ana().Id, "T1", _pedido);

        recuperacao.EmitirToken("T2", _pedido.AddMinutes(1));

        Assert.Equal("T2", recuperacao.Hash);
        Assert.Equal(_pedido.AddMinutes(31), recuperacao.ExpiraEm);
    }

    [Fact]
    public void Pedido_encerrado_nao_recebe_token()
    {
        var ana = Ana();
        var usado = ComToken(ana.Id, "T1", _pedido);
        var invalidado = RecuperacaoDeSenha.Solicitar(ana.Id, _pedido);
        usado.Concluir(ana, "hash-novo", [], [invalidado], _pedido.AddMinutes(1));

        Assert.False(usado.EstaAberta);
        Assert.False(invalidado.EstaAberta);
        Assert.Throws<InvalidOperationException>(() => usado.EmitirToken("T2", _pedido.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => invalidado.EmitirToken("T3", _pedido.AddMinutes(2)));
        Assert.Null(invalidado.Hash);
    }

    [Fact]
    public void Concluir_define_a_senha_marca_o_token_usado_e_revoga_todas_as_familias()
    {
        var ana = Ana();
        var celular = FamiliaDeSessao.Abrir(ana.Id, "C1", _pedido.AddDays(-5));
        var notebook = FamiliaDeSessao.Abrir(ana.Id, "N1", _pedido.AddDays(-1));
        var recuperacao = ComToken(ana.Id, "T", _pedido);
        var conclusao = _pedido.AddMinutes(10);

        var resultado = recuperacao.Concluir(ana, "hash-novo", [celular, notebook], [], conclusao);

        Assert.Equal(ResultadoDaRecuperacao.Concluida, resultado);
        Assert.Equal("hash-novo", ana.HashDaSenha);
        Assert.Equal(conclusao, recuperacao.UsadaEm);
        Assert.All([celular, notebook], familia =>
        {
            Assert.False(familia.EstaAtiva);
            Assert.Equal(MotivoDaRevogacao.SenhaRedefinida, familia.MotivoDaRevogacao);
            Assert.Equal(conclusao, familia.RevogadaEm);
        });
    }

    [Fact]
    public void Familia_ja_revogada_mantem_o_motivo_original()
    {
        var ana = Ana();
        var antiga = FamiliaDeSessao.Abrir(ana.Id, "A1", _pedido.AddDays(-5));
        antiga.Revogar(MotivoDaRevogacao.Logout, _pedido.AddDays(-4));
        var recuperacao = ComToken(ana.Id, "T", _pedido);

        recuperacao.Concluir(ana, "hash-novo", [antiga], [], _pedido.AddMinutes(1));

        Assert.Equal(MotivoDaRevogacao.Logout, antiga.MotivoDaRevogacao);
    }

    [Fact]
    public void Primeiro_acesso_de_quem_veio_do_seed_e_a_mesma_conclusao()
    {
        var renata = Usuario.CriarPeloSeed(
            CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque"), "renataAlbuquerque", _pedido.AddDays(-30));
        var recuperacao = ComToken(renata.Id, "T", _pedido);

        var resultado = recuperacao.Concluir(renata, "primeira-senha", [], [], _pedido.AddMinutes(5));

        Assert.Equal(ResultadoDaRecuperacao.Concluida, resultado);
        Assert.True(renata.TemSenha);
    }

    [Fact]
    public void Token_expirado_nao_muda_nada()
    {
        var ana = Ana();
        var familia = FamiliaDeSessao.Abrir(ana.Id, "C1", _pedido.AddDays(-5));
        var recuperacao = ComToken(ana.Id, "T", _pedido);

        var resultado = recuperacao.Concluir(ana, "hash-novo", [familia], [], _pedido.AddMinutes(30).AddSeconds(1));

        Assert.Equal(ResultadoDaRecuperacao.Expirada, resultado);
        Assert.Equal("hash-antigo", ana.HashDaSenha);
        Assert.Null(recuperacao.UsadaEm);
        Assert.True(familia.EstaAtiva);
    }

    [Fact]
    public void Token_no_ultimo_instante_da_validade_ainda_conclui()
    {
        var ana = Ana();
        var recuperacao = ComToken(ana.Id, "T", _pedido);

        Assert.Equal(ResultadoDaRecuperacao.Concluida, recuperacao.Concluir(ana, "hash-novo", [], [], _pedido.AddMinutes(30)));
    }

    [Fact]
    public void Token_usado_nao_conclui_de_novo()
    {
        var ana = Ana();
        var recuperacao = ComToken(ana.Id, "T", _pedido);
        recuperacao.Concluir(ana, "hash-novo", [], [], _pedido.AddMinutes(1));
        var depois = FamiliaDeSessao.Abrir(ana.Id, "D1", _pedido.AddMinutes(2));

        var resultado = recuperacao.Concluir(ana, "hash-de-quem-pegou-o-link", [depois], [], _pedido.AddMinutes(3));

        Assert.Equal(ResultadoDaRecuperacao.JaUsada, resultado);
        Assert.Equal("hash-novo", ana.HashDaSenha);
        Assert.Equal(_pedido.AddMinutes(1), recuperacao.UsadaEm);
        Assert.True(depois.EstaAtiva);
    }

    [Fact]
    public void Recuperacao_de_outro_usuario_e_erro_de_quem_chama()
    {
        var ana = Ana();
        var recuperacao = ComToken(Guid.CreateVersion7(), "T", _pedido);

        Assert.Throws<ArgumentException>(() => recuperacao.Concluir(ana, "hash-novo", [], [], _pedido));
    }

    [Fact]
    public void Familia_de_outro_usuario_e_erro_de_quem_chama()
    {
        var ana = Ana();
        var deOutro = FamiliaDeSessao.Abrir(Guid.CreateVersion7(), "O1", _pedido);
        var recuperacao = ComToken(ana.Id, "T", _pedido);

        Assert.Throws<ArgumentException>(() => recuperacao.Concluir(ana, "hash-novo", [deOutro], [], _pedido));
        Assert.Equal("hash-antigo", ana.HashDaSenha);
        Assert.Null(recuperacao.UsadaEm);
    }

    [Fact]
    public void Concluir_invalida_os_outros_pedidos_pendentes_do_usuario()
    {
        var ana = Ana();
        var primeiro = ComToken(ana.Id, "T1", _pedido);
        var segundo = ComToken(ana.Id, "T2", _pedido.AddMinutes(2));
        var terceiro = ComToken(ana.Id, "T3", _pedido.AddMinutes(4));
        var conclusao = _pedido.AddMinutes(5);

        segundo.Concluir(ana, "hash-novo", [], [primeiro, segundo, terceiro], conclusao);

        Assert.Equal(conclusao, primeiro.InvalidadaEm);
        Assert.Equal(conclusao, terceiro.InvalidadaEm);
        Assert.Null(segundo.InvalidadaEm);
        Assert.Equal(conclusao, segundo.UsadaEm);
    }

    [Fact]
    public void Link_antigo_invalidado_nao_redefine_a_senha_de_novo()
    {
        var ana = Ana();
        var antigo = ComToken(ana.Id, "T1", _pedido);
        var novo = ComToken(ana.Id, "T2", _pedido.AddMinutes(1));
        novo.Concluir(ana, "hash-novo", [], [antigo], _pedido.AddMinutes(2));
        var sessaoNova = FamiliaDeSessao.Abrir(ana.Id, "S1", _pedido.AddMinutes(3));

        var resultado = antigo.Concluir(ana, "hash-de-quem-pegou-o-link", [sessaoNova], [novo], _pedido.AddMinutes(4));

        Assert.Equal(ResultadoDaRecuperacao.Invalidada, resultado);
        Assert.Equal("hash-novo", ana.HashDaSenha);
        Assert.Null(antigo.UsadaEm);
        Assert.True(sessaoNova.EstaAtiva);
    }

    [Fact]
    public void Pedido_ja_usado_nao_vira_invalidado()
    {
        var ana = Ana();
        var usado = ComToken(ana.Id, "T1", _pedido);
        usado.Concluir(ana, "hash-1", [], [], _pedido.AddMinutes(1));
        var depois = ComToken(ana.Id, "T2", _pedido.AddMinutes(10));

        depois.Concluir(ana, "hash-2", [], [usado], _pedido.AddMinutes(11));

        Assert.Null(usado.InvalidadaEm);
        Assert.Equal(_pedido.AddMinutes(1), usado.UsadaEm);
    }

    [Fact]
    public void Pedido_expirado_nao_invalida_ninguem()
    {
        var ana = Ana();
        var expirado = ComToken(ana.Id, "T1", _pedido);
        var aberto = ComToken(ana.Id, "T2", _pedido.AddMinutes(25));

        var resultado = expirado.Concluir(ana, "hash-novo", [], [aberto], _pedido.AddMinutes(31));

        Assert.Equal(ResultadoDaRecuperacao.Expirada, resultado);
        Assert.Null(aberto.InvalidadaEm);
    }

    [Fact]
    public void Pedido_de_outro_usuario_entre_os_pendentes_e_erro_de_quem_chama()
    {
        var ana = Ana();
        var deOutro = ComToken(Guid.CreateVersion7(), "O1", _pedido);
        var recuperacao = ComToken(ana.Id, "T", _pedido);

        Assert.Throws<ArgumentException>(() => recuperacao.Concluir(ana, "hash-novo", [], [deOutro], _pedido));
        Assert.Equal("hash-antigo", ana.HashDaSenha);
    }

    // Pedido com o token já emitido, no mesmo instante do pedido.
    private static RecuperacaoDeSenha ComToken(Guid usuarioId, string hash, DateTimeOffset agora)
    {
        var recuperacao = RecuperacaoDeSenha.Solicitar(usuarioId, agora);
        recuperacao.EmitirToken(hash, agora);
        return recuperacao;
    }
}
