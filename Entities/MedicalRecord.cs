using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class MedicalRecord
{
    public int Id { get; set; }

    public int DogId { get; set; }

    public Dog? Dog { get; set; }

    [StringLength(120)]
    public string? VaccineName { get; set; }

    [StringLength(1000)]
    public string? TreatmentDescription { get; set; }

    [Required]
    public DateTime RecordDate { get; set; } = DateTime.UtcNow;

    [StringLength(1000)]
    public string? Notes { get; set; }
}
