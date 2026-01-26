// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MailKit.Security;
using MimeKit;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.EventLog;

/// <summary>
/// Represents an email message to be sent.
/// </summary>
public class EmailMessage
{
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
}

/// <summary>
/// Interface for email providers.
/// </summary>
public interface IEmailProvider
{
    Task SendAsync(EmailMessage message);
}

/// <summary>
/// Email provider using MailKit SMTP client.
/// </summary>
public class SmtpEmailProvider(SmtpSettings settings) : IEmailProvider
{
    public async Task SendAsync(EmailMessage message)
    {
        var messageToSend = new MimeMessage(
            from: [InternetAddress.Parse(message.From)],
            to: InternetAddressList.Parse(message.To),
            subject: message.Subject,
            body: new TextPart(MimeKit.Text.TextFormat.Plain)
            {
                Text = message.Body
            }
        );

        using var smtp = new MailKit.Net.Smtp.SmtpClient();
        smtp.MessageSent += (sender, args) => { /* args.Response */ };
        smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;
        await smtp.ConnectAsync(settings.Server, settings.Port, (SecureSocketOptions)settings.SslOptions);
        if (!string.IsNullOrEmpty(settings.AuthUser) || !string.IsNullOrEmpty(settings.AuthPass))
        {
            await smtp.AuthenticateAsync(settings.AuthUser, settings.AuthPass);
        }
        await smtp.SendAsync(messageToSend);
        await smtp.DisconnectAsync(quit: true);
        Console.Out.WriteLine($"[SMTP] Sent notification mail (to: {message.To}, subject: {message.Subject})");
    }
}

/// <summary>
/// Email provider using SendGrid Web API v3.
/// Falls back to SENDGRID_API_KEY environment variable if ApiKey is not configured.
/// </summary>
public class SendGridEmailProvider(SendGridSettings settings) : IEmailProvider
{
    private const string ApiKeyEnvironmentVariable = "SENDGRID_API_KEY";

    public async Task SendAsync(EmailMessage message)
    {
        string apiKey = GetApiKey();
        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(message.From);

        // Support multiple recipients separated by commas
        var msg = new SendGridMessage
        {
            From = from,
            Subject = message.Subject,
            PlainTextContent = message.Body
        };

        // Parse recipients (supports comma-separated list like SMTP does)
        string[] recipients = message.To.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string recipient in recipients)
        {
            msg.AddTo(new EmailAddress(recipient.Trim()));
        }

        var response = await client.SendEmailAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            string responseBody = await response.Body.ReadAsStringAsync();
            throw new Exception($"SendGrid API error: {response.StatusCode} - {responseBody}");
        }

        Console.Out.WriteLine($"[SendGrid] Sent notification mail (to: {message.To}, subject: {message.Subject})");
    }

    private string GetApiKey()
    {
        // First try the configured API key
        if (!string.IsNullOrEmpty(settings.ApiKey))
        {
            return settings.ApiKey;
        }

        // Fall back to environment variable
        string? envApiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        if (!string.IsNullOrEmpty(envApiKey))
        {
            return envApiKey;
        }

        throw new InvalidOperationException(
            $"SendGrid API key not configured. Set the 'apiKey' attribute in SendGridSettings or " +
            $"the '{ApiKeyEnvironmentVariable}' environment variable.");
    }
}

/// <summary>
/// Factory for creating email providers based on configuration.
/// </summary>
public static class EmailProviderFactory
{
    public static IEmailProvider Create(MailNotificationSettings settings)
    {
        return settings.Provider switch
        {
            EmailProviderType.SendGrid => new SendGridEmailProvider(settings.SendGridSettings),
            EmailProviderType.Smtp => new SmtpEmailProvider(settings.SmtpSettings),
            _ => new SmtpEmailProvider(settings.SmtpSettings) // Default fallback
        };
    }

    public static string GetFromAddress(MailNotificationSettings settings)
    {
        return settings.Provider switch
        {
            EmailProviderType.SendGrid => settings.SendGridSettings.From,
            EmailProviderType.Smtp => settings.SmtpSettings.From,
            _ => settings.SmtpSettings.From
        };
    }
}
