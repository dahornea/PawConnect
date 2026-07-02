using PawConnect.Data;

namespace PawConnect.Entities;

public class MessageReadReceipt
{
    public int Id { get; set; }

    public int MessageId { get; set; }

    public Message? Message { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}
