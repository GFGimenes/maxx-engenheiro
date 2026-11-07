using MailKit.Net.Smtp;
using MailKit.Security;
using Maxx.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Maxx.Infrastructure.Email;

// SMTP genérico (decisoes/README.md, E-mail): Mailpit no compose local, provedor da VPS no
// passo 13. A senha do SMTP só por variável de ambiente.
public sealed class EmailOptions
{
    public const string Section = "Email";

    public string Remetente { get; set; } = string.Empty;

    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Porta { get; set; } = 587;

    // None, Auto, StartTls, SslOnConnect (MailKit). None só para o Mailpit local.
    public SecureSocketOptions Seguranca { get; set; } = SecureSocketOptions.StartTls;

    public string? Usuario { get; set; }

    public string? Senha { get; set; }
}

// Uma conexão por mensagem: o volume da fase 1 é de e-mails de recuperação, não de lote.
// Roda só no worker, dentro de um trabalho da fila: a falha sobe para a retentativa.
internal sealed partial class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Smtp.Host) || string.IsNullOrWhiteSpace(config.Remetente))
        {
            throw new InvalidOperationException("Email__Smtp__Host e Email__Remetente precisam estar configurados.");
        }

        using var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(config.Remetente));
        mime.To.Add(MailboxAddress.Parse(mensagem.Para));
        mime.Subject = mensagem.Assunto;
        mime.Body = new TextPart("plain") { Text = mensagem.Texto };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(config.Smtp.Host, config.Smtp.Porta, config.Smtp.Seguranca, cancellationToken);
        if (!string.IsNullOrEmpty(config.Smtp.Usuario))
        {
            await smtp.AuthenticateAsync(config.Smtp.Usuario, config.Smtp.Senha ?? string.Empty, cancellationToken);
        }

        await smtp.SendAsync(mime, cancellationToken);
        await smtp.DisconnectAsync(quit: true, cancellationToken);
        LogEnviado(logger, mensagem.Assunto);
    }

    // Só o assunto: destinatário e texto (com o link) nunca vão para o log.
    [LoggerMessage(Level = LogLevel.Information, Message = "E-mail \"{Assunto}\" enviado.")]
    private static partial void LogEnviado(ILogger logger, string assunto);
}
