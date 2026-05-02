using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class AdopterProfile
{
    public int Id { get; set; }

    [Required]
    public string ApplicationUserId { get; set; } = string.Empty;

    public ApplicationUser? ApplicationUser { get; set; }

    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Url, StringLength(500)]
    public string? ProfileImageUrl { get; set; }

    [StringLength(160)]
    public string? Address { get; set; }

    [Required, StringLength(80)]
    public string City { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? PhoneNumber { get; set; }

    public HousingType HousingType { get; set; } = HousingType.Apartment;

    public bool HasYard { get; set; }

    public bool HasOtherPets { get; set; }

    public bool HasChildren { get; set; }

    [StringLength(1000)]
    public string? ExperienceWithDogs { get; set; }

    [StringLength(1000)]
    public string? AdditionalNotes { get; set; }
}
