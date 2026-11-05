using System.Threading.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using Maxx.Application;
using Microsoft.Extensions.Hosting;
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
internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
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
    }
}

// A fila em memória, por réplica. Cheia, recusa a mensagem nova em vez de segurar o pedido:
// o usuário pede o link de novo.
internal sealed partial class FilaDeEmailEmMemoria(ILogger<FilaDeEmailEmMemoria> logger) : IFilaDeEmail
{
    public const int Capacidade = 1000;

    private readonly Channel<MensagemDeEmail> _canal = Channel.CreateBounded<MensagemDeEmail>(
        new BoundedChannelOptions(Capacidade) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public ChannelReader<MensagemDeEmail> Leitor => _canal.Reader;

    public void Enfileirar(MensagemDeEmail mensagem)
    {
        if (!_canal.Writer.TryWrite(mensagem))
        {
            LogFilaCheia(logger, mensagem.Assunto);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Fila de e-mail cheia: \"{Assunto}\" descartado.")]
    private static partial void LogFilaCheia(ILogger logger, string assunto);
}

// Esvazia a fila fora da requisição. Falha de envio fica no log, sem destinatário nem
// texto, e não volta para a fila: sem reenvio até a fila do passo 6.
internal sealed partial class EnvioDeEmailService(
    FilaDeEmailEmMemoria fila, IEmailSender sender, ILogger<EnvioDeEmailService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var mensagem in fila.Leitor.ReadAllAsync(stoppingToken))
        {
            try
            {
                await sender.EnviarAsync(mensagem, stoppingToken);
                LogEnviado(logger, mensagem.Assunto);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Uma mensagem que falha não pode parar o envio das outras.
            catch (Exception falha)
#pragma warning restore CA1031
            {
                LogFalha(logger, mensagem.Assunto, falha.GetType().Name, falha.Message);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "E-mail \"{Assunto}\" enviado.")]
    private static partial void LogEnviado(ILogger logger, string assunto);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao enviar o e-mail \"{Assunto}\": {Tipo}: {Erro}")]
    private static partial void LogFalha(ILogger logger, string assunto, string tipo, string erro);
}
