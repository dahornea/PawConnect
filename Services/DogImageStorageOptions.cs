namespace PawConnect.Services;

public class DogImageStorageOptions
{
    public string Provider { get; set; } = "Local";

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp"];

    public string LocalRoot { get; set; } = "uploads/dogs";
}
