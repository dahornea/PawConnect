using System.Globalization;
using Microsoft.AspNetCore.Hosting;

namespace PawConnect.Services;

public class LocalMessageAttachmentStorageService(IWebHostEnvironment environment) : IMessageAttachmentStorageService
{
    public const long DefaultMaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string[]> AllowedContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".webp"] = ["image/webp"],
        [".pdf"] = ["application/pdf"]
    };

    public long MaxFileSizeBytes => DefaultMaxFileSizeBytes;

    public IReadOnlySet<string> AllowedExtensions { get; } = AllowedContentTypesByExtension.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<MessageAttachmentStorageResult> SaveAsync(
        int conversationId,
        string originalFileName,
        string contentType,
        Stream content,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        var safeOriginalFileName = ValidateFileName(originalFileName);
        var normalizedContentType = ValidateContentType(contentType, safeOriginalFileName);

        if (fileSizeBytes <= 0)
        {
            throw new InvalidOperationException("Attachment cannot be empty.");
        }

        if (fileSizeBytes > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("Attachment must be 5 MB or smaller.");
        }

        var extension = Path.GetExtension(safeOriginalFileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var relativeDirectory = Path.Combine("uploads", "messages", conversationId.ToString(CultureInfo.InvariantCulture));
        var physicalDirectory = Path.Combine(GetWebRootPath(), relativeDirectory);
        Directory.CreateDirectory(physicalDirectory);

        var physicalPath = Path.Combine(physicalDirectory, storedFileName);
        await using (var output = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(output, cancellationToken);
        }

        return new MessageAttachmentStorageResult(
            safeOriginalFileName,
            storedFileName,
            ToSafeRelativePath(Path.Combine(relativeDirectory, storedFileName)),
            normalizedContentType,
            fileSizeBytes);
    }

    public Task<Stream> OpenReadAsync(string filePathOrKey, CancellationToken cancellationToken = default)
    {
        var normalizedRelativePath = NormalizeStoredRelativePath(filePathOrKey);
        var physicalPath = Path.GetFullPath(Path.Combine(GetWebRootPath(), normalizedRelativePath));
        var uploadRoot = Path.GetFullPath(Path.Combine(GetWebRootPath(), "uploads", "messages"));

        if (!physicalPath.StartsWith(uploadRoot, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(physicalPath))
        {
            throw new FileNotFoundException("Attachment file could not be found.");
        }

        Stream stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    private static string ValidateFileName(string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new InvalidOperationException("Attachment file name is required.");
        }

        var trimmed = originalFileName.Trim();
        var safeFileName = Path.GetFileName(trimmed);
        if (!string.Equals(trimmed, safeFileName, StringComparison.Ordinal) ||
            trimmed.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Attachment file name is not valid.");
        }

        var extension = Path.GetExtension(safeFileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedContentTypesByExtension.ContainsKey(extension))
        {
            throw new InvalidOperationException("Unsupported attachment type. Allowed files are JPG, PNG, WEBP, and PDF.");
        }

        return safeFileName;
    }

    private static string ValidateContentType(string contentType, string fileName)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new InvalidOperationException("Attachment content type is required.");
        }

        var normalized = contentType.Trim().ToLowerInvariant();
        var extension = Path.GetExtension(fileName);
        if (!AllowedContentTypesByExtension.TryGetValue(extension, out var allowedContentTypes) ||
            !allowedContentTypes.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Attachment content type does not match the selected file.");
        }

        return normalized;
    }

    private static string NormalizeStoredRelativePath(string filePathOrKey)
    {
        if (string.IsNullOrWhiteSpace(filePathOrKey))
        {
            throw new FileNotFoundException("Attachment file could not be found.");
        }

        var relativePath = filePathOrKey.Trim().Replace('\\', '/').TrimStart('/');
        if (relativePath.Contains("..", StringComparison.Ordinal) ||
            !relativePath.StartsWith("uploads/messages/", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("Attachment file could not be found.");
        }

        return relativePath;
    }

    private string GetWebRootPath()
    {
        return string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;
    }

    private static string ToSafeRelativePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}
