using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class LostFoundPost
{
    public int Id { get; set; }

    public LostFoundPostType PostType { get; set; }

    public LostFoundPostStatus Status { get; set; } = LostFoundPostStatus.PendingReview;

    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(80)]
    public string? DogName { get; set; }

    [StringLength(120)]
    public string? BreedText { get; set; }

    public DogSize? Size { get; set; }

    [StringLength(80)]
    public string? CoatColor { get; set; }

    [StringLength(500)]
    public string? DistinctiveMarks { get; set; }

    public DateTime LastSeenOrFoundDate { get; set; }

    [Required, StringLength(80)]
    public string City { get; set; } = string.Empty;

    [StringLength(80)]
    public string? Neighborhood { get; set; }

    [StringLength(250)]
    public string? AddressOrAreaDescription { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [Required, StringLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [Required, StringLength(256)]
    public string ContactEmail { get; set; } = string.Empty;

    [StringLength(40)]
    public string? ContactPhone { get; set; }

    public bool ContactInfoPublic { get; set; }

    public string? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? ApprovedByUserId { get; set; }

    public ApplicationUser? ApprovedByUser { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? ClosedByUserId { get; set; }

    public ApplicationUser? ClosedByUser { get; set; }

    [StringLength(500)]
    public string? RejectionReason { get; set; }

    [StringLength(1000)]
    public string? ResolutionNotes { get; set; }

    public ICollection<LostFoundPostImage> Images { get; set; } = new List<LostFoundPostImage>();
}
