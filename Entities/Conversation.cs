using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class Conversation
{
    public int Id { get; set; }

    public int AdoptionRequestId { get; set; }

    public AdoptionRequest? AdoptionRequest { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastMessageAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
