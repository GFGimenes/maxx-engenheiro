namespace Maxx.Application;

// Um e-mail pronto para sair. O texto pode levar segredo (o link de recuperação): nunca
// vai para log, nem pelo ToString.
public sealed record MensagemDeEmail(string Para, string Assunto, string Texto)
{
    public override string ToString() => $"MensagemDeEmail {{ Assunto = {Assunto} }}";
}

// Entrega um e-mail ao servidor SMTP. Chamado só pelo envio em segundo plano.
public interface IEmailSender
{
    Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken);
}

// Envio fora da requisição: quem pede só enfileira e responde. Fila em memória até a fila
// do Postgres do passo 6; perder um e-mail num restart é aceito (decisoes/README.md).
public interface IFilaDeEmail
{
    void Enfileirar(MensagemDeEmail mensagem);
}
