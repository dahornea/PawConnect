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

    [Required, StringLength(120)]
    public string Location { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(1000)]
    public string? BehaviorDescription { get; set; }

    [StringLength(500)]
    public string? MedicalStatus { get; set; }

    public DogStatus Status { get; set; } = DogStatus.Available;

    public int? PreferredFoodTypeId { get; set; }

    public FoodType? PreferredFoodType { get; set; }

    [Range(0, 10000)]
    public int? DailyFoodAmountGrams { get; set; }

    public int ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    public ICollection<DogImage> Images { get; set; } = new List<DogImage>();

    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();

    public ICollection<AdoptionRequest> AdoptionRequests { get; set; } = new List<AdoptionRequest>();

    public ICollection<FavoriteDog> FavoriteDogs { get; set; } = new List<FavoriteDog>();

    public ICollection<RecentlyViewedDog> RecentlyViewedDogs { get; set; } = new List<RecentlyViewedDog>();

    public ICollection<DogStatusHistory> StatusHistories { get; set; } = new List<DogStatusHistory>();
}
