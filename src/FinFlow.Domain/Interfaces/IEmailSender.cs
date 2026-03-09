namespace FinFlow.Domain.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(string to, string subject, string htmlBody);
}
