using System.Net;
using System.Net.Mail;
using FinFlow.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinFlow.Infrastructure.Services;

/// <summary>
/// SMTP経由でメールを送信するIEmailSender実装。
/// 開発環境ではMailHogに接続する。
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _settings = configuration.GetSection("Smtp").Get<SmtpSettings>()
            ?? SmtpSettings.Default;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        // 送信先・件名・本文の基本バリデーション
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("Recipient email address is required.", nameof(to));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Email subject is required.", nameof(subject));

        try
        {
            using var client = CreateSmtpClient();
            using var message = CreateMailMessage(to, subject, htmlBody);

            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent to {To} with subject '{Subject}'", to, subject);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Message}", to, ex.Message);
            throw;
        }
    }

    private SmtpClient CreateSmtpClient()
    {
        var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrEmpty(_settings.Username))
        {
            client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
        }

        return client;
    }

    private MailMessage CreateMailMessage(string to, string subject, string htmlBody)
    {
        var message = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress, _settings.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);
        return message;
    }
}

/// <summary>
/// SMTP接続設定（appsettings.jsonの"Smtp"セクションにマッピング）
/// </summary>
public class SmtpSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool EnableSsl { get; set; } = false;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@finflow.local";
    public string FromName { get; set; } = "FinFlow";

    /// <summary>
    /// 開発用デフォルト設定（MailHog）
    /// </summary>
    public static SmtpSettings Default => new()
    {
        Host = "localhost",
        Port = 1025,
        EnableSsl = false,
        FromAddress = "noreply@finflow.local",
        FromName = "FinFlow"
    };
}
