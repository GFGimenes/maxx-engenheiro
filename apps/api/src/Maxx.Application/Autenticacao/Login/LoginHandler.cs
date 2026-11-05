namespace Maxx.Application.Autenticacao.Login;

// O login (fluxos/01-login.md): aluno por RA ou por e-mail, professor e instituição só
// por e-mail. Toda falha tem a mesma resposta e o mesmo custo de um Argon2id, para que
// nem o corpo nem o tempo digam se a conta existe (ADR 0006).
public sealed class LoginHandler(IUsuarioRepository usuarios, ISenhaHasher senhas, SessaoService sessoes, IUnitOfWork unitOfWork)
{
    public async Task<Sessao> EntrarAsync(Credenciais credenciais, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credenciais);

        // Com arroba é e-mail; sem, é RA. E-mail não tem forma de RA, nem RA de e-mail.
        var identificador = credenciais.Identificador.Trim();
        var usuario = identificador.Contains('@', StringComparison.Ordinal)
            ? await usuarios.BuscarPorEmailAsync(identificador, cancellationToken)
            : await usuarios.BuscarAlunoPorRaAsync(identificador, cancellationToken);

        // Conta do seed que ainda não definiu senha entra pela recuperação, não por aqui.
        if (usuario?.HashDaSenha is not { } hash)
        {
            senhas.VerifyDummy(credenciais.Senha);
            throw CredenciaisInvalidas("conta inexistente ou sem senha");
        }

        if (!senhas.Verify(credenciais.Senha, hash))
        {
            throw CredenciaisInvalidas($"senha errada para o usuário {usuario.Id}");
        }

        var sessao = sessoes.Abrir(usuario);
        await unitOfWork.SalvarAsync(cancellationToken);
        return sessao;
    }

    // O motivo vai só para o log; o identificador digitado não vai nem para lá.
    private static RecusaException CredenciaisInvalidas(string motivo) =>
        new(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.CredenciaisInvalidas, $"Login recusado: {motivo}.");
}

// A senha em claro existe só neste pedido, a caminho do Verify.
public sealed record Credenciais(string Identificador, string Senha)
{
    // Nunca imprimir a senha, nem por acidente num log de objeto.
    public override string ToString() => "Credenciais { }";
}
