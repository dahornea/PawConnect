using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class MessageAttachment
{
    public int Id { get; set; }

    public int MessageId { get; set; }

    public Message? Message { get; set; }

    [Required, StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, StringLength(255)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string FilePathOrKey { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public string UploadedByUserId { get; set; } = string.Empty;

    public ApplicationUser? UploadedByUser { get; set; }
}
