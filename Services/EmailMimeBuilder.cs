using System.Text;
using MimeKit;

namespace PawConnect.Services;

public static class EmailMimeBuilder
{
    public static MimeEntity BuildBody(string body, string? htmlBody, IReadOnlyCollection<EmailAttachment>? attachments)
    {
        var validAttachments = attachments?
            .Where(attachment => attachment.Content.Length > 0)
            .ToList() ?? [];
        var calendarInvites = validAttachments
            .Where(attachment => attachment.IsCalendarInvite)
            .ToList();
        var regularAttachments = validAttachments
            .Where(attachment => !attachment.IsCalendarInvite)
            .ToList();

        var messageBody = BuildAlternativeBody(body, htmlBody, calendarInvites);
        var fallbackCalendarAttachments = calendarInvites
            .Where(attachment => attachment.IncludeAsAttachmentFallback)
            .ToList();

        if (regularAttachments.Count == 0 && fallbackCalendarAttachments.Count == 0)
        {
            return messageBody;
        }

        var mixed = new Multipart("mixed") { messageBody };
        foreach (var attachment in regularAttachments)
        {
            mixed.Add(CreateAttachmentPart(attachment));
        }

        foreach (var calendarInvite in fallbackCalendarAttachments)
        {
            mixed.Add(CreateCalendarPart(calendarInvite, asAttachment: true));
        }

        return mixed;
    }

    private static MimeEntity BuildAlternativeBody(string body, string? htmlBody, IReadOnlyList<EmailAttachment> calendarInvites)
    {
        if (string.IsNullOrWhiteSpace(htmlBody) && calendarInvites.Count == 0)
        {
            return new TextPart("plain") { Text = body };
        }

        var alternative = new Multipart("alternative")
        {
            new TextPart("plain") { Text = body }
        };

        if (!string.IsNullOrWhiteSpace(htmlBody))
        {
            alternative.Add(new TextPart("html") { Text = htmlBody });
        }

        foreach (var calendarInvite in calendarInvites)
        {
            alternative.Add(CreateCalendarPart(calendarInvite, asAttachment: false));
        }

        return alternative;
    }

    private static MimeEntity CreateAttachmentPart(EmailAttachment attachment)
    {
        return new MimePart(ContentType.Parse(attachment.ContentType))
        {
            Content = new MimeContent(new MemoryStream(attachment.Content)),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = attachment.FileName
        };
    }

    private static MimeEntity CreateCalendarPart(EmailAttachment attachment, bool asAttachment)
    {
        var calendarPart = new TextPart("calendar")
        {
            Text = Encoding.UTF8.GetString(attachment.Content),
            ContentTransferEncoding = ContentEncoding.QuotedPrintable
        };
        calendarPart.ContentType.Charset = "utf-8";
        calendarPart.ContentType.Parameters["method"] = NormalizeCalendarMethod(attachment.CalendarMethod);

        if (asAttachment && !string.IsNullOrWhiteSpace(attachment.FileName))
        {
            calendarPart.ContentType.Name = attachment.FileName;
        }

        if (asAttachment)
        {
            calendarPart.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment)
            {
                FileName = attachment.FileName
            };
        }
        else
        {
            calendarPart.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
        }

        return calendarPart;
    }

    private static string NormalizeCalendarMethod(string? method)
    {
        return string.IsNullOrWhiteSpace(method) ? "REQUEST" : method.Trim().ToUpperInvariant();
    }
}
