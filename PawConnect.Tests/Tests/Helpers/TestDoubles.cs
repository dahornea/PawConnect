using PawConnect.Services;

namespace PawConnect.Tests.Tests.Helpers;

public class TestEmailService : IEmailService
{
    public List<(string To, string Subject, string Body, List<EmailAttachment>? Attachments)> SentEmails { get; } = [];

    public Task SendEmailAsync(string to, string subject, string body, List<EmailAttachment>? attachments = null)
    {
        SentEmails.Add((to, subject, body, attachments));
        return Task.CompletedTask;
    }
}

public class TestPdfReportService : IPdfReportService
{
    private static readonly byte[] PdfBytes = "%PDF-test"u8.ToArray();

    public Task<byte[]> GenerateAdoptionRequestReportAsync(int adoptionRequestId)
    {
        return Task.FromResult(PdfBytes);
    }

    public Task<byte[]> GenerateAdoptionStatusReportAsync(int adoptionRequestId)
    {
        return Task.FromResult(PdfBytes);
    }

    public Task<byte[]> GenerateLowStockResourceReportAsync(int resourceStockId)
    {
        return Task.FromResult(PdfBytes);
    }
}
