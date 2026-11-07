namespace Maxx.Application;

// Um e-mail pronto para sair. O texto pode levar segredo (o link de recuperação): nunca
// vai para log, nem pelo ToString.
public sealed record MensagemDeEmail(string Para, string Assunto, string Texto)
{
    public override string ToString() => $"MensagemDeEmail {{ Assunto = {Assunto} }}";
}

// Entrega um e-mail ao servidor SMTP. Chamado só por handler de trabalho, no worker: quem
// pede o e-mail enfileira um trabalho na fila do Postgres e responde (decisoes/README.md,
// Envio de e-mail). Falha lança, e o trabalho volta com recuo.
public interface IEmailSender
{
    Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken);
}
