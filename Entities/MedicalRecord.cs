using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class MedicalRecord
{
    public int Id { get; set; }

    [Required]
    public DateTime RecordDate { get; set; } = DateTime.UtcNow;

    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(120)]
    public string? VeterinarianName { get; set; }

    public int DogId { get; set; }

    public Dog? Dog { get; set; }
}
