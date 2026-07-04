using PawConnect.Entities;

namespace PawConnect.Services;

public interface ILostFoundPostService
{
    Task<IReadOnlyList<LostFoundPostListItemDto>> GetPublicPostsAsync(
        LostFoundPostFilter filter,
        CancellationToken cancellationToken = default);

    Task<LostFoundPostDetailsDto?> GetPublicDetailsAsync(
        int postId,
        CancellationToken cancellationToken = default);

    Task<LostFoundPostDetailsDto?> GetVisibleDetailsAsync(
        int postId,
        string? currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<LostFoundPostDetailsDto> CreatePostAsync(
        LostFoundPostCreateRequest request,
        string createdByUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LostFoundPostListItemDto>> GetAdminPostsAsync(
        LostFoundPostFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<LostFoundPostDetailsDto> ApprovePostAsync(
        int postId,
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<LostFoundPostDetailsDto> RejectPostAsync(
        int postId,
        string reason,
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<LostFoundPostDetailsDto> ClosePostAsync(
        int postId,
        string? resolutionNotes,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<LostFoundPostDetailsDto> ReopenPostAsync(
        int postId,
        string adminUserId,
        CancellationToken cancellationToken = default);
}

public sealed record LostFoundPostFilter(
    LostFoundPostType? PostType = null,
    LostFoundPostStatus? Status = null,
    string? City = null,
    string? Neighborhood = null,
    DogSize? Size = null,
    string? CoatColor = null,
    string? Keyword = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null);

public sealed record LostFoundPostImageInputDto(
    string ImageUrlOrPath,
    bool IsMain = false);

public sealed record LostFoundPostImageDto(
    int Id,
    string ImageUrlOrPath,
    bool IsMain,
    DateTime CreatedAt);

public sealed record LostFoundPostCreateRequest(
    LostFoundPostType PostType,
    string Title,
    string Description,
    string? DogName,
    string? BreedText,
    DogSize? Size,
    string? CoatColor,
    string? DistinctiveMarks,
    DateTime? LastSeenOrFoundDate,
    string City,
    string? Neighborhood,
    string? AddressOrAreaDescription,
    double? Latitude,
    double? Longitude,
    string ContactName,
    string ContactEmail,
    string? ContactPhone,
    bool ContactInfoPublic,
    IReadOnlyList<LostFoundPostImageInputDto>? Images);

public sealed record LostFoundPostListItemDto(
    int Id,
    LostFoundPostType PostType,
    LostFoundPostStatus Status,
    string Title,
    string? DogName,
    string? BreedText,
    DogSize? Size,
    string? CoatColor,
    string City,
    string? Neighborhood,
    string? AddressOrAreaDescription,
    DateTime LastSeenOrFoundDate,
    DateTime CreatedAt,
    string? MainImageUrl);

public sealed record LostFoundPostDetailsDto(
    int Id,
    LostFoundPostType PostType,
    LostFoundPostStatus Status,
    string Title,
    string Description,
    string? DogName,
    string? BreedText,
    DogSize? Size,
    string? CoatColor,
    string? DistinctiveMarks,
    DateTime LastSeenOrFoundDate,
    string City,
    string? Neighborhood,
    string? AddressOrAreaDescription,
    double? Latitude,
    double? Longitude,
    string ContactName,
    string? PublicContactEmail,
    string? PublicContactPhone,
    bool ContactInfoPublic,
    string? CreatedByUserId,
    string? CreatedByDisplayName,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    DateTime? ClosedAt,
    string? RejectionReason,
    string? ResolutionNotes,
    IReadOnlyList<LostFoundPostImageDto> Images);
