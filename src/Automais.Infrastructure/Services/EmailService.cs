using System.Net;
using System.Net.Mail;
using Automais.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Automais.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService>? _logger;
    private readonly string? _smtpHost;
    private readonly int _smtpPort;
    private readonly string? _smtpUsername;
    private readonly string? _smtpPassword;
    private readonly bool _smtpUseSsl;
    private readonly string? _fromEmail;
    private readonly string? _fromName;

    public EmailService(IConfiguration configuration, ILogger<EmailService>? logger = null)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Office365 SMTP Configuration
        _smtpHost = _configuration["Email:Smtp:Host"] ?? Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.office365.com";
        _smtpPort = int.Parse(_configuration["Email:Smtp:Port"] ?? Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
        _smtpUsername = _configuration["Email:Smtp:Username"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? "noreply@automais.io";
        _smtpPassword = _configuration["Email:Smtp:Password"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? Environment.GetEnvironmentVariable("EMAIL_PASSWORD");
        _smtpUseSsl = bool.Parse(_configuration["Email:Smtp:UseSsl"] ?? Environment.GetEnvironmentVariable("SMTP_USE_SSL") ?? "true");
        _fromEmail = _configuration["Email:From:Address"] ?? Environment.GetEnvironmentVariable("EMAIL_FROM_ADDRESS") ?? "noreply@automais.io";
        _fromName = _configuration["Email:From:Name"] ?? Environment.GetEnvironmentVariable("EMAIL_FROM_NAME") ?? "Automais.IO";

        // Log de configuração (sem expor senha)
        _logger?.LogInformation("EmailService configurado - Host: {Host}, Port: {Port}, Username: {Username}, Password configurada: {HasPassword}", 
            _smtpHost, _smtpPort, _smtpUsername, !string.IsNullOrWhiteSpace(_smtpPassword));
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string toName, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        var subject = "Bem-vindo à Automais.IO";
        var body = GetWelcomeEmailTemplate(toName, temporaryPassword);
        
        await SendEmailAsync(toEmail, toName, subject, body, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        var subject = "Nova senha temporária - Automais.IO";
        var body = GetPasswordResetEmailTemplate(toName, temporaryPassword);
        
        await SendEmailAsync(toEmail, toName, subject, body, cancellationToken);
    }

    private async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_smtpHost) || string.IsNullOrWhiteSpace(_smtpUsername) || string.IsNullOrWhiteSpace(_smtpPassword))
        {
            _logger?.LogWarning("Configuração de email não encontrada. Email não será enviado para {Email}", toEmail);
            // Em desenvolvimento, apenas logar o email que seria enviado
            _logger?.LogInformation("Email que seria enviado:\nPara: {ToEmail}\nAssunto: {Subject}\nCorpo: {Body}", 
                toEmail, subject, htmlBody);
            return;
        }

        try
        {
            _logger?.LogInformation("Tentando enviar email para {Email} via {Host}:{Port} com usuário {Username}", 
                toEmail, _smtpHost, _smtpPort, _smtpUsername);

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                EnableSsl = _smtpUseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Timeout = 30000 // 30 segundos
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_fromEmail!, _fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                Priority = MailPriority.Normal
            };

            message.To.Add(new MailAddress(toEmail, toName));

            await client.SendMailAsync(message, cancellationToken);
            _logger?.LogInformation("Email enviado com sucesso para {Email}", toEmail);
        }
        catch (SmtpException smtpEx)
        {
            _logger?.LogError(smtpEx, "Erro SMTP ao enviar email para {Email}. Status: {Status}, Response: {Response}", 
                toEmail, smtpEx.StatusCode, smtpEx.Message);
            
            // Log adicional para debug
            if (string.IsNullOrWhiteSpace(_smtpPassword))
            {
                _logger?.LogError("A senha SMTP está vazia! Verifique a variável de ambiente EMAIL_PASSWORD ou SMTP_PASSWORD");
            }
            
            throw new InvalidOperationException($"Erro ao enviar email: {smtpEx.Message}. Verifique as credenciais SMTP e se a autenticação SMTP está habilitada na conta Office365.", smtpEx);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao enviar email para {Email}", toEmail);
            throw;
        }
    }

    private static string GetWelcomeEmailTemplate(string name, string temporaryPassword)
    {
        return $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Bem-vindo à Automais.IO</title>
</head>
<body style=""margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f5f5f5;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td style=""padding: 40px 20px; text-align: center;"">
                <table role=""presentation"" style=""max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 30px; text-align: center; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); border-radius: 8px 8px 0 0;"">
                            <h1 style=""margin: 0; color: #ffffff; font-size: 28px; font-weight: 600;"">Bem-vindo à Automais.IO</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 40px 30px;"">
                            <p style=""margin: 0 0 20px 0; color: #333333; font-size: 16px; line-height: 1.6;"">
                                Olá <strong>{name}</strong>,
                            </p>
                            <p style=""margin: 0 0 20px 0; color: #333333; font-size: 16px; line-height: 1.6;"">
                                Sua conta foi criada com sucesso na plataforma Automais.IO!
                            </p>
                            <p style=""margin: 0 0 20px 0; color: #333333; font-size: 16px; line-height: 1.6;"">
                                Sua senha temporária é:
                            </p>
                            <div style=""background-color: #f8f9fa; border: 2px solid #667eea; border-radius: 6px; padding: 20px; margin: 20px 0; text-align: center;"">
                                <p style=""margin: 0; font-size: 24px; font-weight: 600; color: #667eea; font-family: 'Courier New', monospace; letter-spacing: 2px;"">
                                    {temporaryPassword}
                                </p>
                            </div>
                            <p style=""margin: 20px 0; color: #666666; font-size: 14px; line-height: 1.6;"">
                                <strong>Importante:</strong> Por segurança, recomendamos que você altere esta senha no primeiro acesso.
                            </p>
                            <div style=""margin: 30px 0; padding: 20px; background-color: #f0f4ff; border-left: 4px solid #667eea; border-radius: 4px;"">
                                <p style=""margin: 0; color: #333333; font-size: 14px; line-height: 1.6;"">
                                    <strong>Dicas de segurança:</strong>
                                </p>
                                <ul style=""margin: 10px 0 0 0; padding-left: 20px; color: #666666; font-size: 14px; line-height: 1.8;"">
                                    <li>Use uma senha forte com pelo menos 8 caracteres</li>
                                    <li>Combine letras maiúsculas, minúsculas, números e símbolos</li>
                                    <li>Não compartilhe sua senha com ninguém</li>
                                </ul>
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 30px; text-align: center; background-color: #f8f9fa; border-radius: 0 0 8px 8px;"">
                            <p style=""margin: 0; color: #666666; font-size: 12px; line-height: 1.6;"">
                                Se você não solicitou esta conta, por favor, ignore este email.
                            </p>
                            <p style=""margin: 10px 0 0 0; color: #999999; font-size: 12px;"">
                                © {DateTime.Now.Year} Automais.IO - Todos os direitos reservados
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private static string GetPasswordResetEmailTemplate(string name, string temporaryPassword)
    {
        return $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Nova senha temporária - Automais.IO</title>
</head>
<body style=""margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f5f5f5;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td style=""padding: 40px 20px; text-align: center;"">
                <table role=""presentation"" style=""max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 30px; text-align: center; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); border-radius: 8px 8px 0 0;"">
                            <h1 style=""margin: 0; color: #ffffff; font-size: 28px; font-weight: 600;"">Nova Senha Temporária</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 40px 30px;"">
                            <p style=""margin: 0 0 20px 0; color: #333333; font-size: 16px; line-height: 1.6;"">
                                Olá <strong>{name}</strong>,
                            </p>
                            <p style=""margin: 0 0 20px 0; color: #333333; font-size: 16px; line-height: 1.6;"">
                                Sua senha foi resetada com sucesso. Uma nova senha temporária foi gerada para você:
                            </p>
                            <div style=""background-color: #f8f9fa; border: 2px solid #667eea; border-radius: 6px; padding: 20px; margin: 20px 0; text-align: center;"">
                                <p style=""margin: 0; font-size: 24px; font-weight: 600; color: #667eea; font-family: 'Courier New', monospace; letter-spacing: 2px;"">
                                    {temporaryPassword}
                                </p>
                            </div>
                            <p style=""margin: 20px 0; color: #666666; font-size: 14px; line-height: 1.6;"">
                                <strong>Importante:</strong> Por segurança, recomendamos que você altere esta senha no próximo acesso.
                            </p>
                            <div style=""margin: 30px 0; padding: 20px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 4px;"">
                                <p style=""margin: 0; color: #856404; font-size: 14px; line-height: 1.6;"">
                                    <strong>⚠️ Atenção:</strong> Se você não solicitou o reset de senha, entre em contato com o suporte imediatamente.
                                </p>
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 30px; text-align: center; background-color: #f8f9fa; border-radius: 0 0 8px 8px;"">
                            <p style=""margin: 0; color: #666666; font-size: 12px; line-height: 1.6;"">
                                Este é um email automático, por favor não responda.
                            </p>
                            <p style=""margin: 10px 0 0 0; color: #999999; font-size: 12px;"">
                                © {DateTime.Now.Year} Automais.IO - Todos os direitos reservados
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }
}

