using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class SavedDogSearch
{
    public int Id { get; set; }

    [Required]
    public string AdopterUserId { get; set; } = string.Empty;

    public ApplicationUser? AdopterUser { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? SearchText { get; set; }

    public int? ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    [StringLength(120)]
    public string? Breed { get; set; }

    [StringLength(80)]
    public string? CoatColor { get; set; }

    public int? MaxAgeYears { get; set; }

    public DogSize? Size { get; set; }

    [StringLength(120)]
    public string? Location { get; set; }

    [StringLength(120)]
    public string? Neighborhood { get; set; }

    public DogStatus? Status { get; set; }

    public CatCompatibility? CatCompatibility { get; set; }

    public ChildrenCompatibility? ChildrenCompatibility { get; set; }

    public DogActivityLevel? ActivityLevel { get; set; }

    public ApartmentSuitability? ApartmentSuitability { get; set; }

    public DogSortOption SortOption { get; set; } = DogSortOption.NameAsc;

    [StringLength(250)]
    public string? NearbyLabel { get; set; }

    public double? NearbyLatitude { get; set; }

    public double? NearbyLongitude { get; set; }

    public int? RadiusKm { get; set; }

    [StringLength(2000)]
    public string? CriteriaJson { get; set; }

    public bool AlertsEnabled { get; set; } = true;

    public SavedSearchAlertFrequency AlertFrequency { get; set; } = SavedSearchAlertFrequency.Immediate;

    public DateTime? LastEvaluatedAtUtc { get; set; }

    public DateTime? LastMatchAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<SavedSearchMatch> Matches { get; set; } = new List<SavedSearchMatch>();
}
