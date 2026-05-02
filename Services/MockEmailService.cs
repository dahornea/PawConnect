using System.Diagnostics;

namespace PawConnect.Services;

public class MockEmailService(ILogger<MockEmailService> logger) : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string body)
    {
        var message = $"Mock email to {to}: {subject} - {body}";
        logger.LogInformation("{Message}", message);
        Debug.WriteLine(message);
        return Task.CompletedTask;
    }
}
