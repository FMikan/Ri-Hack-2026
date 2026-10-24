using System.Net;
using System.Net.Mail;
using App.Application.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure;

// SMTP when Email:Smtp:Host is set (Email__Smtp__* in .env), otherwise the link goes to the log (local development).
// Sending runs in the background: the request returns before the SMTP server answers, so timing reveals nothing.
public class EmailSender(IConfiguration configuration, ILogger<EmailSender> logger) : IEmailSender
{
    public Task SendPasswordResetAsync(string to, string token, CancellationToken cancellationToken)
    {
        var baseUrl = (configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{baseUrl}/reset-password?token={token}";
        var smtp = configuration.GetSection("Email:Smtp");
        if (string.IsNullOrEmpty(smtp["Host"]))
        {
            logger.LogWarning("Email:Smtp:Host is not set; password reset link for {Email}: {Link}", to, link);
            return Task.CompletedTask;
        }

        // ponytail: fire-and-forget, a failed send is only logged; add a retry queue if mail delivery matters.
        _ = Task.Run(async () =>
        {
            try
            {
                using var client = new SmtpClient(smtp["Host"], int.TryParse(smtp["Port"], out var port) ? port : 587)
                {
                    EnableSsl = smtp["EnableSsl"] != "false", // STARTTLS on 587
                    Credentials = string.IsNullOrEmpty(smtp["Username"]) ? null : new NetworkCredential(smtp["Username"], smtp["Password"]),
                };
                using var message = new MailMessage(smtp["From"] ?? smtp["Username"]!, to)
                {
                    Subject = "Reset your password",
                    Body = $"Someone asked to reset the password for this e-mail. Open this link within an hour to choose a new one:\n\n{link}\n\nIf it was not you, ignore this e-mail.",
                };
                await client.SendMailAsync(message, CancellationToken.None);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Sending the password reset e-mail to {Email} failed", to);
            }
        }, CancellationToken.None);
        return Task.CompletedTask;
    }
}
