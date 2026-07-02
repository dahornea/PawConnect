namespace PawConnect.Services;

public sealed record DogImageUploadResult(
    string ImagePath,
    string FileName,
    long FileSizeBytes,
    string ContentType);
