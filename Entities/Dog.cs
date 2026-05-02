using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class Dog
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string Breed { get; set; } = string.Empty;

    [Range(0, 30)]
    public int Age { get; set; }

    public DogSize Size { get; set; }

    public DogStatus Status { get; set; } = DogStatus.Available;

    [StringLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    public ICollection<DogImage> DogImages { get; set; } = new List<DogImage>();

    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();

    public ICollection<AdoptionRequest> AdoptionRequests { get; set; } = new List<AdoptionRequest>();

    public ICollection<FavoriteDog> FavoriteDogs { get; set; } = new List<FavoriteDog>();
}
