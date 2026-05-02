using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class DogImage
{
    public int Id { get; set; }

    public int DogId { get; set; }

    public Dog? Dog { get; set; }

    [Required, Url, StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    public bool IsMainImage { get; set; }
}
