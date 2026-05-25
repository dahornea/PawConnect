using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class DogBreed
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [StringLength(600)]
    public string? GeneralDescription { get; set; }

    [StringLength(300)]
    public string? TypicalTraits { get; set; }

    [StringLength(500)]
    public string? CareNotes { get; set; }

    [StringLength(600)]
    public string? CommonHealthConsiderations { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Dog> Dogs { get; set; } = new List<Dog>();
}
