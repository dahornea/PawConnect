using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class Message
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    public Conversation? Conversation { get; set; }

    [Required]
    public string SenderUserId { get; set; } = string.Empty;

    public ApplicationUser? SenderUser { get; set; }

    [StringLength(2000)]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EditedAt { get; set; }

    public ICollection<MessageReadReceipt> ReadReceipts { get; set; } = new List<MessageReadReceipt>();

    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();

    public ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();

    public ICollection<MessageReport> Reports { get; set; } = new List<MessageReport>();
}
