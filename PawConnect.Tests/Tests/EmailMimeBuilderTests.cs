using MimeKit;
using PawConnect.Services;

namespace PawConnect.Tests.Tests;

public class EmailMimeBuilderTests
{
    [Fact]
    public void BuildBody_AddsCalendarInvitePartAndAttachmentFallback()
    {
        var calendar = new EmailAttachment
        {
            FileName = "adoption-visit-bella-2026-05-20.ics",
            ContentType = "text/calendar",
            IsCalendarInvite = true,
            CalendarMethod = "REQUEST",
            IncludeAsAttachmentFallback = true,
            Content = System.Text.Encoding.UTF8.GetBytes("""
                BEGIN:VCALENDAR
                VERSION:2.0
                METHOD:REQUEST
                BEGIN:VEVENT
                UID:pawconnect-adoption-visit-7@pawconnect.local
                DTSTART:20260520T090000Z
                DTEND:20260520T100000Z
                SUMMARY:Visit Bella at Happy Paws
                LOCATION:Strada Exemplu 10, Cluj-Napoca
                END:VEVENT
                END:VCALENDAR
                """)
        };

        var body = EmailMimeBuilder.BuildBody(
            "Plain body",
            "<p>HTML body</p>",
            [calendar]);

        var parts = Flatten(body).ToList();
        var calendarParts = parts
            .OfType<TextPart>()
            .Where(part => part.ContentType.MimeType.Equals("text/calendar", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(2, calendarParts.Count);
        Assert.Contains(calendarParts, part =>
            part.ContentDisposition?.Disposition == ContentDisposition.Inline &&
            part.ContentType.Parameters["method"] == "REQUEST" &&
            part.Text.Contains("METHOD:REQUEST", StringComparison.Ordinal));
        Assert.Contains(calendarParts, part =>
            part.ContentDisposition?.Disposition == ContentDisposition.Attachment &&
            part.ContentDisposition.FileName == "adoption-visit-bella-2026-05-20.ics");
    }

    [Fact]
    public void BuildBody_PreservesPdfAttachmentBehavior()
    {
        var pdf = new EmailAttachment
        {
            FileName = "report.pdf",
            ContentType = "application/pdf",
            Content = "%PDF-test"u8.ToArray()
        };

        var body = EmailMimeBuilder.BuildBody("Plain body", "<p>HTML body</p>", [pdf]);

        var attachment = Assert.Single(Flatten(body).OfType<MimePart>(), part =>
            part.ContentDisposition?.Disposition == ContentDisposition.Attachment);
        Assert.Equal("application/pdf", attachment.ContentType.MimeType);
        Assert.Equal("report.pdf", attachment.FileName);
        Assert.DoesNotContain(Flatten(body).OfType<TextPart>(), part =>
            part.ContentType.MimeType.Equals("text/calendar", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<MimeEntity> Flatten(MimeEntity entity)
    {
        yield return entity;

        if (entity is not Multipart multipart)
        {
            yield break;
        }

        foreach (var part in multipart)
        {
            foreach (var child in Flatten(part))
            {
                yield return child;
            }
        }
    }
}
