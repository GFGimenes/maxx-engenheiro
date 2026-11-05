namespace Maxx.Domain.Identidade;

// Uma sessão, de um login até o logout, o reuso ou a senha redefinida. Cada refresh usado
// é trocado por outro da mesma família; apresentar de novo um já trocado é sinal de roubo
// ou replay e derruba a família inteira (ADR 0006). O domínio só vê o hash do refresh:
// gerar o valor e calcular o hash é da infraestrutura.
public sealed class FamiliaDeSessao
{
    // Renovados a cada rotação, sem teto absoluto na fase 1 (decisoes/README.md).
    public static readonly TimeSpan ValidadeDoRefresh = TimeSpan.FromDays(30);

    private readonly List<RefreshToken> _tokens = [];

    // Para o EF Core materializar do banco.
    private FamiliaDeSessao()
    {
    }

    private FamiliaDeSessao(Guid usuarioId, DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        UsuarioId = usuarioId;
        CriadaEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public DateTimeOffset? RevogadaEm { get; private set; }

    public MotivoDaRevogacao? MotivoDaRevogacao { get; private set; }

    public bool EstaAtiva => RevogadaEm is null;

    public IReadOnlyCollection<RefreshToken> Tokens => _tokens.AsReadOnly();

    public static FamiliaDeSessao Abrir(Guid usuarioId, string hashDoRefresh, DateTimeOffset agora)
    {
        var familia = new FamiliaDeSessao(usuarioId, agora);
        familia.Emitir(hashDoRefresh, agora);
        return familia;
    }

    // Família revogada vence tudo; depois o reuso, que revoga; depois a validade.
    public ResultadoDoRefresh Rotacionar(string hashApresentado, string hashDoNovo, DateTimeOffset agora)
    {
        var apresentado = _tokens.SingleOrDefault(token => token.Hash == hashApresentado)
            ?? throw new ArgumentException("O refresh apresentado não é desta família.", nameof(hashApresentado));

        if (!EstaAtiva)
        {
            return ResultadoDoRefresh.FamiliaRevogada;
        }

        if (apresentado.RotacionadoEm is not null)
        {
            Revogar(Identidade.MotivoDaRevogacao.Reuso, agora);
            return ResultadoDoRefresh.ReusoDetectado;
        }

        if (agora > apresentado.ExpiraEm)
        {
            return ResultadoDoRefresh.Expirado;
        }

        apresentado.MarcarRotacionado(agora);
        Emitir(hashDoNovo, agora);
        return ResultadoDoRefresh.Rotacionado;
    }

    // Idempotente: a primeira revogação é a que fica registrada.
    public void Revogar(MotivoDaRevogacao motivo, DateTimeOffset agora)
    {
        if (!EstaAtiva)
        {
            return;
        }

        RevogadaEm = agora;
        MotivoDaRevogacao = motivo;
    }

    private void Emitir(string hash, DateTimeOffset agora)
    {
        hash = Texto.Obrigatorio(hash, "O hash do refresh");
        if (_tokens.Any(token => token.Hash == hash))
        {
            throw new ArgumentException("O hash do refresh novo já foi emitido nesta família.", nameof(hash));
        }

        _tokens.Add(new RefreshToken(Id, hash, agora, agora + ValidadeDoRefresh));
    }
}

public sealed class RefreshToken
{
    // Para o EF Core materializar do banco.
    private RefreshToken()
    {
        Hash = null!;
    }

    internal RefreshToken(Guid familiaId, string hash, DateTimeOffset emitidoEm, DateTimeOffset expiraEm)
    {
        Id = Guid.CreateVersion7();
        FamiliaId = familiaId;
        Hash = hash;
        EmitidoEm = emitidoEm;
        ExpiraEm = expiraEm;
    }

    public Guid Id { get; private set; }

    public Guid FamiliaId { get; private set; }

    // SHA-256 do valor entregue ao cliente. O valor em si nunca é guardado.
    public string Hash { get; private set; }

    public DateTimeOffset EmitidoEm { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    public DateTimeOffset? RotacionadoEm { get; private set; }

    internal void MarcarRotacionado(DateTimeOffset agora) => RotacionadoEm = agora;
}

public enum ResultadoDoRefresh
{
    Rotacionado,
    Expirado,
    FamiliaRevogada,

    // A família acabou de ser revogada por este pedido.
    ReusoDetectado,
}

public enum MotivoDaRevogacao
{
    Logout = 1,
    Reuso = 2,
    SenhaRedefinida = 3,
}
