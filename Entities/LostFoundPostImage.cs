using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class LostFoundPostImage
{
    public int Id { get; set; }

    public int LostFoundPostId { get; set; }

    public LostFoundPost? LostFoundPost { get; set; }

    [Required, StringLength(500)]
    public string ImageUrlOrPath { get; set; } = string.Empty;

    public bool IsMain { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UploadedByUserId { get; set; }

    public ApplicationUser? UploadedByUser { get; set; }
}
