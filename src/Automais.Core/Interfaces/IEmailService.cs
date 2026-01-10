namespace Automais.Core.Interfaces;

public interface IEmailService
{
    Task SendWelcomeEmailAsync(string toEmail, string toName, string temporaryPassword, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string temporaryPassword, CancellationToken cancellationToken = default);
}

