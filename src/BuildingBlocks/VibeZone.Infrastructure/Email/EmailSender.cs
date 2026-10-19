using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace VibeZone.Infrastructure.Email;

public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody, string Template);

/// <summary>
/// Queues an email for asynchronous delivery so request latency (and therefore response timing)
/// does not depend on SMTP or on whether an email is sent at all.
/// In-memory: messages are lost if the process stops before sending. A durable outbox replaces it in M4.
/// </summary>
public interface IEmailSender
{
    ValueTask EnqueueAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed class EmailOptions
{
    public const string Section = "Email";

    [Required]
    public string From { get; set; } = string.Empty;

    [Required]
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    /// <summary>None (local Mailpit only), StartTls, or SslOnConnect.</summary>
    public SecureSocketOptions SmtpSecurity { get; set; } = SecureSocketOptions.StartTls;

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }
}

internal sealed class ChannelEmailBuffer : IEmailSender
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        // DropWrite: under extreme load we prefer losing an email over blocking requests; the user can retry.
        _channel.Writer.TryWrite(message);
        return ValueTask.CompletedTask;
    }
}

internal sealed partial class SmtpEmailDispatcher(
    ChannelEmailBuffer queue,
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await SendWithRetriesAsync(message, stoppingToken);
        }
    }

    private async Task SendWithRetriesAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await SendAsync(message, cancellationToken);
                // Never log recipients, subjects with personal data, or bodies (they contain tokens).
                LogSent(logger, message.Template, attempt + 1);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < RetryDelays.Length)
            {
                LogRetry(logger, ex, message.Template, attempt + 1);
                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex, message.Template);
                return;
            }
        }
    }

    private async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(settings.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 15_000 };
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, settings.SmtpSecurity, cancellationToken);
        if (!string.IsNullOrEmpty(settings.SmtpUsername))
        {
            await client.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email '{Template}' sent (attempt {Attempt})")]
    private static partial void LogSent(ILogger logger, string template, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email '{Template}' failed on attempt {Attempt}; retrying")]
    private static partial void LogRetry(ILogger logger, Exception ex, string template, int attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Email '{Template}' could not be delivered")]
    private static partial void LogFailed(ILogger logger, Exception ex, string template);
}

public static class EmailExtensions
{
    public static IServiceCollection AddVibeZoneEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<ChannelEmailBuffer>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<ChannelEmailBuffer>());
        services.AddHostedService<SmtpEmailDispatcher>();
        return services;
    }
}
