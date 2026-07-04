using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ShelterOperationsAssistantService(
    ApplicationDbContext context,
    IOpenAiShelterOperationsAssistantClient openAiClient,
    IOptions<OpenAiSettings> options,
    ILogger<ShelterOperationsAssistantService> logger) : IShelterOperationsAssistantService
{
    private const int OldPendingRequestDays = 5;
    private const int LongReservedDogDays = 14;
    private const int MaxPriorityItems = 10;
    private const int MaxSuggestedActions = 8;
    private const int MaxWarnings = 6;
    private const int MaxHighlights = 8;

    public Task<ShelterOperationsBriefDto> GenerateDailyBriefAsync(
        string shelterUserId,
        CancellationToken cancellationToken = default)
    {
        return GenerateBriefAsync(
            shelterUserId,
            new ShelterOperationsBriefRequest(ShelterOperationsBriefPeriod.Today),
            cancellationToken);
    }

    public async Task<ShelterOperationsBriefDto> GenerateBriefAsync(
        string shelterUserId,
        ShelterOperationsBriefRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureShelterUserAsync(shelterUserId, cancellationToken);

        var shelter = await context.Shelters
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ApplicationUserId == shelterUserId, cancellationToken);

        if (shelter is null)
        {
            throw new InvalidOperationException("No shelter profile is linked to this account.");
        }

        var now = DateTime.UtcNow;
        var period = GetPeriodRange(request.Period, now);
        var deterministic = await BuildDeterministicBriefAsync(shelter, shelterUserId, request.Period, period, now, cancellationToken);
        var settings = options.Value;

        if (!settings.Enabled || !settings.ShelterOperationsAssistantEnabled || !settings.HasApiKey)
        {
            return deterministic with { FallbackReason = "OpenAI shelter operations assistant is disabled or not configured." };
        }

        try
        {
            var aiResponse = await openAiClient.GenerateBriefAsync(BuildAiInput(deterministic), cancellationToken);
            if (!aiResponse.Success || aiResponse.Brief is null)
            {
                return deterministic with { FallbackReason = aiResponse.ErrorMessage ?? "OpenAI shelter operations assistant was unavailable." };
            }

            var merged = MergeAiBrief(deterministic, aiResponse.Brief);
            return merged is null
                ? deterministic with { FallbackReason = "OpenAI shelter operations assistant returned an invalid brief." }
                : merged;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Shelter operations assistant AI generation failed. Using deterministic fallback.");
            return deterministic with { FallbackReason = "OpenAI shelter operations assistant failed." };
        }
    }

    private async Task EnsureShelterUserAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("A signed-in shelter user is required.");
        }

        var isShelter = await context.UserRoles
            .AsNoTracking()
            .Join(
                context.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, RoleName = role.Name })
            .AnyAsync(role => role.UserId == userId && role.RoleName == IdentitySeedData.ShelterRole, cancellationToken);

        if (!isShelter)
        {
            throw new InvalidOperationException("Only shelter accounts can use the shelter operations assistant.");
        }
    }

    private async Task<ShelterOperationsBriefDto> BuildDeterministicBriefAsync(
        Shelter shelter,
        string shelterUserId,
        ShelterOperationsBriefPeriod period,
        PeriodRange periodRange,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var requests = await context.AdoptionRequests
            .AsNoTracking()
            .Include(request => request.Dog)
            .Where(request => request.Dog != null && request.Dog.ShelterId == shelter.Id)
            .Select(request => new
            {
                request.Id,
                request.Status,
                request.VisitStatus,
                request.CreatedAt,
                request.UpdatedAt,
                request.PreferredVisitDateTime,
                request.VisitConfirmedAt,
                DogId = request.Dog!.Id,
                DogName = request.Dog.Name
            })
            .ToListAsync(cancellationToken);

        var dogs = await context.Dogs
            .AsNoTracking()
            .Include(dog => dog.Images)
            .Where(dog => dog.ShelterId == shelter.Id)
            .Select(dog => new
            {
                dog.Id,
                dog.Name,
                dog.Status,
                dog.Description,
                dog.BehaviorDescription,
                Images = dog.Images.ToList()
            })
            .ToListAsync(cancellationToken);

        var resources = await context.ResourceStocks
            .AsNoTracking()
            .Include(resource => resource.ResourceCategory)
            .Where(resource => resource.ShelterId == shelter.Id)
            .Select(resource => new
            {
                resource.Id,
                resource.Name,
                resource.Quantity,
                resource.LowStockThreshold,
                resource.Unit,
                CategoryName = resource.ResourceCategory == null ? "Uncategorized" : resource.ResourceCategory.Name
            })
            .ToListAsync(cancellationToken);

        var reservedSince = await context.DogStatusHistories
            .AsNoTracking()
            .Where(history =>
                history.Dog != null &&
                history.Dog.ShelterId == shelter.Id &&
                history.NewStatus == DogStatus.Reserved)
            .GroupBy(history => history.DogId)
            .Select(group => new { DogId = group.Key, ReservedAt = group.Max(history => history.ChangedAt) })
            .ToDictionaryAsync(item => item.DogId, item => item.ReservedAt, cancellationToken);

        var unreadNotifications = await context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == shelterUserId && !notification.IsRead)
            .Where(notification => notification.CreatedAt >= now.AddDays(-7))
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(5)
            .Select(notification => new
            {
                notification.Title,
                notification.Message,
                notification.Type,
                notification.Link
            })
            .ToListAsync(cancellationToken);

        var recentReports = await context.ReportHistories
            .AsNoTracking()
            .Where(report => report.ShelterId == shelter.Id && report.GeneratedAt >= periodRange.RecentStartUtc)
            .Select(report => new
            {
                report.ReportType,
                report.WasSuccessful,
                report.GeneratedAt
            })
            .ToListAsync(cancellationToken);

        var pendingRequests = requests
            .Where(request => request.Status == AdoptionRequestStatus.Pending)
            .OrderBy(request => request.CreatedAt)
            .ToList();
        var staleRequests = pendingRequests
            .Where(request => (now - request.CreatedAt).TotalDays >= OldPendingRequestDays)
            .ToList();
        var upcomingVisits = requests
            .Where(request =>
                (request.Status == AdoptionRequestStatus.VisitConfirmed || request.VisitStatus == AdoptionVisitStatus.Confirmed) &&
                request.PreferredVisitDateTime.HasValue &&
                request.PreferredVisitDateTime.Value >= now &&
                request.PreferredVisitDateTime.Value < periodRange.UpcomingEndUtc)
            .OrderBy(request => request.PreferredVisitDateTime)
            .Take(MaxHighlights)
            .Select(request => new ShelterOperationsVisitDto(
                request.Id,
                request.DogName,
                request.PreferredVisitDateTime,
                FormatVisitStatus(request.VisitStatus),
                "/shelter/adoption-requests"))
            .ToList();
        var missingVisitConfirmation = pendingRequests
            .Where(request => request.VisitStatus is AdoptionVisitStatus.NotScheduled or AdoptionVisitStatus.Requested)
            .ToList();
        var recentClosedRequests = requests
            .Where(request =>
                request.UpdatedAt >= periodRange.RecentStartUtc &&
                request.Status is AdoptionRequestStatus.Accepted or AdoptionRequestStatus.Rejected or AdoptionRequestStatus.Cancelled)
            .ToList();
        var requestHighlights = pendingRequests
            .Take(MaxHighlights)
            .Select(request => new ShelterOperationsRequestHighlightDto(
                request.Id,
                request.DogName,
                request.Status.ToString(),
                FormatVisitStatus(request.VisitStatus),
                request.CreatedAt,
                Math.Max(0, (int)Math.Floor((now - request.CreatedAt).TotalDays)),
                "/shelter/adoption-requests"))
            .ToList();
        var reservedDogs = dogs
            .Where(dog => dog.Status == DogStatus.Reserved)
            .ToList();
        var longReservedDogs = reservedDogs
            .Where(dog => reservedSince.TryGetValue(dog.Id, out var reservedAt) &&
                          (now - reservedAt).TotalDays >= LongReservedDogDays)
            .ToList();
        var lowStockItems = resources
            .Where(resource => resource.Quantity <= resource.LowStockThreshold)
            .OrderBy(resource => GetResourcePriority(resource.Quantity, resource.LowStockThreshold))
            .ThenBy(resource => resource.Quantity - resource.LowStockThreshold)
            .Take(MaxHighlights)
            .Select(resource => new ShelterOperationsResourceItemDto(
                resource.Id,
                resource.Name,
                resource.CategoryName,
                resource.Quantity,
                resource.LowStockThreshold,
                resource.Unit,
                GetResourcePriority(resource.Quantity, resource.LowStockThreshold),
                "/shelter/resources"))
            .ToList();
        var profileHighlights = dogs
            .Where(dog => dog.Status is DogStatus.Available or DogStatus.Reserved)
            .Select(dog => new ShelterOperationsDogProfileItemDto(
                dog.Id,
                dog.Name,
                dog.Status,
                GetMissingProfileFields(dog.Description, dog.BehaviorDescription, dog.Images),
                $"/shelter/dogs/edit/{dog.Id}"))
            .Where(dog => dog.MissingFields.Count > 0)
            .Take(MaxHighlights)
            .ToList();

        var priorityItems = BuildPriorityItems(
            staleRequests,
            upcomingVisits,
            longReservedDogs.Select(dog => (dog.Id, dog.Name, ReservedAt: reservedSince[dog.Id])).ToList(),
            lowStockItems,
            profileHighlights,
            unreadNotifications,
            recentReports.Where(report => !report.WasSuccessful).ToList(),
            now);
        var metrics = BuildMetrics(
            pendingRequests.Count,
            staleRequests.Count,
            upcomingVisits.Count,
            missingVisitConfirmation.Count,
            reservedDogs.Count,
            longReservedDogs.Count,
            lowStockItems.Count,
            profileHighlights.Count,
            unreadNotifications.Count,
            recentClosedRequests.Count);
        var actions = BuildSuggestedActions(priorityItems, pendingRequests.Count, lowStockItems.Count, profileHighlights.Count);
        var insights = BuildInsights(unreadNotifications, recentReports);
        var warnings = BuildWarnings(priorityItems, recentReports);
        var links = new Dictionary<string, string>
        {
            ["Adoption requests"] = "/shelter/adoption-requests",
            ["Resources"] = "/shelter/resources",
            ["Manage dogs"] = "/shelter/dogs",
            ["Reports"] = "/shelter/dashboard"
        };

        var summary = BuildFallbackSummary(
            shelter.Name,
            pendingRequests.Count,
            staleRequests.Count,
            upcomingVisits.Count,
            lowStockItems.Count,
            profileHighlights.Count,
            priorityItems.Count);

        return new ShelterOperationsBriefDto(
            now,
            period,
            shelter.Id,
            shelter.Name,
            UsedAi: false,
            FallbackReason: null,
            summary,
            metrics,
            priorityItems,
            actions,
            upcomingVisits,
            lowStockItems,
            requestHighlights,
            profileHighlights,
            insights,
            warnings,
            links);
    }

    private static IReadOnlyList<ShelterPriorityItemDto> BuildPriorityItems(
        IReadOnlyList<dynamic> staleRequests,
        IReadOnlyList<ShelterOperationsVisitDto> upcomingVisits,
        IReadOnlyList<(int DogId, string DogName, DateTime ReservedAt)> longReservedDogs,
        IReadOnlyList<ShelterOperationsResourceItemDto> lowStockItems,
        IReadOnlyList<ShelterOperationsDogProfileItemDto> profileHighlights,
        IReadOnlyList<dynamic> unreadNotifications,
        IReadOnlyList<dynamic> failedReports,
        DateTime now)
    {
        var items = new List<ShelterPriorityItemDto>();

        foreach (var resource in lowStockItems)
        {
            items.Add(new ShelterPriorityItemDto(
                resource.Priority,
                ShelterOperationsCategory.ResourceStock,
                $"{resource.Name} is below threshold",
                $"{resource.Quantity} {resource.Unit} available; threshold is {resource.LowStockThreshold} {resource.Unit}.",
                "ResourceStock",
                resource.ResourceId.ToString(),
                resource.ActionLink,
                "Review resources"));
        }

        foreach (var request in staleRequests.Take(4))
        {
            var ageDays = Math.Max(0, (int)Math.Floor((now - request.CreatedAt).TotalDays));
            items.Add(new ShelterPriorityItemDto(
                ShelterOperationsPriority.High,
                ShelterOperationsCategory.AdoptionRequest,
                $"Request for {request.DogName} has been pending {ageDays} days",
                "Review the request and decide whether to confirm a visit, ask for more information, reject, or leave a shelter note.",
                "AdoptionRequest",
                request.Id.ToString(),
                "/shelter/adoption-requests",
                "Open requests"));
        }

        foreach (var visit in upcomingVisits.Where(visit =>
                     visit.PreferredVisitDateTime.HasValue &&
                     (visit.PreferredVisitDateTime.Value - now).TotalHours <= 24).Take(4))
        {
            items.Add(new ShelterPriorityItemDto(
                ShelterOperationsPriority.High,
                ShelterOperationsCategory.Visit,
                $"Visit for {visit.DogName} is coming up",
                $"Confirmed visit time: {FormatDateTime(visit.PreferredVisitDateTime)}. Prepare the dog profile and visit notes before the adopter arrives.",
                "AdoptionRequest",
                visit.AdoptionRequestId.ToString(),
                visit.ActionLink,
                "Open requests"));
        }

        foreach (var dog in longReservedDogs.Take(4))
        {
            var reservedDays = Math.Max(0, (int)Math.Floor((now - dog.ReservedAt).TotalDays));
            items.Add(new ShelterPriorityItemDto(
                reservedDays >= 21 ? ShelterOperationsPriority.High : ShelterOperationsPriority.Medium,
                ShelterOperationsCategory.DogStatus,
                $"{dog.DogName} has been reserved {reservedDays} days",
                "Check whether the visit/adoption is still progressing or whether the reservation needs follow-up.",
                "Dog",
                dog.DogId.ToString(),
                "/shelter/adoption-requests",
                "Review adoption requests"));
        }

        foreach (var dog in profileHighlights.Take(4))
        {
            items.Add(new ShelterPriorityItemDto(
                ShelterOperationsPriority.Medium,
                ShelterOperationsCategory.DogProfile,
                $"{dog.DogName}'s profile needs more information",
                $"Missing or weak fields: {string.Join(", ", dog.MissingFields)}.",
                "Dog",
                dog.DogId.ToString(),
                dog.ActionLink,
                "Edit dog profile"));
        }

        foreach (var notification in unreadNotifications.Take(2))
        {
            items.Add(new ShelterPriorityItemDto(
                ShelterOperationsPriority.Info,
                ShelterOperationsCategory.Notification,
                notification.Title,
                notification.Message,
                "Notification",
                null,
                notification.Link,
                string.IsNullOrWhiteSpace(notification.Link) ? null : "Open notification"));
        }

        foreach (var report in failedReports.Take(2))
        {
            items.Add(new ShelterPriorityItemDto(
                ShelterOperationsPriority.Medium,
                ShelterOperationsCategory.Report,
                $"{FormatReportType(report.ReportType)} failed recently",
                "Review report history from the dashboard and regenerate the report manually if needed.",
                "ReportHistory",
                null,
                "/shelter/dashboard",
                "Open dashboard"));
        }

        if (items.Count == 0)
        {
            items.Add(new ShelterPriorityItemDto(
                ShelterOperationsPriority.Info,
                ShelterOperationsCategory.SystemInfo,
                "No urgent shelter operations need attention",
                "Pending requests, visits, resource stock, and profile quality do not show urgent issues right now.",
                null,
                null,
                "/shelter/dashboard",
                "Open dashboard"));
        }

        return items
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Category)
            .Take(MaxPriorityItems)
            .ToList();
    }

    private static IReadOnlyList<ShelterOperationsMetricDto> BuildMetrics(
        int pendingRequests,
        int stalePendingRequests,
        int upcomingVisits,
        int missingVisitConfirmation,
        int reservedDogs,
        int longReservedDogs,
        int lowStockResources,
        int weakProfiles,
        int unreadNotifications,
        int recentClosedRequests)
    {
        return
        [
            new("Pending requests", pendingRequests.ToString(), $"{stalePendingRequests} older than {OldPendingRequestDays} days.", stalePendingRequests > 0 ? ShelterOperationsPriority.High : ShelterOperationsPriority.Info),
            new("Upcoming visits", upcomingVisits.ToString(), "Confirmed visits in the selected period.", upcomingVisits > 0 ? ShelterOperationsPriority.High : ShelterOperationsPriority.Info),
            new("Missing visit confirmations", missingVisitConfirmation.ToString(), "Pending requests that still need visit planning.", missingVisitConfirmation > 0 ? ShelterOperationsPriority.Medium : ShelterOperationsPriority.Info),
            new("Reserved dogs", reservedDogs.ToString(), $"{longReservedDogs} reserved longer than {LongReservedDogDays} days.", longReservedDogs > 0 ? ShelterOperationsPriority.Medium : ShelterOperationsPriority.Info),
            new("Low-stock resources", lowStockResources.ToString(), "Current resource stock snapshot.", lowStockResources > 0 ? ShelterOperationsPriority.High : ShelterOperationsPriority.Info),
            new("Profiles needing attention", weakProfiles.ToString(), "Dogs missing description, behavior, or real images.", weakProfiles > 0 ? ShelterOperationsPriority.Medium : ShelterOperationsPriority.Info),
            new("Unread notifications", unreadNotifications.ToString(), "Unread shelter notifications from the last 7 days.", unreadNotifications > 0 ? ShelterOperationsPriority.Low : ShelterOperationsPriority.Info),
            new("Recent closed requests", recentClosedRequests.ToString(), "Accepted, rejected, or cancelled in the selected period.", ShelterOperationsPriority.Info)
        ];
    }

    private static IReadOnlyList<ShelterSuggestedActionDto> BuildSuggestedActions(
        IReadOnlyList<ShelterPriorityItemDto> priorityItems,
        int pendingRequests,
        int lowStockResources,
        int weakProfiles)
    {
        var actions = new List<ShelterSuggestedActionDto>();

        if (pendingRequests > 0)
        {
            actions.Add(new ShelterSuggestedActionDto(
                "Review adoption requests",
                "Start with older pending requests and confirmed visits due soon.",
                "/shelter/adoption-requests",
                "Open adoption requests",
                priorityItems.Any(item => item.Category == ShelterOperationsCategory.AdoptionRequest && item.Priority <= ShelterOperationsPriority.High)
                    ? ShelterOperationsPriority.High
                    : ShelterOperationsPriority.Medium));
        }

        if (lowStockResources > 0)
        {
            actions.Add(new ShelterSuggestedActionDto(
                "Update resource stock",
                "Check critical stock items and update quantities after restocking.",
                "/shelter/resources",
                "Open resources",
                ShelterOperationsPriority.High));
        }

        if (weakProfiles > 0)
        {
            actions.Add(new ShelterSuggestedActionDto(
                "Improve dog profiles",
                "Add specific behavior notes and real images to help adopters decide confidently.",
                "/shelter/dogs",
                "Manage dogs",
                ShelterOperationsPriority.Medium));
        }

        if (actions.Count == 0)
        {
            actions.Add(new ShelterSuggestedActionDto(
                "Keep monitoring daily operations",
                "There are no urgent items right now. Keep profiles, visits, and resources up to date.",
                "/shelter/dashboard",
                "Open dashboard",
                ShelterOperationsPriority.Info));
        }

        return actions.Take(MaxSuggestedActions).ToList();
    }

    private static IReadOnlyList<ShelterAssistantInsightDto> BuildInsights(
        IReadOnlyList<dynamic> unreadNotifications,
        IReadOnlyList<dynamic> recentReports)
    {
        var insights = new List<ShelterAssistantInsightDto>();

        if (unreadNotifications.Count > 0)
        {
            insights.Add(new ShelterAssistantInsightDto(
                "Unread notifications",
                $"{unreadNotifications.Count} recent unread notification(s) may need attention.",
                ShelterOperationsCategory.Notification,
                ShelterOperationsPriority.Low,
                "/notifications"));
        }

        if (recentReports.Count > 0)
        {
            var failed = recentReports.Count(report => !report.WasSuccessful);
            insights.Add(new ShelterAssistantInsightDto(
                "Recent report activity",
                failed == 0
                    ? $"{recentReports.Count} report(s) were generated recently."
                    : $"{failed} of {recentReports.Count} recent report(s) failed.",
                ShelterOperationsCategory.Report,
                failed > 0 ? ShelterOperationsPriority.Medium : ShelterOperationsPriority.Info,
                "/shelter/dashboard"));
        }

        return insights;
    }

    private static IReadOnlyList<string> BuildWarnings(
        IReadOnlyList<ShelterPriorityItemDto> priorityItems,
        IReadOnlyList<dynamic> recentReports)
    {
        var warnings = new List<string>();
        if (priorityItems.Any(item => item.Priority == ShelterOperationsPriority.Critical))
        {
            warnings.Add("At least one item is marked critical. Review it before handling lower-priority tasks.");
        }

        if (recentReports.Any(report => !report.WasSuccessful))
        {
            warnings.Add("A recent report failed. The main workflow is not changed, but report history should be reviewed.");
        }

        warnings.Add("Suggestions are advisory. Final decisions remain with shelter staff.");
        return warnings.Take(MaxWarnings).ToList();
    }

    private static ShelterOperationsBriefInputDto BuildAiInput(ShelterOperationsBriefDto brief)
    {
        return new ShelterOperationsBriefInputDto(
            brief.GeneratedAt,
            brief.Period,
            brief.ShelterName,
            brief.Metrics,
            brief.PriorityItems.Select(item => item with
            {
                RelatedEntityId = null,
                ActionLink = null,
                ActionLabel = null,
                IsAiGeneratedText = false
            }).ToList(),
            brief.SuggestedActions.Select(action => action with
            {
                ActionLink = null,
                ActionLabel = null
            }).ToList(),
            brief.UpcomingVisits,
            brief.LowStockItems.Select(item => item with { ActionLink = string.Empty }).ToList(),
            brief.RequestHighlights.Select(item => item with { ActionLink = string.Empty }).ToList(),
            brief.DogProfileHighlights.Select(item => item with { ActionLink = string.Empty }).ToList(),
            brief.Insights.Select(insight => insight with { Link = null }).ToList());
    }

    private static ShelterOperationsBriefDto? MergeAiBrief(
        ShelterOperationsBriefDto deterministic,
        ShelterOperationsAiBriefDto aiBrief)
    {
        var summary = SafeTrim(aiBrief.ExecutiveSummary, 1000);
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        var normalizedPriorities = MergeAiPriorityText(deterministic.PriorityItems, aiBrief.PriorityItems);
        var aiWarnings = NormalizeStrings(aiBrief.Warnings.Concat(aiBrief.Limitations), MaxWarnings);
        var warnings = aiWarnings.Count == 0
            ? deterministic.Warnings
            : aiWarnings
                .Append("Suggestions are advisory. Final decisions remain with shelter staff.")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxWarnings)
                .ToList();

        return deterministic with
        {
            UsedAi = true,
            FallbackReason = null,
            ExecutiveSummary = summary,
            PriorityItems = normalizedPriorities,
            Warnings = warnings
        };
    }

    private static IReadOnlyList<ShelterPriorityItemDto> MergeAiPriorityText(
        IReadOnlyList<ShelterPriorityItemDto> backendItems,
        IReadOnlyList<ShelterOperationsAiPriorityItemDto> aiItems)
    {
        if (aiItems.Count == 0)
        {
            return backendItems;
        }

        return backendItems
            .Select(item =>
            {
                var match = aiItems.FirstOrDefault(aiItem =>
                    Enum.TryParse<ShelterOperationsPriority>(aiItem.Priority, true, out var priority) &&
                    priority == item.Priority &&
                    Enum.TryParse<ShelterOperationsCategory>(aiItem.Category, true, out var category) &&
                    category == item.Category &&
                    string.Equals(aiItem.Title?.Trim(), item.Title, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    return item;
                }

                return item with
                {
                    Description = SafeTrim(match.Description, 500) ?? item.Description,
                    IsAiGeneratedText = true
                };
            })
            .ToList();
    }

    private static IReadOnlyList<string> GetMissingProfileFields(
        string? description,
        string? behaviorDescription,
        IReadOnlyCollection<DogImage> images)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length < 80)
        {
            missing.Add("description");
        }

        if (string.IsNullOrWhiteSpace(behaviorDescription) || behaviorDescription.Trim().Length < 50)
        {
            missing.Add("behavior");
        }

        if (!DogImageUrlValidator.GetRealDogImages(images).Any())
        {
            missing.Add("real image");
        }

        return missing;
    }

    private static ShelterOperationsPriority GetResourcePriority(int quantity, int threshold)
    {
        if (quantity <= 0)
        {
            return ShelterOperationsPriority.Critical;
        }

        if (threshold <= 0)
        {
            return quantity <= 0 ? ShelterOperationsPriority.Critical : ShelterOperationsPriority.Info;
        }

        return quantity <= Math.Max(1, threshold / 2)
            ? ShelterOperationsPriority.Critical
            : ShelterOperationsPriority.High;
    }

    private static string BuildFallbackSummary(
        string shelterName,
        int pendingRequests,
        int staleRequests,
        int upcomingVisits,
        int lowStockResources,
        int profileIssues,
        int priorityCount)
    {
        if (priorityCount == 1 && pendingRequests == 0 && upcomingVisits == 0 && lowStockResources == 0 && profileIssues == 0)
        {
            return $"{shelterName} has no urgent operational issues right now. Keep monitoring adoption requests, visits, resource stock, and dog profile quality.";
        }

        return $"{shelterName} currently has {pendingRequests} pending adoption request(s), {upcomingVisits} upcoming confirmed visit(s), " +
            $"{lowStockResources} low-stock resource item(s), and {profileIssues} dog profile(s) that may need more information. " +
            $"{staleRequests} pending request(s) are older than {OldPendingRequestDays} days and should be reviewed first.";
    }

    private static PeriodRange GetPeriodRange(ShelterOperationsBriefPeriod period, DateTime now)
    {
        var todayStart = now.Date;
        return period switch
        {
            ShelterOperationsBriefPeriod.Next7Days => new PeriodRange(todayStart, todayStart.AddDays(7), now.AddDays(-30), todayStart.AddDays(7)),
            ShelterOperationsBriefPeriod.Last30Days => new PeriodRange(todayStart.AddDays(-29), todayStart.AddDays(1), now.AddDays(-30), now.AddDays(7)),
            _ => new PeriodRange(todayStart, todayStart.AddDays(1), now.AddDays(-7), now.AddDays(1))
        };
    }

    private static IReadOnlyList<string> NormalizeStrings(IEnumerable<string?> values, int take)
    {
        return values
            .Select(value => SafeTrim(value, 260))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    private static string? SafeTrim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string FormatVisitStatus(AdoptionVisitStatus status)
    {
        return status switch
        {
            AdoptionVisitStatus.NotScheduled => "Not scheduled",
            AdoptionVisitStatus.Requested => "Requested",
            AdoptionVisitStatus.Confirmed => "Confirmed",
            AdoptionVisitStatus.Completed => "Completed",
            AdoptionVisitStatus.Cancelled => "Cancelled",
            _ => status.ToString()
        };
    }

    private static string FormatDateTime(DateTime? value)
    {
        return value?.ToLocalTime().ToString("dd MMM yyyy, HH:mm") ?? "No visit time set";
    }

    private static string FormatReportType(string reportType)
    {
        return reportType switch
        {
            ReportHistoryTypes.ShelterSummaryReport => "Shelter summary report",
            ReportHistoryTypes.AdminPlatformSummaryReport => "Admin platform summary",
            ReportHistoryTypes.AdoptionRequestReport => "Adoption request report",
            ReportHistoryTypes.AdoptionStatusReport => "Adoption status report",
            ReportHistoryTypes.LowStockResourceReport => "Low-stock resource report",
            ReportHistoryTypes.ShelterRegistrationRequestReport => "Shelter application report",
            ReportHistoryTypes.CsvExport => "CSV export",
            ReportHistoryTypes.PdfExport => "PDF export",
            _ => reportType
        };
    }

    private sealed record PeriodRange(
        DateTime StartUtc,
        DateTime EndUtc,
        DateTime RecentStartUtc,
        DateTime UpcomingEndUtc);
}
