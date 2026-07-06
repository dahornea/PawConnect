using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services.Caching;

namespace PawConnect.Services;

public class AnalyticsService(
    ApplicationDbContext context,
    ILocalCacheService? cache = null) : IAnalyticsService
{
    private const int TopListSize = 6;
    private static readonly TimeSpan DashboardCacheTtl = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan LookupCacheTtl = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyList<AnalyticsShelterOptionDto>> GetAdminShelterOptionsAsync(
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserInRoleAsync(adminUserId, IdentitySeedData.AdminRole, cancellationToken);

        if (cache is not null)
        {
            return await cache.GetOrCreateAsync(
                CacheKeys.AdminShelterOptions,
                () => GetAdminShelterOptionsCoreAsync(cancellationToken),
                LookupCacheTtl);
        }

        return await GetAdminShelterOptionsCoreAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AnalyticsShelterOptionDto>> GetAdminShelterOptionsCoreAsync(
        CancellationToken cancellationToken)
    {
        return await context.Shelters
            .AsNoTracking()
            .OrderBy(shelter => shelter.Name)
            .Select(shelter => new AnalyticsShelterOptionDto(shelter.Id, shelter.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminAnalyticsDashboardDto> GetAdminAnalyticsAsync(
        AnalyticsDateRange range,
        int? shelterId,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserInRoleAsync(adminUserId, IdentitySeedData.AdminRole, cancellationToken);
        range.Validate();

        if (shelterId.HasValue && !await context.Shelters.AnyAsync(shelter => shelter.Id == shelterId.Value, cancellationToken))
        {
            throw new InvalidOperationException("The selected shelter does not exist.");
        }

        if (cache is not null)
        {
            return await cache.GetOrCreateAsync(
                CacheKeys.AdminAnalytics(range, shelterId),
                () => GetAdminAnalyticsCoreAsync(range, shelterId, cancellationToken),
                DashboardCacheTtl);
        }

        return await GetAdminAnalyticsCoreAsync(range, shelterId, cancellationToken);
    }

    private async Task<AdminAnalyticsDashboardDto> GetAdminAnalyticsCoreAsync(
        AnalyticsDateRange range,
        int? shelterId,
        CancellationToken cancellationToken)
    {
        var shelterOptions = cache is null
            ? await GetAdminShelterOptionsCoreAsync(cancellationToken)
            : await cache.GetOrCreateAsync(
                CacheKeys.AdminShelterOptions,
                () => GetAdminShelterOptionsCoreAsync(cancellationToken),
                LookupCacheTtl);
        var funnel = await BuildAdoptionFunnelAsync(range, shelterId, cancellationToken);
        var trends = await BuildRequestTrendsAsync(range, shelterId, cancellationToken);
        var dogStatusBreakdown = await BuildDogStatusBreakdownAsync(shelterId, cancellationToken);
        var workload = await BuildShelterWorkloadAsync(range, shelterId, cancellationToken);
        var visibility = await BuildDogVisibilityAsync(range, shelterId, cancellationToken);
        var resourceAnalytics = await BuildResourceAnalyticsAsync(shelterId, cancellationToken);
        var reportActivity = await BuildReportActivityAsync(range, shelterId, cancellationToken);
        var copilotAnalytics = await BuildCopilotAnalyticsAsync(range, cancellationToken);

        return new AdminAnalyticsDashboardDto(
            range,
            shelterId,
            shelterOptions,
            BuildAdminSummaryCards(funnel, dogStatusBreakdown, resourceAnalytics, reportActivity, copilotAnalytics),
            funnel,
            trends,
            dogStatusBreakdown,
            workload,
            visibility.MostViewed,
            visibility.MostFavorited,
            visibility.LowEngagement,
            resourceAnalytics,
            reportActivity,
            copilotAnalytics);
    }

    public async Task<ShelterAnalyticsDashboardDto> GetShelterAnalyticsAsync(
        AnalyticsDateRange range,
        string shelterUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserInRoleAsync(shelterUserId, IdentitySeedData.ShelterRole, cancellationToken);
        range.Validate();

        var shelter = await context.Shelters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ApplicationUserId == shelterUserId, cancellationToken);

        if (shelter is null)
        {
            throw new InvalidOperationException("No shelter profile is linked to this account.");
        }

        if (cache is not null)
        {
            return await cache.GetOrCreateAsync(
                CacheKeys.ShelterAnalytics(shelter.Id, range),
                () => GetShelterAnalyticsCoreAsync(range, shelter, cancellationToken),
                DashboardCacheTtl);
        }

        return await GetShelterAnalyticsCoreAsync(range, shelter, cancellationToken);
    }

    private async Task<ShelterAnalyticsDashboardDto> GetShelterAnalyticsCoreAsync(
        AnalyticsDateRange range,
        Shelter shelter,
        CancellationToken cancellationToken)
    {
        var funnel = await BuildAdoptionFunnelAsync(range, shelter.Id, cancellationToken);
        var trends = await BuildRequestTrendsAsync(range, shelter.Id, cancellationToken);
        var dogStatusBreakdown = await BuildDogStatusBreakdownAsync(shelter.Id, cancellationToken);
        var workload = (await BuildShelterWorkloadAsync(range, shelter.Id, cancellationToken)).SingleOrDefault()
            ?? new ShelterWorkloadDto(shelter.Id, shelter.Name, 0, 0, 0, 0, 0, null, null);
        var visibility = await BuildDogVisibilityAsync(range, shelter.Id, cancellationToken);
        var resourceAnalytics = await BuildResourceAnalyticsAsync(shelter.Id, cancellationToken);
        var reportActivity = await BuildReportActivityAsync(range, shelter.Id, cancellationToken);

        return new ShelterAnalyticsDashboardDto(
            range,
            shelter.Name,
            BuildShelterSummaryCards(funnel, dogStatusBreakdown, resourceAnalytics, reportActivity),
            funnel,
            trends,
            dogStatusBreakdown,
            workload,
            visibility.MostViewed,
            visibility.MostFavorited,
            visibility.LowEngagement,
            resourceAnalytics,
            reportActivity);
    }

    private async Task EnsureUserInRoleAsync(string userId, string roleName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("A signed-in user is required for analytics.");
        }

        var hasRole = await context.UserRoles
            .AsNoTracking()
            .Join(
                context.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, RoleName = role.Name })
            .AnyAsync(role => role.UserId == userId && role.RoleName == roleName, cancellationToken);

        if (!hasRole)
        {
            var message = roleName == IdentitySeedData.AdminRole
                ? "Only administrators can access platform analytics."
                : "Only shelter accounts can access shelter analytics.";
            throw new InvalidOperationException(message);
        }
    }

    private IQueryable<AdoptionRequest> AdoptionRequestsForScope(int? shelterId)
    {
        var query = context.AdoptionRequests
            .AsNoTracking()
            .Include(request => request.Dog)
            .ThenInclude(dog => dog!.Shelter)
            .AsQueryable();

        if (shelterId.HasValue)
        {
            query = query.Where(request => request.Dog != null && request.Dog.ShelterId == shelterId.Value);
        }

        return query;
    }

    private IQueryable<Dog> DogsForScope(int? shelterId)
    {
        var query = context.Dogs
            .AsNoTracking()
            .Include(dog => dog.Shelter)
            .AsQueryable();

        if (shelterId.HasValue)
        {
            query = query.Where(dog => dog.ShelterId == shelterId.Value);
        }

        return query;
    }

    private IQueryable<ResourceStock> ResourcesForScope(int? shelterId)
    {
        var query = context.ResourceStocks
            .AsNoTracking()
            .Include(resource => resource.Shelter)
            .Include(resource => resource.ResourceCategory)
            .AsQueryable();

        if (shelterId.HasValue)
        {
            query = query.Where(resource => resource.ShelterId == shelterId.Value);
        }

        return query;
    }

    private async Task<AdoptionFunnelDto> BuildAdoptionFunnelAsync(
        AnalyticsDateRange range,
        int? shelterId,
        CancellationToken cancellationToken)
    {
        var requests = await AdoptionRequestsForScope(shelterId)
            .Where(request => request.CreatedAt >= range.StartUtc && request.CreatedAt < range.EndUtc)
            .Select(request => new
            {
                request.Status,
                request.VisitStatus,
                request.CreatedAt,
                request.UpdatedAt,
                request.VisitConfirmedAt
            })
            .ToListAsync(cancellationToken);

        var submitted = requests.Count;
        var pending = requests.Count(request => request.Status == AdoptionRequestStatus.Pending);
        var visitConfirmed = requests.Count(request =>
            request.Status == AdoptionRequestStatus.VisitConfirmed ||
            request.VisitStatus == AdoptionVisitStatus.Confirmed ||
            request.VisitStatus == AdoptionVisitStatus.Completed);
        var accepted = requests.Count(request => request.Status == AdoptionRequestStatus.Accepted);
        var rejected = requests.Count(request => request.Status == AdoptionRequestStatus.Rejected);
        var cancelled = requests.Count(request => request.Status == AdoptionRequestStatus.Cancelled);
        var finalDecisions = requests
            .Where(request => IsFinalDecision(request.Status) && request.UpdatedAt >= request.CreatedAt)
            .Select(request => (request.UpdatedAt - request.CreatedAt).TotalHours)
            .ToList();
        var confirmedVisits = requests
            .Where(request => request.VisitConfirmedAt.HasValue && request.VisitConfirmedAt.Value >= request.CreatedAt)
            .Select(request => (request.VisitConfirmedAt!.Value - request.CreatedAt).TotalHours)
            .ToList();
        var pendingAges = requests
            .Where(request => request.Status == AdoptionRequestStatus.Pending)
            .Select(request => (DateTime.UtcNow - request.CreatedAt).TotalHours)
            .ToList();

        return new AdoptionFunnelDto(
            submitted,
            pending,
            visitConfirmed,
            accepted,
            rejected,
            cancelled,
            Rate(accepted, submitted),
            Rate(visitConfirmed, submitted),
            Rate(rejected + cancelled, submitted),
            AverageOrNull(confirmedVisits),
            AverageOrNull(finalDecisions),
            AverageOrNull(pendingAges));
    }

    private async Task<IReadOnlyList<AdoptionTrendPointDto>> BuildRequestTrendsAsync(
        AnalyticsDateRange range,
        int? shelterId,
        CancellationToken cancellationToken)
    {
        var requests = await AdoptionRequestsForScope(shelterId)
            .Where(request =>
                (request.CreatedAt >= range.StartUtc && request.CreatedAt < range.EndUtc) ||
                (request.UpdatedAt >= range.StartUtc && request.UpdatedAt < range.EndUtc) ||
                (request.VisitConfirmedAt.HasValue &&
                 request.VisitConfirmedAt.Value >= range.StartUtc &&
                 request.VisitConfirmedAt.Value < range.EndUtc))
            .Select(request => new
            {
                request.Status,
                request.CreatedAt,
                request.UpdatedAt,
                request.VisitConfirmedAt
            })
            .ToListAsync(cancellationToken);

        var useWeeklyBuckets = range.TotalDays > 60;
        var buckets = CreateBuckets(range, useWeeklyBuckets);
        var points = buckets
            .Select(bucket =>
            {
                var bucketEnd = useWeeklyBuckets ? bucket.AddDays(7) : bucket.AddDays(1);
                var submitted = requests.Count(request => IsInBucket(request.CreatedAt, bucket, bucketEnd));
                var visitConfirmed = requests.Count(request =>
                    request.VisitConfirmedAt.HasValue &&
                    IsInBucket(request.VisitConfirmedAt.Value, bucket, bucketEnd));
                var accepted = requests.Count(request =>
                    request.Status == AdoptionRequestStatus.Accepted &&
                    IsInBucket(request.UpdatedAt, bucket, bucketEnd));
                var rejectedCancelled = requests.Count(request =>
                    (request.Status == AdoptionRequestStatus.Rejected || request.Status == AdoptionRequestStatus.Cancelled) &&
                    IsInBucket(request.UpdatedAt, bucket, bucketEnd));

                return new AdoptionTrendPointDto(
                    bucket,
                    useWeeklyBuckets ? $"Week of {bucket:dd MMM}" : bucket.ToString("dd MMM"),
                    submitted,
                    visitConfirmed,
                    accepted,
                    rejectedCancelled);
            })
            .ToList();

        return points;
    }

    private async Task<IReadOnlyList<StatusBreakdownDto>> BuildDogStatusBreakdownAsync(
        int? shelterId,
        CancellationToken cancellationToken)
    {
        var groups = await DogsForScope(shelterId)
            .GroupBy(dog => dog.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var total = groups.Sum(group => group.Count);
        return Enum.GetValues<DogStatus>()
            .Select(status =>
            {
                var count = groups.FirstOrDefault(group => group.Status == status)?.Count ?? 0;
                return new StatusBreakdownDto(FormatEnum(status), count, Rate(count, total));
            })
            .ToList();
    }

    private async Task<IReadOnlyList<ShelterWorkloadDto>> BuildShelterWorkloadAsync(
        AnalyticsDateRange range,
        int? shelterId,
        CancellationToken cancellationToken)
    {
        var shelters = await context.Shelters
            .AsNoTracking()
            .Where(shelter => !shelterId.HasValue || shelter.Id == shelterId.Value)
            .OrderBy(shelter => shelter.Name)
            .Select(shelter => new { shelter.Id, shelter.Name })
            .ToListAsync(cancellationToken);

        var dogCounts = await context.Dogs
            .AsNoTracking()
            .Where(dog => !shelterId.HasValue || dog.ShelterId == shelterId.Value)
            .GroupBy(dog => dog.ShelterId)
            .Select(group => new { ShelterId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ShelterId, item => item.Count, cancellationToken);

        var requests = await AdoptionRequestsForScope(shelterId)
            .Where(request => request.CreatedAt >= range.StartUtc && request.CreatedAt < range.EndUtc)
            .Select(request => new
            {
                ShelterId = request.Dog!.ShelterId,
                request.Status,
                request.CreatedAt,
                request.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var pendingRequests = await AdoptionRequestsForScope(shelterId)
            .Where(request => request.Status == AdoptionRequestStatus.Pending)
            .Select(request => new
            {
                ShelterId = request.Dog!.ShelterId,
                request.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var lowStockCounts = await context.ResourceStocks
            .AsNoTracking()
            .Where(resource => !shelterId.HasValue || resource.ShelterId == shelterId.Value)
            .Where(resource => resource.Quantity <= resource.LowStockThreshold)
            .GroupBy(resource => resource.ShelterId)
            .Select(group => new { ShelterId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ShelterId, item => item.Count, cancellationToken);

        return shelters
            .Select(shelter =>
            {
                var shelterRequests = requests.Where(request => request.ShelterId == shelter.Id).ToList();
                var shelterPending = pendingRequests.Where(request => request.ShelterId == shelter.Id).ToList();
                var finalDecisionHours = shelterRequests
                    .Where(request => IsFinalDecision(request.Status) && request.UpdatedAt >= request.CreatedAt)
                    .Select(request => (request.UpdatedAt - request.CreatedAt).TotalHours)
                    .ToList();
                var pendingAgeHours = shelterPending
                    .Select(request => (DateTime.UtcNow - request.CreatedAt).TotalHours)
                    .ToList();

                return new ShelterWorkloadDto(
                    shelter.Id,
                    shelter.Name,
                    dogCounts.GetValueOrDefault(shelter.Id),
                    shelterRequests.Count,
                    shelterPending.Count,
                    shelterRequests.Count(request => request.Status == AdoptionRequestStatus.Accepted),
                    lowStockCounts.GetValueOrDefault(shelter.Id),
                    AverageOrNull(pendingAgeHours),
                    AverageOrNull(finalDecisionHours));
            })
            .OrderByDescending(workload => workload.PendingRequests)
            .ThenByDescending(workload => workload.RequestsInRange)
            .ThenBy(workload => workload.ShelterName)
            .ToList();
    }

    private async Task<DogVisibilityResult> BuildDogVisibilityAsync(
        AnalyticsDateRange range,
        int? shelterId,
        CancellationToken cancellationToken)
    {
        var dogs = await DogsForScope(shelterId)
            .Select(dog => new
            {
                dog.Id,
                dog.Name,
                dog.Status,
                ShelterName = dog.Shelter == null ? "Unknown shelter" : dog.Shelter.Name
            })
            .ToListAsync(cancellationToken);
        var dogIds = dogs.Select(dog => dog.Id).ToHashSet();

        var views = await context.RecentlyViewedDogs
            .AsNoTracking()
            .Where(view => dogIds.Contains(view.DogId) && view.ViewedAt >= range.StartUtc && view.ViewedAt < range.EndUtc)
            .GroupBy(view => view.DogId)
            .Select(group => new { DogId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.DogId, item => item.Count, cancellationToken);

        var favorites = await context.FavoriteDogs
            .AsNoTracking()
            .Where(favorite => dogIds.Contains(favorite.DogId) && favorite.CreatedAt >= range.StartUtc && favorite.CreatedAt < range.EndUtc)
            .GroupBy(favorite => favorite.DogId)
            .Select(group => new { DogId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.DogId, item => item.Count, cancellationToken);

        var requests = await context.AdoptionRequests
            .AsNoTracking()
            .Where(request => dogIds.Contains(request.DogId) && request.CreatedAt >= range.StartUtc && request.CreatedAt < range.EndUtc)
            .GroupBy(request => request.DogId)
            .Select(group => new { DogId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.DogId, item => item.Count, cancellationToken);

        var rows = dogs
            .Select(dog =>
            {
                var viewCount = views.GetValueOrDefault(dog.Id);
                var favoriteCount = favorites.GetValueOrDefault(dog.Id);
                var requestCount = requests.GetValueOrDefault(dog.Id);
                var insight = requestCount > 0
                    ? "Converts interest into adoption requests"
                    : viewCount > 0 || favoriteCount > 0
                        ? "Visible but not requested yet"
                        : "Low recent engagement";

                return new DogVisibilityDto(
                    dog.Id,
                    dog.Name,
                    dog.ShelterName,
                    dog.Status,
                    viewCount,
                    favoriteCount,
                    requestCount,
                    insight);
            })
            .ToList();

        return new DogVisibilityResult(
            rows
                .Where(row => row.RecentViewActivity > 0)
                .OrderByDescending(row => row.RecentViewActivity)
                .ThenByDescending(row => row.AdoptionRequests)
                .Take(TopListSize)
                .ToList(),
            rows
                .Where(row => row.Favorites > 0)
                .OrderByDescending(row => row.Favorites)
                .ThenByDescending(row => row.AdoptionRequests)
                .Take(TopListSize)
                .ToList(),
            rows
                .Where(row => row.Status == DogStatus.Available && row.RecentViewActivity == 0 && row.AdoptionRequests == 0)
                .OrderBy(row => row.DogName)
                .Take(TopListSize)
                .ToList());
    }

    private async Task<ResourceAnalyticsDto> BuildResourceAnalyticsAsync(
        int? shelterId,
        CancellationToken cancellationToken)
    {
        var resources = await ResourcesForScope(shelterId)
            .Select(resource => new
            {
                resource.Id,
                resource.Name,
                resource.Quantity,
                resource.LowStockThreshold,
                resource.Unit,
                ShelterName = resource.Shelter == null ? "Unknown shelter" : resource.Shelter.Name,
                CategoryName = resource.ResourceCategory == null ? "Uncategorized" : resource.ResourceCategory.Name,
                IsLowStock = resource.Quantity <= resource.LowStockThreshold
            })
            .ToListAsync(cancellationToken);

        var lowStockByCategory = resources
            .GroupBy(resource => resource.CategoryName)
            .Select(group => new ResourceCategoryAnalyticsDto(
                group.Key,
                group.Count(resource => resource.IsLowStock),
                group.Count()))
            .Where(group => group.LowStockCount > 0)
            .OrderByDescending(group => group.LowStockCount)
            .ThenBy(group => group.CategoryName)
            .ToList();

        var closestToThreshold = resources
            .Where(resource => resource.IsLowStock)
            .OrderBy(resource => resource.Quantity - resource.LowStockThreshold)
            .ThenBy(resource => resource.Name)
            .Take(TopListSize)
            .Select(resource => new LowStockResourceDto(
                resource.Id,
                resource.Name,
                resource.ShelterName,
                resource.CategoryName,
                resource.Quantity,
                resource.LowStockThreshold,
                resource.Unit))
            .ToList();

        return new ResourceAnalyticsDto(
            resources.Count,
            resources.Count(resource => resource.IsLowStock),
            lowStockByCategory,
            closestToThreshold);
    }

    private async Task<ReportActivityAnalyticsDto> BuildReportActivityAsync(
        AnalyticsDateRange range,
        int? shelterId,
        CancellationToken cancellationToken)
    {
        var reports = await context.ReportHistories
            .AsNoTracking()
            .Where(report => report.GeneratedAt >= range.StartUtc && report.GeneratedAt < range.EndUtc)
            .Where(report => !shelterId.HasValue || report.ShelterId == shelterId.Value)
            .Select(report => new
            {
                report.ReportType,
                report.WasSuccessful,
                report.SentAt
            })
            .ToListAsync(cancellationToken);

        var reportsByType = reports
            .GroupBy(report => report.ReportType)
            .Select(group => new ReportTypeAnalyticsDto(
                FormatReportType(group.Key),
                group.Count(),
                group.Count(report => report.WasSuccessful),
                group.Count(report => !report.WasSuccessful)))
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.ReportType)
            .ToList();

        return new ReportActivityAnalyticsDto(
            reports.Count,
            reports.Count(report => report.SentAt.HasValue && report.WasSuccessful),
            reports.Count(report => !report.WasSuccessful),
            reportsByType);
    }

    private async Task<CopilotAnalyticsDto?> BuildCopilotAnalyticsAsync(
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        var sessions = await context.CopilotSessions
            .AsNoTracking()
            .Where(session => session.CreatedAt >= range.StartUtc && session.CreatedAt < range.EndUtc)
            .Select(session => new
            {
                session.PrimaryIntent,
                session.UsedAiEnhancement,
                session.UsedSemanticSearch,
                session.UsedToolCalling,
                session.FallbackReason,
                session.ResultCount
            })
            .ToListAsync(cancellationToken);

        var feedback = await context.CopilotResultFeedbacks
            .AsNoTracking()
            .Where(item => item.CreatedAt >= range.StartUtc && item.CreatedAt < range.EndUtc)
            .Select(item => item.FeedbackType)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0 && feedback.Count == 0)
        {
            return null;
        }

        var topIntents = sessions
            .GroupBy(session => string.IsNullOrWhiteSpace(session.PrimaryIntent) ? "Unknown" : session.PrimaryIntent!)
            .Select(group => new CopilotIntentAnalyticsDto(group.Key, group.Count()))
            .OrderByDescending(intent => intent.Count)
            .ThenBy(intent => intent.Intent)
            .Take(TopListSize)
            .ToList();

        return new CopilotAnalyticsDto(
            sessions.Count,
            sessions.Count(session => session.UsedAiEnhancement),
            sessions.Count(session => !string.IsNullOrWhiteSpace(session.FallbackReason) || !session.UsedAiEnhancement),
            sessions.Count(session => session.UsedSemanticSearch),
            sessions.Count(session => session.UsedToolCalling),
            sessions.Count == 0 ? 0 : Math.Round(sessions.Average(session => session.ResultCount), 1),
            feedback.Count(type => type is CopilotFeedbackType.Positive or CopilotFeedbackType.GoodExplanation),
            feedback.Count(type => type is CopilotFeedbackType.Negative or CopilotFeedbackType.NotRelevant or CopilotFeedbackType.MissingInformation),
            topIntents);
    }

    private static IReadOnlyList<AnalyticsSummaryCardDto> BuildAdminSummaryCards(
        AdoptionFunnelDto funnel,
        IReadOnlyList<StatusBreakdownDto> dogStatusBreakdown,
        ResourceAnalyticsDto resourceAnalytics,
        ReportActivityAnalyticsDto reportActivity,
        CopilotAnalyticsDto? copilotAnalytics)
    {
        var availableDogs = dogStatusBreakdown.FirstOrDefault(status => status.Label == FormatEnum(DogStatus.Available))?.Count ?? 0;
        return
        [
            new("Submitted requests", funnel.SubmittedRequests.ToString(), $"{funnel.ConversionRate:0.#}% converted to adoption", "primary"),
            new("Available dogs", availableDogs.ToString(), "Current public-safe adoption supply", "success"),
            new("Low-stock resources", resourceAnalytics.LowStockResources.ToString(), $"{resourceAnalytics.TotalResources} resource records checked", "warning"),
            new("Reports generated", reportActivity.ReportsGenerated.ToString(), $"{reportActivity.FailedReports} failed in range", "info"),
            new("Copilot sessions", (copilotAnalytics?.Sessions ?? 0).ToString(), copilotAnalytics is null ? "No Copilot history in range" : $"{copilotAnalytics.AiEnhancedSessions} AI-enhanced", "info")
        ];
    }

    private static IReadOnlyList<AnalyticsSummaryCardDto> BuildShelterSummaryCards(
        AdoptionFunnelDto funnel,
        IReadOnlyList<StatusBreakdownDto> dogStatusBreakdown,
        ResourceAnalyticsDto resourceAnalytics,
        ReportActivityAnalyticsDto reportActivity)
    {
        var availableDogs = dogStatusBreakdown.FirstOrDefault(status => status.Label == FormatEnum(DogStatus.Available))?.Count ?? 0;
        return
        [
            new("Submitted requests", funnel.SubmittedRequests.ToString(), $"{funnel.PendingRequests} still pending", "primary"),
            new("Available dogs", availableDogs.ToString(), "Current dogs ready for adopters", "success"),
            new("Low-stock resources", resourceAnalytics.LowStockResources.ToString(), "Current stock snapshot", "warning"),
            new("Reports generated", reportActivity.ReportsGenerated.ToString(), "Metadata only, files are not stored here", "info")
        ];
    }

    private static IReadOnlyList<DateTime> CreateBuckets(AnalyticsDateRange range, bool weekly)
    {
        var buckets = new List<DateTime>();
        var current = weekly ? StartOfWeek(range.StartUtc.Date) : range.StartUtc.Date;
        while (current < range.EndUtc)
        {
            buckets.Add(current);
            current = current.AddDays(weekly ? 7 : 1);
        }

        return buckets;
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset).Date;
    }

    private static bool IsInBucket(DateTime value, DateTime bucketStart, DateTime bucketEnd)
    {
        return value >= bucketStart && value < bucketEnd;
    }

    private static bool IsFinalDecision(AdoptionRequestStatus status)
    {
        return status is AdoptionRequestStatus.Accepted or AdoptionRequestStatus.Rejected or AdoptionRequestStatus.Cancelled;
    }

    private static double Rate(int part, int total)
    {
        return total <= 0 ? 0 : Math.Round(part * 100.0 / total, 1);
    }

    private static double? AverageOrNull(IReadOnlyCollection<double> values)
    {
        return values.Count == 0 ? null : Math.Round(values.Average(), 1);
    }

    private static string FormatEnum<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var text = value.ToString();
        return string.Concat(text.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));
    }

    private static string FormatReportType(string reportType)
    {
        return reportType switch
        {
            ReportHistoryTypes.ShelterSummaryReport => "Shelter summary",
            ReportHistoryTypes.AdminPlatformSummaryReport => "Admin platform summary",
            ReportHistoryTypes.AdoptionRequestReport => "Adoption request",
            ReportHistoryTypes.AdoptionStatusReport => "Adoption status",
            ReportHistoryTypes.LowStockResourceReport => "Low-stock resource",
            ReportHistoryTypes.ShelterRegistrationRequestReport => "Shelter application",
            ReportHistoryTypes.CsvExport => "CSV export",
            ReportHistoryTypes.PdfExport => "PDF export",
            _ => FormatIdentifier(reportType)
        };
    }

    private static string FormatIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        return string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));
    }

    private sealed record DogVisibilityResult(
        IReadOnlyList<DogVisibilityDto> MostViewed,
        IReadOnlyList<DogVisibilityDto> MostFavorited,
        IReadOnlyList<DogVisibilityDto> LowEngagement);
}
