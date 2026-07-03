using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class CopilotResultFeedback
{
    public int Id { get; set; }

    public int CopilotSessionId { get; set; }

    public CopilotSession? CopilotSession { get; set; }

    public int DogId { get; set; }

    public Dog? Dog { get; set; }

    [Required]
    public string AdopterUserId { get; set; } = string.Empty;

    public ApplicationUser? AdopterUser { get; set; }

    public CopilotFeedbackType FeedbackType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? OptionalComment { get; set; }

    public bool WasOpened { get; set; }

    public bool WasFavorited { get; set; }
}
