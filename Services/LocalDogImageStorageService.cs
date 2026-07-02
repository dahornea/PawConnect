using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace PawConnect.Services;

public class LocalDogImageStorageService(
    IWebHostEnvironment environment,
    IOptions<DogImageStorageOptions> options) : IDogImageStorageService
{
    private static readonly Dictionary<string, string> ExpectedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private readonly DogImageStorageOptions storageOptions = options.Value;

    public async Task<DogImageUploadResult> SaveDogImageAsync(
        int dogId,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        ValidateDogId(dogId);
        var extension = ValidateFile(file);
        var webRootPath = GetWebRootPath();
        var localRoot = NormalizeLocalRoot(storageOptions.LocalRoot);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var dogDirectory = GetSafePhysicalPath(webRootPath, localRoot, dogId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dogDirectory);

        var physicalPath = Path.Combine(dogDirectory, fileName);
        await using var inputStream = file.OpenReadStream(storageOptions.MaxFileSizeBytes, cancellationToken);
        await using var outputStream = File.Create(physicalPath);
        await inputStream.CopyToAsync(outputStream, cancellationToken);

        var publicPath = $"/{localRoot.Replace('\\', '/')}/{dogId.ToString(CultureInfo.InvariantCulture)}/{fileName}";
        return new DogImageUploadResult(publicPath, fileName, file.Size, file.ContentType);
    }

    public Task DeleteDogImageAsync(
        string imagePathOrKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePathOrKey))
        {
            return Task.CompletedTask;
        }

        var localRoot = NormalizeLocalRoot(storageOptions.LocalRoot);
        var normalizedPath = imagePathOrKey.Trim().Replace('\\', '/');
        var expectedPrefix = $"/{localRoot.Replace('\\', '/')}/";
        if (!normalizedPath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var relativePath = normalizedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var webRootPath = GetWebRootPath();
        var physicalPath = Path.GetFullPath(Path.Combine(webRootPath, relativePath));
        EnsureInsideRoot(physicalPath, webRootPath);

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }

    private string ValidateFile(IBrowserFile file)
    {
        if (file.Size <= 0)
        {
            throw new InvalidOperationException("Image file is empty.");
        }

        if (file.Size > storageOptions.MaxFileSizeBytes)
        {
            throw new InvalidOperationException($"Image file must be {FormatFileSize(storageOptions.MaxFileSizeBytes)} or smaller.");
        }

        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        var allowedExtensions = GetAllowedExtensions();
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Only {string.Join(", ", allowedExtensions)} image files are supported.");
        }

        if (!ExpectedContentTypes.TryGetValue(extension, out var expectedContentType) ||
            !string.Equals(file.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected file type does not match the image extension.");
        }

        return extension;
    }

    private HashSet<string> GetAllowedExtensions()
    {
        return storageOptions.AllowedExtensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension.StartsWith(".", StringComparison.Ordinal) ? extension : $".{extension}")
            .Select(extension => extension.ToLowerInvariant())
            .Where(ExpectedContentTypes.ContainsKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateDogId(int dogId)
    {
        if (dogId <= 0)
        {
            throw new InvalidOperationException("Dog image upload requires a saved dog profile.");
        }
    }

    private string GetWebRootPath()
    {
        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        Directory.CreateDirectory(webRootPath);
        return Path.GetFullPath(webRootPath);
    }

    private static string NormalizeLocalRoot(string? localRoot)
    {
        var root = string.IsNullOrWhiteSpace(localRoot) ? "uploads/dogs" : localRoot.Trim();
        var safeParts = root
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part is not "." and not "..")
            .ToArray();

        return safeParts.Length == 0
            ? "uploads/dogs"
            : string.Join(Path.DirectorySeparatorChar, safeParts);
    }

    private static string GetSafePhysicalPath(string webRootPath, params string[] parts)
    {
        var physicalPath = Path.GetFullPath(Path.Combine([webRootPath, .. parts]));
        EnsureInsideRoot(physicalPath, webRootPath);
        return physicalPath;
    }

    private static void EnsureInsideRoot(string physicalPath, string webRootPath)
    {
        var normalizedWebRoot = Path.GetFullPath(webRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!physicalPath.StartsWith(normalizedWebRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Image storage path is not valid.");
        }
    }

    private static string FormatFileSize(long bytes)
    {
        var megabytes = bytes / 1024d / 1024d;
        return $"{megabytes:0.#} MB";
    }
}
