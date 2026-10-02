using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using MimeKit;
using TodoApp.Data;
using TodoApp.Resources;

namespace TodoApp.Components.Account;

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FromAddress { get; set; }
    public string FromName { get; set; } = "TodoApp";

    public bool IsConfigured =>
        !string.IsNullOrEmpty(Host) && !string.IsNullOrEmpty(UserName) && !string.IsNullOrEmpty(Password);
}

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options, IStringLocalizer<SharedResource> localizer) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, localizer["EmailConfirmSubject"], localizer["EmailConfirmBody", confirmationLink]);

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, localizer["EmailResetSubject"], localizer["EmailResetLinkBody", resetLink]);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, localizer["EmailResetSubject"], localizer["EmailResetCodeBody", resetCode]);

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        var smtp = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, string.IsNullOrEmpty(smtp.FromAddress) ? smtp.UserName : smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(smtp.UserName, smtp.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
