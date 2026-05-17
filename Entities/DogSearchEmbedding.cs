using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class DogSearchEmbedding
{
    public int Id { get; set; }

    public int DogId { get; set; }

    public Dog? Dog { get; set; }

    [Required, StringLength(4000)]
    public string Content { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string ContentHash { get; set; } = string.Empty;

    [Required]
    public string EmbeddingJson { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string EmbeddingModel { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
