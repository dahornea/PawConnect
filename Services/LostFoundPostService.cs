using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class LostFoundPostService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    INotificationService? notificationService = null) : ILostFoundPostService
{
    public const int MaxImagesPerPost = 5;
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 2000;
    public const int MaxResolutionNotesLength = 1000;
    public const int MaxRejectionReasonLength = 500;

    public async Task<IReadOnlyList<LostFoundPostListItemDto>> GetPublicPostsAsync(
        LostFoundPostFilter filter,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = BuildPostQuery(context)
            .Where(post => post.Status == LostFoundPostStatus.Approved || post.Status == LostFoundPostStatus.Closed);

        if (filter.Status is not null)
        {
            query = query.Where(post => post.Status == filter.Status);
        }
        else
        {
            query = query.Where(post => post.Status == LostFoundPostStatus.Approved);
        }

        var posts = await ApplyFilters(query, filter)
            .OrderByDescending(post => post.LastSeenOrFoundDate)
            .ThenByDescending(post => post.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return posts.Select(ToListItemDto).ToList();
    }

    public async Task<LostFoundPostDetailsDto?> GetPublicDetailsAsync(
        int postId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var post = await BuildPostQuery(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                existing => existing.Id == postId &&
                    (existing.Status == LostFoundPostStatus.Approved || existing.Status == LostFoundPostStatus.Closed),
                cancellationToken);

        return post is null ? null : ToDetailsDto(post, includePrivateContact: false);
    }

    public async Task<LostFoundPostDetailsDto?> GetVisibleDetailsAsync(
        int postId,
        string? currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var post = await BuildPostQuery(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(existing => existing.Id == postId, cancellationToken);

        if (post is null)
        {
            return null;
        }

        var isPublic = post.Status is LostFoundPostStatus.Approved or LostFoundPostStatus.Closed;
        var isCreator = !string.IsNullOrWhiteSpace(currentUserId) &&
            string.Equals(post.CreatedByUserId, currentUserId, StringComparison.Ordinal);

        if (!isPublic && !isCreator && !isAdmin)
        {
            return null;
        }

        return ToDetailsDto(post, includePrivateContact: isCreator || isAdmin);
    }

    public async Task<LostFoundPostDetailsDto> CreatePostAsync(
        LostFoundPostCreateRequest request,
        string createdByUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new InvalidOperationException("You must be signed in to create a lost or found post.");
        }

        var normalized = NormalizeCreateRequest(request);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var creatorExists = await context.Users.AnyAsync(user => user.Id == createdByUserId, cancellationToken);
        if (!creatorExists)
        {
            throw new InvalidOperationException("Current user could not be found.");
        }

        var now = DateTime.UtcNow;
        var post = new LostFoundPost
        {
            PostType = normalized.PostType,
            Status = LostFoundPostStatus.PendingReview,
            Title = normalized.Title,
            Description = normalized.Description,
            DogName = normalized.DogName,
            BreedText = normalized.BreedText,
            Size = normalized.Size,
            CoatColor = normalized.CoatColor,
            DistinctiveMarks = normalized.DistinctiveMarks,
            LastSeenOrFoundDate = normalized.LastSeenOrFoundDate!.Value,
            City = normalized.City,
            Neighborhood = normalized.Neighborhood,
            AddressOrAreaDescription = normalized.AddressOrAreaDescription,
            Latitude = normalized.Latitude,
            Longitude = normalized.Longitude,
            ContactName = normalized.ContactName,
            ContactEmail = normalized.ContactEmail,
            ContactPhone = normalized.ContactPhone,
            ContactInfoPublic = normalized.ContactInfoPublic,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            Images = normalized.Images?
                .Select((image, index) => new LostFoundPostImage
                {
                    ImageUrlOrPath = image.ImageUrlOrPath,
                    IsMain = image.IsMain || index == 0 && !normalized.Images.Any(candidate => candidate.IsMain),
                    CreatedAt = now,
                    UploadedByUserId = createdByUserId
                })
                .ToList() ?? []
        };

        context.LostFoundPosts.Add(post);
        await context.SaveChangesAsync(cancellationToken);
        await NotifyAdminsAboutNewPostAsync(context, post, cancellationToken);

        var savedPost = await LoadPostForDtoAsync(context, post.Id, cancellationToken);
        return ToDetailsDto(savedPost!, includePrivateContact: true);
    }

    public async Task<IReadOnlyList<LostFoundPostListItemDto>> GetAdminPostsAsync(
        LostFoundPostFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);

        var posts = await ApplyFilters(BuildPostQuery(context), filter)
            .OrderBy(post => post.Status == LostFoundPostStatus.PendingReview ? 0 : 1)
            .ThenByDescending(post => post.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return posts.Select(ToListItemDto).ToList();
    }

    public async Task<LostFoundPostDetailsDto> ApprovePostAsync(
        int postId,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);
        var post = await LoadPostForUpdateAsync(context, postId, cancellationToken);

        post.Status = LostFoundPostStatus.Approved;
        post.ApprovedAt = DateTime.UtcNow;
        post.ApprovedByUserId = adminUserId;
        post.RejectionReason = null;
        post.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await NotifyCreatorAsync(post, "Lost & found post approved", "Your lost or found post is now visible publicly.", NotificationType.Success);

        var updated = await LoadPostForDtoAsync(context, postId, cancellationToken);
        return ToDetailsDto(updated!, includePrivateContact: true);
    }

    public async Task<LostFoundPostDetailsDto> RejectPostAsync(
        int postId,
        string reason,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedReason = NormalizeRequired(reason, MaxRejectionReasonLength, "Rejection reason");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);
        var post = await LoadPostForUpdateAsync(context, postId, cancellationToken);

        post.Status = LostFoundPostStatus.Rejected;
        post.RejectionReason = normalizedReason;
        post.ApprovedAt = null;
        post.ApprovedByUserId = null;
        post.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await NotifyCreatorAsync(post, "Lost & found post needs changes", normalizedReason, NotificationType.Warning);

        var updated = await LoadPostForDtoAsync(context, postId, cancellationToken);
        return ToDetailsDto(updated!, includePrivateContact: true);
    }

    public async Task<LostFoundPostDetailsDto> ClosePostAsync(
        int postId,
        string? resolutionNotes,
        string currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var normalizedNotes = NormalizeOptional(resolutionNotes, MaxResolutionNotesLength, "Resolution notes");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (isAdmin)
        {
            await EnsureAdminAsync(context, currentUserId, cancellationToken);
        }

        var post = await LoadPostForUpdateAsync(context, postId, cancellationToken);
        var isCreator = !string.IsNullOrWhiteSpace(currentUserId) &&
            string.Equals(post.CreatedByUserId, currentUserId, StringComparison.Ordinal);
        if (!isAdmin && !isCreator)
        {
            throw new InvalidOperationException("Only the creator or an administrator can close this post.");
        }

        if (post.Status != LostFoundPostStatus.Approved)
        {
            throw new InvalidOperationException("Only approved posts can be closed.");
        }

        post.Status = LostFoundPostStatus.Closed;
        post.ClosedAt = DateTime.UtcNow;
        post.ClosedByUserId = currentUserId;
        post.ResolutionNotes = normalizedNotes;
        post.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await NotifyCreatorAsync(post, "Lost & found post closed", "The post was marked as resolved.", NotificationType.Info);

        var updated = await LoadPostForDtoAsync(context, postId, cancellationToken);
        return ToDetailsDto(updated!, includePrivateContact: true);
    }

    public async Task<LostFoundPostDetailsDto> ReopenPostAsync(
        int postId,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);
        var post = await LoadPostForUpdateAsync(context, postId, cancellationToken);

        post.Status = LostFoundPostStatus.Approved;
        post.ClosedAt = null;
        post.ClosedByUserId = null;
        post.ResolutionNotes = null;
        post.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await NotifyCreatorAsync(post, "Lost & found post reopened", "The post is visible publicly again.", NotificationType.Info);

        var updated = await LoadPostForDtoAsync(context, postId, cancellationToken);
        return ToDetailsDto(updated!, includePrivateContact: true);
    }

    private static IQueryable<LostFoundPost> BuildPostQuery(ApplicationDbContext context)
    {
        return context.LostFoundPosts
            .Include(post => post.CreatedByUser)
            .Include(post => post.Images);
    }

    private static IQueryable<LostFoundPost> ApplyFilters(
        IQueryable<LostFoundPost> query,
        LostFoundPostFilter filter)
    {
        if (filter.PostType is not null)
        {
            query = query.Where(post => post.PostType == filter.PostType);
        }

        if (filter.Status is not null)
        {
            query = query.Where(post => post.Status == filter.Status);
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var city = filter.City.Trim();
            query = query.Where(post => post.City.Contains(city));
        }

        if (!string.IsNullOrWhiteSpace(filter.Neighborhood))
        {
            var neighborhood = filter.Neighborhood.Trim();
            query = query.Where(post => post.Neighborhood != null && post.Neighborhood.Contains(neighborhood));
        }

        if (filter.Size is not null)
        {
            query = query.Where(post => post.Size == filter.Size);
        }

        if (!string.IsNullOrWhiteSpace(filter.CoatColor))
        {
            var coatColor = filter.CoatColor.Trim();
            query = query.Where(post => post.CoatColor != null && post.CoatColor.Contains(coatColor));
        }

        if (filter.FromDate is not null)
        {
            query = query.Where(post => post.LastSeenOrFoundDate >= filter.FromDate.Value);
        }

        if (filter.ToDate is not null)
        {
            var inclusiveTo = filter.ToDate.Value.Date.AddDays(1);
            query = query.Where(post => post.LastSeenOrFoundDate < inclusiveTo);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(post =>
                post.Title.Contains(keyword) ||
                post.Description.Contains(keyword) ||
                (post.DogName != null && post.DogName.Contains(keyword)) ||
                (post.BreedText != null && post.BreedText.Contains(keyword)) ||
                (post.DistinctiveMarks != null && post.DistinctiveMarks.Contains(keyword)));
        }

        return query;
    }

    private static LostFoundPostCreateRequest NormalizeCreateRequest(LostFoundPostCreateRequest request)
    {
        if (!Enum.IsDefined(request.PostType))
        {
            throw new InvalidOperationException("Please choose whether this is a lost or found dog post.");
        }

        var date = request.LastSeenOrFoundDate?.Date;
        if (date is null)
        {
            throw new InvalidOperationException("Last seen or found date is required.");
        }

        if (date.Value > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new InvalidOperationException("Last seen or found date cannot be in the future.");
        }

        ValidateCoordinates(request.Latitude, request.Longitude);
        var images = NormalizeImages(request.Images);

        return request with
        {
            Title = NormalizeRequired(request.Title, MaxTitleLength, "Title"),
            Description = NormalizeRequired(request.Description, MaxDescriptionLength, "Description"),
            DogName = NormalizeOptional(request.DogName, 80, "Dog name"),
            BreedText = NormalizeOptional(request.BreedText, 120, "Breed"),
            CoatColor = NormalizeOptional(request.CoatColor, 80, "Coat color"),
            DistinctiveMarks = NormalizeOptional(request.DistinctiveMarks, 500, "Distinctive marks"),
            LastSeenOrFoundDate = date,
            City = NormalizeRequired(request.City, 80, "City"),
            Neighborhood = NormalizeOptional(request.Neighborhood, 80, "Neighborhood"),
            AddressOrAreaDescription = NormalizeOptional(request.AddressOrAreaDescription, 250, "Area description"),
            ContactName = NormalizeRequired(request.ContactName, 120, "Contact name"),
            ContactEmail = NormalizeEmail(request.ContactEmail),
            ContactPhone = NormalizeOptional(request.ContactPhone, 40, "Contact phone"),
            Images = images
        };
    }

    private static IReadOnlyList<LostFoundPostImageInputDto> NormalizeImages(IReadOnlyList<LostFoundPostImageInputDto>? images)
    {
        if (images is null || images.Count == 0)
        {
            return [];
        }

        if (images.Count > MaxImagesPerPost)
        {
            throw new InvalidOperationException($"A lost or found post can have at most {MaxImagesPerPost} images.");
        }

        var normalizedImages = new List<LostFoundPostImageInputDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in images)
        {
            if (!DogImageUrlValidator.TryNormalizeImageReference(image.ImageUrlOrPath, out var normalizedUrl))
            {
                throw new InvalidOperationException(DogImageUrlValidator.ValidationMessage);
            }

            if (!IsAllowedLostFoundImage(normalizedUrl))
            {
                throw new InvalidOperationException("Lost and found images must be JPG, PNG, or WebP files.");
            }

            if (!seen.Add(normalizedUrl))
            {
                continue;
            }

            normalizedImages.Add(image with { ImageUrlOrPath = normalizedUrl });
        }

        if (normalizedImages.Count == 0)
        {
            return [];
        }

        if (!normalizedImages.Any(image => image.IsMain))
        {
            normalizedImages[0] = normalizedImages[0] with { IsMain = true };
        }
        else if (normalizedImages.Count(image => image.IsMain) > 1)
        {
            var firstMainIndex = normalizedImages.FindIndex(image => image.IsMain);
            for (var index = 0; index < normalizedImages.Count; index++)
            {
                normalizedImages[index] = normalizedImages[index] with { IsMain = index == firstMainIndex };
            }
        }

        return normalizedImages;
    }

    private async Task<LostFoundPost> LoadPostForUpdateAsync(
        ApplicationDbContext context,
        int postId,
        CancellationToken cancellationToken)
    {
        return await context.LostFoundPosts
            .Include(post => post.Images)
            .FirstOrDefaultAsync(post => post.Id == postId, cancellationToken)
            ?? throw new InvalidOperationException("Lost or found post could not be found.");
    }

    private static async Task<LostFoundPost?> LoadPostForDtoAsync(
        ApplicationDbContext context,
        int postId,
        CancellationToken cancellationToken)
    {
        return await BuildPostQuery(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(post => post.Id == postId, cancellationToken);
    }

    private static async Task EnsureAdminAsync(
        ApplicationDbContext context,
        string adminUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new InvalidOperationException("Current user could not be found.");
        }

        var isAdmin = await context.UserRoles
            .Join(
                context.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .AnyAsync(
                roleAssignment =>
                    roleAssignment.UserId == adminUserId &&
                    roleAssignment.Name == IdentitySeedData.AdminRole,
                cancellationToken);

        if (!isAdmin)
        {
            throw new InvalidOperationException("Only administrators can moderate lost and found posts.");
        }
    }

    private async Task NotifyAdminsAboutNewPostAsync(
        ApplicationDbContext context,
        LostFoundPost post,
        CancellationToken cancellationToken)
    {
        if (notificationService is null)
        {
            return;
        }

        var adminUserIds = await context.UserRoles
            .Join(
                context.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .Where(roleAssignment => roleAssignment.Name == IdentitySeedData.AdminRole)
            .Select(roleAssignment => roleAssignment.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var adminUserId in adminUserIds)
        {
            await notificationService.CreateNotificationAsync(
                adminUserId,
                "Lost & found post pending review",
                $"{FormatPostType(post.PostType)} post \"{post.Title}\" is waiting for approval.",
                NotificationCategory.System,
                NotificationType.Info,
                "/admin/lost-found",
                nameof(LostFoundPost),
                post.Id.ToString(),
                TimeSpan.FromMinutes(5));
        }
    }

    private async Task NotifyCreatorAsync(
        LostFoundPost post,
        string title,
        string message,
        NotificationType type)
    {
        if (notificationService is null || string.IsNullOrWhiteSpace(post.CreatedByUserId))
        {
            return;
        }

        await notificationService.CreateNotificationAsync(
            post.CreatedByUserId,
            title,
            message,
            NotificationCategory.System,
            type,
            $"/lost-found/{post.Id}",
            nameof(LostFoundPost),
            post.Id.ToString(),
            TimeSpan.FromMinutes(5));
    }

    private static LostFoundPostListItemDto ToListItemDto(LostFoundPost post)
    {
        return new LostFoundPostListItemDto(
            post.Id,
            post.PostType,
            post.Status,
            post.Title,
            post.DogName,
            post.BreedText,
            post.Size,
            post.CoatColor,
            post.City,
            post.Neighborhood,
            post.AddressOrAreaDescription,
            post.LastSeenOrFoundDate,
            post.CreatedAt,
            GetMainImageUrl(post.Images));
    }

    private static LostFoundPostDetailsDto ToDetailsDto(LostFoundPost post, bool includePrivateContact)
    {
        var exposeContact = includePrivateContact || post.ContactInfoPublic;
        return new LostFoundPostDetailsDto(
            post.Id,
            post.PostType,
            post.Status,
            post.Title,
            post.Description,
            post.DogName,
            post.BreedText,
            post.Size,
            post.CoatColor,
            post.DistinctiveMarks,
            post.LastSeenOrFoundDate,
            post.City,
            post.Neighborhood,
            post.AddressOrAreaDescription,
            post.Latitude,
            post.Longitude,
            post.ContactName,
            exposeContact ? post.ContactEmail : null,
            exposeContact ? post.ContactPhone : null,
            post.ContactInfoPublic,
            post.CreatedByUserId,
            DisplayName(post.CreatedByUser?.FullName, post.CreatedByUser?.Email),
            post.CreatedAt,
            post.ApprovedAt,
            post.ClosedAt,
            post.RejectionReason,
            post.ResolutionNotes,
            GetValidImages(post.Images)
                .Select(image => new LostFoundPostImageDto(image.Id, image.ImageUrlOrPath, image.IsMain, image.CreatedAt))
                .ToList());
    }

    private static string? GetMainImageUrl(IEnumerable<LostFoundPostImage> images)
    {
        return GetValidImages(images)
            .Select(image => image.ImageUrlOrPath)
            .FirstOrDefault();
    }

    private static List<LostFoundPostImage> GetValidImages(IEnumerable<LostFoundPostImage> images)
    {
        return images
            .Where(image => DogImageUrlValidator.IsValidRealDogImageUrl(image.ImageUrlOrPath))
            .OrderByDescending(image => image.IsMain)
            .ThenBy(image => image.Id)
            .ToList();
    }

    private static string NormalizeRequired(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} must be {maxLength} characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} must be {maxLength} characters or fewer.");
        }

        return normalized;
    }

    private static string NormalizeEmail(string? value)
    {
        var email = NormalizeRequired(value, 256, "Contact email");
        if (!new EmailAddressAttribute().IsValid(email))
        {
            throw new InvalidOperationException("Please enter a valid contact email address.");
        }

        return email;
    }

    private static void ValidateCoordinates(double? latitude, double? longitude)
    {
        if (latitude is null && longitude is null)
        {
            return;
        }

        if (latitude is null || longitude is null)
        {
            throw new InvalidOperationException("Both latitude and longitude are required when adding map coordinates.");
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new InvalidOperationException("Map coordinates are outside the valid range.");
        }
    }

    private static bool IsAllowedLostFoundImage(string imageUrl)
    {
        var path = imageUrl.Split('?', '#')[0];
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static string? DisplayName(string? fullName, string? email)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName.Trim();
        }

        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    private static string FormatPostType(LostFoundPostType type)
    {
        return type == LostFoundPostType.Lost ? "Lost dog" : "Found dog";
    }
}
