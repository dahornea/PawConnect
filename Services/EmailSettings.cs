namespace PawConnect.Services;

public class EmailSettings
{
    public bool Enabled { get; set; } = true;

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public string SmtpUser { get; set; } = string.Empty;

    public string SmtpPassword { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = "PawConnect";

    public bool EnableSsl { get; set; } = true;

    public bool OpenLocalInboxOnStartup { get; set; }

    public string LocalInboxUrl { get; set; } = string.Empty;
}
