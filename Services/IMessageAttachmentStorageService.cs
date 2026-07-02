namespace PawConnect.Services;

public interface IMessageAttachmentStorageService
{
    long MaxFileSizeBytes { get; }

    IReadOnlySet<string> AllowedExtensions { get; }

    Task<MessageAttachmentStorageResult> SaveAsync(
        int conversationId,
        string originalFileName,
        string contentType,
        Stream content,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string filePathOrKey,
        CancellationToken cancellationToken = default);
}

public sealed record MessageAttachmentStorageResult(
    string OriginalFileName,
    string StoredFileName,
    string FilePathOrKey,
    string ContentType,
    long FileSizeBytes);
