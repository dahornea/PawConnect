namespace PawConnect.Services;

public class EmailAttachment
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/pdf";

    public byte[] Content { get; set; } = [];

    public bool IsCalendarInvite { get; set; }

    public string CalendarMethod { get; set; } = "REQUEST";

    public bool IncludeAsAttachmentFallback { get; set; } = true;
}
