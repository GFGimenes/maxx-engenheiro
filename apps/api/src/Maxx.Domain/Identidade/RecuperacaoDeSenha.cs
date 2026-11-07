namespace Maxx.Domain.Identidade;

// Um pedido de recuperação de senha, que também é o primeiro acesso de quem vem do seed
// (docs/fluxos/06-recuperacao-de-senha.md). Token de uso único, por 30 minutos; o domínio
// só vê o hash dele. O pedido nasce sem token: o worker o emite na hora de mandar o e-mail,
// para nenhum token esperar em claro na fila (decisoes/README.md, Envio de e-mail).
public sealed class RecuperacaoDeSenha
{
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(30);

    // Para o EF Core materializar do banco.
    private RecuperacaoDeSenha()
    {
    }

    private RecuperacaoDeSenha(Guid usuarioId, DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        UsuarioId = usuarioId;
        SolicitadaEm = agora;
        ExpiraEm = agora + Validade;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    // Nulo até o token ser emitido: um pedido sem token não é encontrado por link nenhum.
    public string? Hash { get; private set; }

    public DateTimeOffset SolicitadaEm { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    public DateTimeOffset? UsadaEm { get; private set; }

    // Outro pedido do mesmo usuário foi concluído antes deste.
    public DateTimeOffset? InvalidadaEm { get; private set; }

    // Nem usado nem invalidado: ainda pode receber token e ser concluído.
    public bool EstaAberta => UsadaEm is null && InvalidadaEm is null;

    public static RecuperacaoDeSenha Solicitar(Guid usuarioId, DateTimeOffset agora) => new(usuarioId, agora);

    // O token que vai no e-mail. Os 30 minutos contam daqui, do envio, e não do pedido. Uma
    // nova emissão (retentativa do envio) troca o hash: o token anterior nunca chegou a
    // ninguém, porque o envio dele falhou.
    public void EmitirToken(string hash, DateTimeOffset agora)
    {
        if (!EstaAberta)
        {
            throw new InvalidOperationException($"A recuperação {Id} já foi encerrada e não recebe token.");
        }

        Hash = Texto.Obrigatorio(hash, "O hash do token de recuperação");
        ExpiraEm = agora + Validade;
    }

    // Senha nova, todas as sessões do usuário derrubadas e os outros pedidos pendentes dele
    // invalidados, ou nada muda: um link antigo não redefine a senha de novo. A política já
    // passou sobre a senha em claro; aqui chega o hash. familiasDoUsuario e
    // outrosPedidosDoUsuario são os que quem chama carregou.
    public ResultadoDaRecuperacao Concluir(
        Usuario usuario,
        string hashDaSenhaNova,
        IEnumerable<FamiliaDeSessao> familiasDoUsuario,
        IEnumerable<RecuperacaoDeSenha> outrosPedidosDoUsuario,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentNullException.ThrowIfNull(familiasDoUsuario);
        ArgumentNullException.ThrowIfNull(outrosPedidosDoUsuario);

        if (usuario.Id != UsuarioId)
        {
            throw new ArgumentException("A recuperação é de outro usuário.", nameof(usuario));
        }

        var familias = familiasDoUsuario.ToList();
        if (familias.Any(familia => familia.UsuarioId != UsuarioId))
        {
            throw new ArgumentException("Há família de sessão de outro usuário.", nameof(familiasDoUsuario));
        }

        var outrosPedidos = outrosPedidosDoUsuario.Where(pedido => pedido.Id != Id).ToList();
        if (outrosPedidos.Any(pedido => pedido.UsuarioId != UsuarioId))
        {
            throw new ArgumentException("Há pedido de recuperação de outro usuário.", nameof(outrosPedidosDoUsuario));
        }

        if (UsadaEm is not null)
        {
            return ResultadoDaRecuperacao.JaUsada;
        }

        if (InvalidadaEm is not null)
        {
            return ResultadoDaRecuperacao.Invalidada;
        }

        if (agora > ExpiraEm)
        {
            return ResultadoDaRecuperacao.Expirada;
        }

        usuario.DefinirSenha(hashDaSenhaNova);
        UsadaEm = agora;
        foreach (var familia in familias)
        {
            familia.Revogar(MotivoDaRevogacao.SenhaRedefinida, agora);
        }

        foreach (var pedido in outrosPedidos)
        {
            pedido.Invalidar(agora);
        }

        return ResultadoDaRecuperacao.Concluida;
    }

    // Só pedido ainda aberto: usado ou já invalidado fica como está.
    private void Invalidar(DateTimeOffset agora)
    {
        if (EstaAberta)
        {
            InvalidadaEm = agora;
        }
    }
}

public enum ResultadoDaRecuperacao
{
    Concluida,
    Expirada,
    JaUsada,

    // Outro pedido do mesmo usuário foi concluído antes deste.
    Invalidada,
}
