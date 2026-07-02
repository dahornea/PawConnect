using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class MessageReaction
{
    public int Id { get; set; }

    public int MessageId { get; set; }

    public Message? Message { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public MessageReactionType ReactionType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum MessageReactionType
{
    Like = 0,
    Heart = 1,
    Thanks = 2,
    Seen = 3,
    Important = 4
}
