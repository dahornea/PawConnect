using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class ShelterRegistrationRequest
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string ShelterName { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string ContactPersonName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Url, StringLength(200)]
    public string? Website { get; set; }

    [StringLength(200)]
    public string? OpeningHours { get; set; }

    [StringLength(1000)]
    public string? ReasonForJoining { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    public ShelterRegistrationRequestStatus Status { get; set; } = ShelterRegistrationRequestStatus.Pending;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedByUserId { get; set; }

    public ApplicationUser? ReviewedByUser { get; set; }

    public string? CreatedUserId { get; set; }

    public int? CreatedShelterId { get; set; }

    public Shelter? CreatedShelter { get; set; }
}
