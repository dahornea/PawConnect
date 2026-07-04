using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class NaturalLanguageSearchService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IOpenAiNaturalLanguageSearchClient openAiClient,
    IOptions<OpenAiSettings> openAiOptions,
    ILogger<NaturalLanguageSearchService> logger) : INaturalLanguageSearchService
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    private static readonly string[] AllowedSortFields =
    [
        "CreatedAt",
        "UpdatedAt",
        "GeneratedAt",
        "ChangedAt",
        "Name",
        "Status",
        "Quantity",
        "PendingRequests",
        "DaysReserved"
    ];

    public async Task<NaturalLanguageSearchResult> SearchAdminAsync(
        NaturalLanguageSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BuildEmptyResult(request.Query, UnknownInterpretation("Please enter a search query."));
        }

        await EnsureAdminAsync(request.CurrentUserId);

        var interpretation = InterpretDeterministically(request.Query);
        if (interpretation.Intent == NaturalLanguageSearchIntent.Unknown && openAiOptions.Value.Enabled)
        {
            var aiResponse = await openAiClient.InterpretAsync(BuildAiRequest(request.Query), cancellationToken);
            if (aiResponse.Success && aiResponse.Interpretation is not null)
            {
                interpretation = ValidateInterpretation(aiResponse.Interpretation);
            }
            else
            {
                interpretation.FallbackReason = aiResponse.ErrorMessage;
                logger.LogInformation(
                    "Natural-language admin search used safe fallback because OpenAI interpretation was unavailable: {Reason}",
                    aiResponse.ErrorMessage);
            }
        }

        interpretation = ValidateInterpretation(interpretation);
        if (interpretation.Intent == NaturalLanguageSearchIntent.Unknown || interpretation.NeedsClarification)
        {
            return BuildEmptyResult(request.Query, interpretation);
        }

        var items = await ExecuteSearchAsync(interpretation, cancellationToken);
        var message = items.Count == 0
            ? "No matching records were found for the interpreted search."
            : $"Found {items.Count} matching record{(items.Count == 1 ? string.Empty : "s")}.";

        return new NaturalLanguageSearchResult(request.Query, interpretation, items, message);
    }

    public async Task<NaturalLanguageSearchResult> SearchShelterAsync(
        NaturalLanguageSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BuildEmptyResult(request.Query, UnknownInterpretation("Please enter a search query."));
        }

        var shelterId = await EnsureShelterAsync(request.CurrentUserId, cancellationToken);

        var interpretation = InterpretDeterministically(request.Query);
        if (interpretation.Intent == NaturalLanguageSearchIntent.Unknown && openAiOptions.Value.Enabled)
        {
            var aiResponse = await openAiClient.InterpretAsync(BuildAiRequest(request.Query, IdentitySeedData.ShelterRole), cancellationToken);
            if (aiResponse.Success && aiResponse.Interpretation is not null)
            {
                interpretation = ValidateShelterInterpretation(aiResponse.Interpretation);
            }
            else
            {
                interpretation.FallbackReason = aiResponse.ErrorMessage;
                logger.LogInformation(
                    "Natural-language shelter search used safe fallback because OpenAI interpretation was unavailable: {Reason}",
                    aiResponse.ErrorMessage);
            }
        }

        interpretation = ValidateShelterInterpretation(interpretation);
        if (interpretation.Intent == NaturalLanguageSearchIntent.Unknown || interpretation.NeedsClarification)
        {
            return BuildEmptyResult(request.Query, interpretation);
        }

        var items = await ExecuteShelterSearchAsync(interpretation, shelterId, cancellationToken);
        var message = items.Count == 0
            ? "No matching shelter records were found for the interpreted search."
            : $"Found {items.Count} matching shelter record{(items.Count == 1 ? string.Empty : "s")}.";

        return new NaturalLanguageSearchResult(request.Query, interpretation, items, message);
    }

    private async Task EnsureAdminAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !await userManager.IsInRoleAsync(user, IdentitySeedData.AdminRole))
        {
            throw new InvalidOperationException("Only administrators can use natural-language admin search.");
        }
    }

    private async Task<int> EnsureShelterAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !await userManager.IsInRoleAsync(user, IdentitySeedData.ShelterRole))
        {
            throw new InvalidOperationException("Only shelter accounts can use shelter operational search.");
        }

        var shelterId = await context.Shelters
            .AsNoTracking()
            .Where(shelter => shelter.ApplicationUserId == userId)
            .Select(shelter => (int?)shelter.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return shelterId ?? throw new InvalidOperationException("Current shelter profile could not be resolved.");
    }

    private NaturalLanguageSearchInterpretation InterpretDeterministically(string query)
    {
        var normalized = Normalize(query);
        var dateRange = DetectDateRange(normalized, DateTime.UtcNow);
        var limit = DetectLimit(normalized);

        if (normalized.Contains("pending shelter") && ContainsAny(normalized, "application", "applications", "request", "requests"))
        {
            return Interpreted(
                NaturalLanguageSearchIntent.FindPendingShelterApplications,
                NaturalLanguageSearchScope.Shelters,
                "The query asks for pending shelter applications.",
                limit,
                dateRange,
                shelterApplicationStatus: ShelterRegistrationRequestStatus.Pending);
        }

        if (ContainsAny(normalized, "low stock", "below threshold", "below low stock"))
        {
            var isShelterQuery = normalized.Contains("shelter") || normalized.Contains("shelters");
            var category = DetectResourceCategory(normalized);
            return Interpreted(
                isShelterQuery ? NaturalLanguageSearchIntent.FindSheltersWithLowStock : NaturalLanguageSearchIntent.FindLowStockResources,
                isShelterQuery ? NaturalLanguageSearchScope.Shelters : NaturalLanguageSearchScope.Resources,
                isShelterQuery
                    ? "The query asks for shelters that have low-stock resources."
                    : "The query asks for resources below their low-stock threshold.",
                limit,
                dateRange,
                lowStockOnly: true,
                resourceCategory: category);
        }

        if (ContainsAny(normalized, "upcoming visit", "upcoming visits", "visits this week", "visits today", "scheduled visit", "scheduled visits"))
        {
            return Interpreted(
                NaturalLanguageSearchIntent.FindUpcomingVisits,
                NaturalLanguageSearchScope.AdoptionRequests,
                "The query asks for upcoming shelter visits.",
                limit,
                dateRange);
        }

        if (ContainsAny(normalized, "pending request", "pending requests"))
        {
            return Interpreted(
                NaturalLanguageSearchIntent.FindPendingRequests,
                NaturalLanguageSearchScope.AdoptionRequests,
                "The query asks for pending adoption requests.",
                limit,
                dateRange,
                requestStatus: AdoptionRequestStatus.Pending);
        }

        if (ContainsAny(normalized, "waiting for visit", "visit confirmation", "confirm visit", "confirm visits"))
        {
            return Interpreted(
                NaturalLanguageSearchIntent.FindRequestsWaitingForVisitConfirmation,
                NaturalLanguageSearchScope.AdoptionRequests,
                "The query asks for adoption requests waiting for shelter visit confirmation.",
                limit,
                dateRange,
                requestStatus: AdoptionRequestStatus.Pending,
                visitStatus: AdoptionVisitStatus.Requested);
        }

        if (TryDetectRequestStatus(normalized, out var requestStatus))
        {
            return Interpreted(
                NaturalLanguageSearchIntent.FindRequestsByStatus,
                NaturalLanguageSearchScope.AdoptionRequests,
                $"The query asks for {FormatEnum(requestStatus)} adoption requests.",
                limit,
                dateRange,
                requestStatus: requestStatus);
        }

        if (normalized.Contains("reserved dog") || normalized.Contains("reserved dogs"))
        {
            var days = DetectOlderThanDays(normalized);
            return Interpreted(
                days.HasValue ? NaturalLanguageSearchIntent.FindReservedDogsTooLong : NaturalLanguageSearchIntent.FindDogsByStatus,
                NaturalLanguageSearchScope.Dogs,
                days.HasValue
                    ? $"The query asks for dogs reserved for more than {days.Value} days."
                    : "The query asks for reserved dogs.",
                limit,
                dateRange,
                dogStatus: DogStatus.Reserved,
                olderThanDays: days);
        }

        if (ContainsAny(normalized, "available dog", "available dogs", "adopted dog", "adopted dogs", "in treatment", "intreatment"))
        {
            var dogStatus = DetectDogStatus(normalized);
            return Interpreted(
                NaturalLanguageSearchIntent.FindDogsByStatus,
                NaturalLanguageSearchScope.Dogs,
                dogStatus is null
                    ? "The query asks for dogs by status."
                    : $"The query asks for {FormatEnum(dogStatus.Value)} dogs.",
                limit,
                dateRange,
                dogStatus: dogStatus,
                city: DetectCity(normalized));
        }

        if (ContainsAll(normalized, "dog", "no") && ContainsAny(normalized, "request", "requests", "recent request", "recent requests"))
        {
            return Interpreted(
                NaturalLanguageSearchIntent.FindDogsWithNoRequests,
                NaturalLanguageSearchScope.Dogs,
                "The query asks for dogs that do not have adoption request history.",
                limit,
                dateRange,
                dogStatus: DetectDogStatus(normalized),
                city: DetectCity(normalized),
                noRequestsOnly: true);
        }

        if (ContainsAny(normalized, "low visibility", "few views", "no views", "few favorites", "no favorites"))
        {
            return Interpreted(
                NaturalLanguageSearchIntent.FindDogsWithLowVisibility,
                NaturalLanguageSearchScope.Dogs,
                "The query asks for dogs with low adopter visibility.",
                limit,
                dateRange,
                dogStatus: DetectDogStatus(normalized));
        }

        if (normalized.Contains("report") || normalized.Contains("reports"))
        {
            return Interpreted(
                NaturalLanguageSearchIntent.FindReportsByDateRange,
                NaturalLanguageSearchScope.Reports,
                "The query asks for generated report history.",
                limit,
                dateRange);
        }

        if (ContainsAny(normalized, "activity", "activities", "audit", "audits", "recent actions", "admin actions", "dog updates", "status changes"))
        {
            return Interpreted(
                NaturalLanguageSearchIntent.FindRecentActivity,
                NaturalLanguageSearchScope.ActivityLogs,
                "The query asks for recent activity log records.",
                limit,
                dateRange);
        }

        if (ContainsAll(normalized, "shelter", "pending", "request"))
        {
            return Interpreted(
                NaturalLanguageSearchIntent.FindSheltersWithManyPendingRequests,
                NaturalLanguageSearchScope.Shelters,
                "The query asks for shelters with several pending adoption requests.",
                limit,
                dateRange);
        }

        return UnknownInterpretation("I could not interpret this search. Try asking for pending requests, reserved dogs, low stock resources, recent reports, or audit activity.");
    }

    private NaturalLanguageSearchInterpretation ValidateInterpretation(NaturalLanguageSearchInterpretation interpretation)
    {
        if (interpretation.Intent == NaturalLanguageSearchIntent.Unknown ||
            interpretation.Scope == NaturalLanguageSearchScope.Unknown)
        {
            interpretation.Intent = NaturalLanguageSearchIntent.Unknown;
            interpretation.Scope = NaturalLanguageSearchScope.Unknown;
            interpretation.NeedsClarification = true;
            interpretation.ClarificationQuestion ??= "Try asking for pending requests, reserved dogs, low stock resources, reports, or recent activity.";
            interpretation.Explanation = string.IsNullOrWhiteSpace(interpretation.Explanation)
                ? "PawConnect could not map the query to a supported operational search."
                : interpretation.Explanation;
            return interpretation;
        }

        interpretation.Limit = interpretation.Limit <= 0 ? DefaultLimit : Math.Min(interpretation.Limit, MaxLimit);
        interpretation.Confidence = Math.Clamp(interpretation.Confidence, 0, 1);

        if (!string.IsNullOrWhiteSpace(interpretation.SortField) &&
            !AllowedSortFields.Contains(interpretation.SortField, StringComparer.OrdinalIgnoreCase))
        {
            interpretation.Warnings.Add($"Unsupported sort field '{interpretation.SortField}' was ignored.");
            interpretation.SortField = null;
        }

        if (interpretation.DateRange?.From is not null &&
            interpretation.DateRange.To is not null &&
            interpretation.DateRange.From > interpretation.DateRange.To)
        {
            interpretation.Warnings.Add("The interpreted date range was invalid and was ignored.");
            interpretation.DateRange = null;
        }

        return interpretation;
    }

    private NaturalLanguageSearchInterpretation ValidateShelterInterpretation(NaturalLanguageSearchInterpretation interpretation)
    {
        interpretation = ValidateInterpretation(interpretation);

        if (interpretation.Intent == NaturalLanguageSearchIntent.Unknown)
        {
            interpretation.ClarificationQuestion = "I could not interpret this search. Try asking for pending requests, reserved dogs, low stock resources, reports this month, or upcoming visits.";
            return interpretation;
        }

        var allowed = interpretation.Scope switch
        {
            NaturalLanguageSearchScope.AdoptionRequests => true,
            NaturalLanguageSearchScope.Dogs => true,
            NaturalLanguageSearchScope.Resources => true,
            NaturalLanguageSearchScope.Reports => true,
            _ => false
        };

        if (!allowed || IsAdminOnlyIntent(interpretation.Intent))
        {
            return UnknownInterpretation("That query is not available for shelter accounts. Try searching your adoption requests, dogs, resources, reports, or upcoming visits.");
        }

        if (!string.IsNullOrWhiteSpace(interpretation.ShelterName))
        {
            interpretation.Warnings.Add("Shelter filters are ignored for shelter search; results are always limited to your own shelter.");
            interpretation.ShelterName = null;
        }

        if (!string.IsNullOrWhiteSpace(interpretation.City))
        {
            interpretation.Warnings.Add("Location filters are ignored for shelter search; results are always limited to your own shelter.");
            interpretation.City = null;
        }

        return interpretation;
    }

    private async Task<IReadOnlyList<NaturalLanguageSearchResultItem>> ExecuteSearchAsync(
        NaturalLanguageSearchInterpretation interpretation,
        CancellationToken cancellationToken)
    {
        return interpretation.Scope switch
        {
            NaturalLanguageSearchScope.AdoptionRequests => await SearchAdoptionRequestsAsync(interpretation, cancellationToken),
            NaturalLanguageSearchScope.Dogs => await SearchDogsAsync(interpretation, cancellationToken),
            NaturalLanguageSearchScope.Resources => await SearchResourcesAsync(interpretation, cancellationToken),
            NaturalLanguageSearchScope.Shelters => await SearchSheltersAsync(interpretation, cancellationToken),
            NaturalLanguageSearchScope.Reports => await SearchReportsAsync(interpretation, cancellationToken),
            NaturalLanguageSearchScope.ActivityLogs => await SearchActivityLogsAsync(interpretation, cancellationToken),
            _ => []
        };
    }

    private async Task<IReadOnlyList<NaturalLanguageSearchResultItem>> ExecuteShelterSearchAsync(
        NaturalLanguageSearchInterpretation interpretation,
        int shelterId,
        CancellationToken cancellationToken)
    {
        return interpretation.Scope switch
        {
            NaturalLanguageSearchScope.AdoptionRequests => await SearchAdoptionRequestsAsync(interpretation, cancellationToken, shelterId),
            NaturalLanguageSearchScope.Dogs => await SearchDogsAsync(interpretation, cancellationToken, shelterId),
            NaturalLanguageSearchScope.Resources => await SearchResourcesAsync(interpretation, cancellationToken, shelterId),
            NaturalLanguageSearchScope.Reports => await SearchReportsAsync(interpretation, cancellationToken, shelterId),
            _ => []
        };
    }

    private async Task<IReadOnlyList<NaturalLanguageSearchResultItem>> SearchAdoptionRequestsAsync(
        NaturalLanguageSearchInterpretation interpretation,
        CancellationToken cancellationToken,
        int? shelterId = null)
    {
        var query = context.AdoptionRequests
            .AsNoTracking()
            .Include(request => request.Dog)
                .ThenInclude(dog => dog!.Shelter)
            .Include(request => request.Dog)
                .ThenInclude(dog => dog!.DogBreed)
            .Include(request => request.Dog)
                .ThenInclude(dog => dog!.SecondaryBreed)
            .AsQueryable();

        if (shelterId.HasValue)
        {
            query = query.Where(request => request.Dog != null && request.Dog.ShelterId == shelterId.Value);
        }

        if (interpretation.RequestStatus.HasValue)
        {
            query = query.Where(request => request.Status == interpretation.RequestStatus.Value);
        }

        if (interpretation.VisitStatus.HasValue &&
            interpretation.Intent != NaturalLanguageSearchIntent.FindUpcomingVisits)
        {
            query = query.Where(request => request.VisitStatus == interpretation.VisitStatus.Value);
        }

        if (interpretation.Intent == NaturalLanguageSearchIntent.FindUpcomingVisits)
        {
            query = query.Where(request =>
                request.PreferredVisitDateTime.HasValue &&
                request.PreferredVisitDateTime.Value >= DateTime.UtcNow &&
                (request.VisitStatus == AdoptionVisitStatus.Requested ||
                 request.VisitStatus == AdoptionVisitStatus.Confirmed));
            query = ApplyVisitDateRange(query, interpretation.DateRange);
        }
        else
        {
            query = ApplyCreatedDateRange(query, interpretation.DateRange);
        }

        if (!string.IsNullOrWhiteSpace(interpretation.DogName))
        {
            query = query.Where(request => request.Dog != null && request.Dog.Name.Contains(interpretation.DogName));
        }

        if (!string.IsNullOrWhiteSpace(interpretation.ShelterName))
        {
            query = query.Where(request => request.Dog != null &&
                request.Dog.Shelter != null &&
                request.Dog.Shelter.Name.Contains(interpretation.ShelterName));
        }

        var requests = await query
            .OrderByDescending(request => request.CreatedAt)
            .Take(interpretation.Limit)
            .ToListAsync(cancellationToken);

        return requests.Select(request => new NaturalLanguageSearchResultItem(
            request.Id.ToString(),
            "Adoption request",
            request.Dog is null ? $"Adoption request #{request.Id}" : $"Request for {request.Dog.Name}",
            request.Dog?.Shelter?.Name ?? "Shelter not available",
            FormatEnum(request.Status),
            $"Visit: {FormatEnum(request.VisitStatus)}. Submitted {FormatDate(request.CreatedAt)}.",
            request.CreatedAt,
            request.PreferredVisitDateTime,
            shelterId.HasValue ? "/shelter/adoption-requests" : "/admin/adoption-requests",
            BuildChips(
                $"Status: {FormatEnum(request.Status)}",
                $"Visit: {FormatEnum(request.VisitStatus)}",
                request.Dog?.Shelter?.Name),
            new Dictionary<string, string>
            {
                ["Dog"] = request.Dog?.Name ?? "-",
                ["Breed"] = request.Dog is null ? "-" : DogBreedFormatter.Format(request.Dog),
                ["Shelter"] = request.Dog?.Shelter?.Name ?? "-",
                ["Preferred visit"] = request.PreferredVisitDateTime?.ToLocalTime().ToString("dd MMM yyyy HH:mm") ?? "-"
            })) .ToList();
    }

    private async Task<IReadOnlyList<NaturalLanguageSearchResultItem>> SearchDogsAsync(
        NaturalLanguageSearchInterpretation interpretation,
        CancellationToken cancellationToken,
        int? shelterId = null)
    {
        var dogQuery = context.Dogs
            .AsNoTracking()
            .Include(dog => dog.Shelter)
            .Include(dog => dog.DogBreed)
            .Include(dog => dog.SecondaryBreed)
            .Include(dog => dog.AdoptionRequests)
            .Include(dog => dog.FavoriteDogs)
            .Include(dog => dog.RecentlyViewedDogs)
            .Include(dog => dog.StatusHistories)
            .AsQueryable();

        if (shelterId.HasValue)
        {
            dogQuery = dogQuery.Where(dog => dog.ShelterId == shelterId.Value);
        }

        if (interpretation.DogStatus == DogStatus.Adopted)
        {
            if (interpretation.DateRange?.From is DateTime from)
            {
                dogQuery = dogQuery.Where(dog => dog.AdoptedAt.HasValue && dog.AdoptedAt.Value >= from);
            }

            if (interpretation.DateRange?.To is DateTime to)
            {
                dogQuery = dogQuery.Where(dog => dog.AdoptedAt.HasValue && dog.AdoptedAt.Value <= to);
            }
        }

        var dogs = await dogQuery.ToListAsync(cancellationToken);

        if (interpretation.DogStatus.HasValue)
        {
            dogs = dogs.Where(dog => dog.Status == interpretation.DogStatus.Value).ToList();
        }

        if (!string.IsNullOrWhiteSpace(interpretation.City))
        {
            dogs = dogs
                .Where(dog => ContainsText(dog.Location, interpretation.City) ||
                              ContainsText(dog.Shelter?.City, interpretation.City) ||
                              ContainsText(dog.Shelter?.Neighborhood, interpretation.City))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(interpretation.ShelterName))
        {
            dogs = dogs.Where(dog => ContainsText(dog.Shelter?.Name, interpretation.ShelterName)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(interpretation.DogName))
        {
            dogs = dogs.Where(dog => ContainsText(dog.Name, interpretation.DogName)).ToList();
        }

        if (interpretation.NoRequestsOnly)
        {
            dogs = dogs.Where(dog => dog.AdoptionRequests.Count == 0).ToList();
        }

        if (interpretation.Intent == NaturalLanguageSearchIntent.FindReservedDogsTooLong)
        {
            var olderThanDays = interpretation.OlderThanDays ?? 10;
            dogs = dogs
                .Where(dog => dog.Status == DogStatus.Reserved)
                .Where(dog => GetReservedSince(dog) is DateTime reservedSince &&
                              (DateTime.UtcNow - reservedSince).TotalDays > olderThanDays)
                .ToList();
        }

        if (interpretation.Intent == NaturalLanguageSearchIntent.FindDogsWithLowVisibility)
        {
            dogs = dogs
                .OrderBy(dog => dog.FavoriteDogs.Count + dog.RecentlyViewedDogs.Count)
                .ThenBy(dog => dog.Name)
                .ToList();
        }
        else
        {
            dogs = SortDogs(dogs, interpretation).ToList();
        }

        return dogs
            .Take(interpretation.Limit)
            .Select(dog =>
            {
                var reservedSince = GetReservedSince(dog);
                var requestCount = dog.AdoptionRequests.Count;
                var visibilityCount = dog.FavoriteDogs.Count + dog.RecentlyViewedDogs.Count;
                var metadata = new Dictionary<string, string>
                {
                    ["Shelter"] = dog.Shelter?.Name ?? "-",
                    ["Breed"] = DogBreedFormatter.Format(dog),
                    ["Requests"] = requestCount.ToString(),
                    ["Visibility signals"] = visibilityCount.ToString()
                };

                if (reservedSince.HasValue)
                {
                    metadata["Reserved since"] = FormatDate(reservedSince.Value);
                    metadata["Days reserved"] = Math.Floor((DateTime.UtcNow - reservedSince.Value).TotalDays).ToString("0");
                }

                return new NaturalLanguageSearchResultItem(
                    dog.Id.ToString(),
                    "Dog",
                    dog.Name,
                    $"{DogBreedFormatter.Format(dog)} · {dog.Shelter?.Name ?? "No shelter"}",
                    FormatEnum(dog.Status),
                    $"{dog.Size} dog in {dog.Location}. {requestCount} adoption request{(requestCount == 1 ? string.Empty : "s")}.",
                    null,
                    reservedSince,
                    shelterId.HasValue ? $"/shelter/dogs/edit/{dog.Id}" : $"/dogs/{dog.Id}?returnUrl=%2Fadmin%2Fsearch",
                    BuildChips(
                        $"Status: {FormatEnum(dog.Status)}",
                        dog.Shelter?.Name,
                        interpretation.Intent == NaturalLanguageSearchIntent.FindDogsWithLowVisibility ? $"Visibility: {visibilityCount}" : null,
                        reservedSince.HasValue ? $"Reserved: {Math.Floor((DateTime.UtcNow - reservedSince.Value).TotalDays):0} days" : null),
                    metadata);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<NaturalLanguageSearchResultItem>> SearchResourcesAsync(
        NaturalLanguageSearchInterpretation interpretation,
        CancellationToken cancellationToken,
        int? shelterId = null)
    {
        var query = context.ResourceStocks
            .AsNoTracking()
            .Include(resource => resource.Shelter)
            .Include(resource => resource.ResourceCategory)
            .Include(resource => resource.FoodType)
            .AsQueryable();

        if (shelterId.HasValue)
        {
            query = query.Where(resource => resource.ShelterId == shelterId.Value);
        }

        if (interpretation.LowStockOnly)
        {
            query = query.Where(resource => resource.Quantity <= resource.LowStockThreshold);
        }

        if (!string.IsNullOrWhiteSpace(interpretation.ResourceCategory))
        {
            query = query.Where(resource => resource.ResourceCategory != null &&
                resource.ResourceCategory.Name.Contains(interpretation.ResourceCategory));
        }

        if (!string.IsNullOrWhiteSpace(interpretation.ShelterName))
        {
            query = query.Where(resource => resource.Shelter != null &&
                resource.Shelter.Name.Contains(interpretation.ShelterName));
        }

        var resources = await query
            .OrderBy(resource => resource.Quantity > resource.LowStockThreshold)
            .ThenBy(resource => resource.Quantity)
            .ThenBy(resource => resource.Name)
            .Take(interpretation.Limit)
            .ToListAsync(cancellationToken);

        return resources.Select(resource => new NaturalLanguageSearchResultItem(
            resource.Id.ToString(),
            "Resource",
            resource.Name,
            resource.Shelter?.Name ?? "Shelter not available",
            resource.Quantity <= resource.LowStockThreshold ? "Low stock" : "In stock",
            $"{resource.Quantity} {resource.Unit}, threshold {resource.LowStockThreshold} {resource.Unit}.",
            resource.LastUpdatedAt,
            null,
            shelterId.HasValue ? "/shelter/resources" : "/admin/shelters",
            BuildChips(
                resource.ResourceCategory?.Name,
                resource.FoodType?.Name,
                resource.Quantity <= resource.LowStockThreshold ? "Low stock" : "In stock"),
            new Dictionary<string, string>
            {
                ["Shelter"] = resource.Shelter?.Name ?? "-",
                ["Category"] = resource.ResourceCategory?.Name ?? "-",
                ["Food type"] = resource.FoodType?.Name ?? "-",
                ["Quantity"] = $"{resource.Quantity} {resource.Unit}",
                ["Threshold"] = $"{resource.LowStockThreshold} {resource.Unit}"
            })).ToList();
    }

    private async Task<IReadOnlyList<NaturalLanguageSearchResultItem>> SearchSheltersAsync(
        NaturalLanguageSearchInterpretation interpretation,
        CancellationToken cancellationToken)
    {
        if (interpretation.Intent == NaturalLanguageSearchIntent.FindPendingShelterApplications)
        {
            var requests = await context.ShelterRegistrationRequests
                .AsNoTracking()
                .Where(request => request.Status == ShelterRegistrationRequestStatus.Pending)
                .OrderByDescending(request => request.SubmittedAt)
                .Take(interpretation.Limit)
                .ToListAsync(cancellationToken);

            return requests.Select(request => new NaturalLanguageSearchResultItem(
                request.Id.ToString(),
                "Shelter application",
                request.ShelterName,
                $"{request.City}{(string.IsNullOrWhiteSpace(request.Neighborhood) ? string.Empty : $" · {request.Neighborhood}")}",
                FormatEnum(request.Status),
                $"Submitted {FormatDate(request.SubmittedAt)} by {request.ContactPersonName}.",
                request.SubmittedAt,
                null,
                "/admin/shelter-requests",
                BuildChips("Pending application", request.City, request.Neighborhood),
                new Dictionary<string, string>
                {
                    ["City"] = request.City,
                    ["Neighborhood"] = request.Neighborhood ?? "-",
                    ["Submitted"] = FormatDate(request.SubmittedAt)
                })).ToList();
        }

        if (interpretation.Intent == NaturalLanguageSearchIntent.FindSheltersWithLowStock)
        {
            var lowStockResources = await context.ResourceStocks
                .AsNoTracking()
                .Include(resource => resource.Shelter)
                .Include(resource => resource.ResourceCategory)
                .Where(resource => resource.Quantity <= resource.LowStockThreshold)
                .ToListAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(interpretation.ResourceCategory))
            {
                lowStockResources = lowStockResources
                    .Where(resource => ContainsText(resource.ResourceCategory?.Name, interpretation.ResourceCategory))
                    .ToList();
            }

            return lowStockResources
                .GroupBy(resource => resource.Shelter)
                .Where(group => group.Key is not null)
                .Select(group => new NaturalLanguageSearchResultItem(
                    group.Key!.Id.ToString(),
                    "Shelter",
                    group.Key.Name,
                    $"{group.Key.City}{(string.IsNullOrWhiteSpace(group.Key.Neighborhood) ? string.Empty : $" · {group.Key.Neighborhood}")}",
                    "Low stock",
                    $"{group.Count()} low-stock resource{(group.Count() == 1 ? string.Empty : "s")}.",
                    null,
                    null,
                    "/admin/shelters",
                    BuildChips("Low stock", group.Key.City, group.Key.Neighborhood),
                    new Dictionary<string, string>
                    {
                        ["Low-stock resources"] = group.Count().ToString(),
                        ["City"] = group.Key.City
                    }))
                .OrderByDescending(item => int.Parse(item.Metadata["Low-stock resources"]))
                .Take(interpretation.Limit)
                .ToList();
        }

        var pendingRequests = await context.AdoptionRequests
            .AsNoTracking()
            .Include(request => request.Dog)
                .ThenInclude(dog => dog!.Shelter)
            .Where(request => request.Status == AdoptionRequestStatus.Pending && request.Dog != null && request.Dog.Shelter != null)
            .ToListAsync(cancellationToken);

        var pendingByShelter = pendingRequests
            .GroupBy(request => request.Dog!.Shelter!)
            .Select(group => new { Shelter = group.Key, Count = group.Count() })
            .OrderByDescending(row => row.Count)
            .Take(interpretation.Limit)
            .ToList();

        return pendingByShelter.Select(row => new NaturalLanguageSearchResultItem(
            row.Shelter.Id.ToString(),
            "Shelter",
            row.Shelter.Name,
            $"{row.Shelter.City}{(string.IsNullOrWhiteSpace(row.Shelter.Neighborhood) ? string.Empty : $" · {row.Shelter.Neighborhood}")}",
            "Pending requests",
            $"{row.Count} pending adoption request{(row.Count == 1 ? string.Empty : "s")}.",
            null,
            null,
            "/admin/shelters",
            BuildChips("Pending requests", row.Shelter.City, row.Shelter.Neighborhood),
            new Dictionary<string, string>
            {
                ["Pending requests"] = row.Count.ToString(),
                ["City"] = row.Shelter.City
            })).ToList();
    }

    private async Task<IReadOnlyList<NaturalLanguageSearchResultItem>> SearchReportsAsync(
        NaturalLanguageSearchInterpretation interpretation,
        CancellationToken cancellationToken,
        int? shelterId = null)
    {
        var query = context.ReportHistories
            .AsNoTracking()
            .Include(report => report.Shelter)
            .AsQueryable();

        if (shelterId.HasValue)
        {
            query = query.Where(report => report.ShelterId == shelterId.Value);
        }

        if (interpretation.DateRange?.From is DateTime from)
        {
            query = query.Where(report => report.GeneratedAt >= from);
        }

        if (interpretation.DateRange?.To is DateTime to)
        {
            query = query.Where(report => report.GeneratedAt <= to);
        }

        var reports = await query
            .OrderByDescending(report => report.GeneratedAt)
            .Take(interpretation.Limit)
            .ToListAsync(cancellationToken);

        return reports.Select(report => new NaturalLanguageSearchResultItem(
            report.Id.ToString(),
            "Report",
            FormatReportType(report.ReportType),
            report.Shelter?.Name ?? report.TriggeredBy,
            report.WasSuccessful ? "Successful" : "Failed",
            report.Subject ?? report.FileName ?? "Report metadata",
            report.GeneratedAt,
            report.SentAt,
            shelterId.HasValue ? "/shelter/dashboard" : "/admin/report-history",
            BuildChips(report.WasSuccessful ? "Successful" : "Failed", report.Shelter?.Name, FormatReportType(report.ReportType)),
            new Dictionary<string, string>
            {
                ["Generated"] = FormatDate(report.GeneratedAt),
                ["Triggered by"] = report.TriggeredBy,
                ["Shelter"] = report.Shelter?.Name ?? "-",
                ["File"] = report.FileName ?? "-"
            })).ToList();
    }

    private async Task<IReadOnlyList<NaturalLanguageSearchResultItem>> SearchActivityLogsAsync(
        NaturalLanguageSearchInterpretation interpretation,
        CancellationToken cancellationToken)
    {
        var query = context.AuditLogs.AsNoTracking().AsQueryable();

        if (interpretation.DateRange?.From is DateTime from)
        {
            query = query.Where(log => log.CreatedAt >= from);
        }

        if (interpretation.DateRange?.To is DateTime to)
        {
            query = query.Where(log => log.CreatedAt <= to);
        }

        var logs = await query
            .OrderByDescending(log => log.CreatedAt)
            .Take(interpretation.Limit)
            .ToListAsync(cancellationToken);

        return logs.Select(log => new NaturalLanguageSearchResultItem(
            log.Id.ToString(),
            "Audit log",
            log.Action,
            log.EntityName,
            log.Severity,
            log.Description,
            log.CreatedAt,
            null,
            "/admin/audit-logs",
            BuildChips(log.Severity, log.EventType, log.EntityName),
            new Dictionary<string, string>
            {
                ["Entity"] = log.EntityName,
                ["Action"] = log.Action,
                ["Created"] = FormatDate(log.CreatedAt),
                ["Correlation"] = log.CorrelationId ?? "-"
            })).ToList();
    }

    private static IQueryable<AdoptionRequest> ApplyCreatedDateRange(
        IQueryable<AdoptionRequest> query,
        NaturalLanguageSearchDateRange? dateRange)
    {
        if (dateRange?.From is DateTime from)
        {
            query = query.Where(request => request.CreatedAt >= from);
        }

        if (dateRange?.To is DateTime to)
        {
            query = query.Where(request => request.CreatedAt <= to);
        }

        return query;
    }

    private static IQueryable<AdoptionRequest> ApplyVisitDateRange(
        IQueryable<AdoptionRequest> query,
        NaturalLanguageSearchDateRange? dateRange)
    {
        if (dateRange?.From is DateTime from)
        {
            query = query.Where(request => request.PreferredVisitDateTime.HasValue &&
                request.PreferredVisitDateTime.Value >= from);
        }

        if (dateRange?.To is DateTime to)
        {
            query = query.Where(request => request.PreferredVisitDateTime.HasValue &&
                request.PreferredVisitDateTime.Value <= to);
        }

        return query;
    }

    private static IEnumerable<Dog> SortDogs(IEnumerable<Dog> dogs, NaturalLanguageSearchInterpretation interpretation)
    {
        if (string.Equals(interpretation.SortField, "Name", StringComparison.OrdinalIgnoreCase))
        {
            return interpretation.SortDirection == NaturalLanguageSearchSortDirection.Ascending
                ? dogs.OrderBy(dog => dog.Name)
                : dogs.OrderByDescending(dog => dog.Name);
        }

        if (string.Equals(interpretation.SortField, "DaysReserved", StringComparison.OrdinalIgnoreCase))
        {
            return interpretation.SortDirection == NaturalLanguageSearchSortDirection.Ascending
                ? dogs.OrderBy(dog => GetReservedSince(dog))
                : dogs.OrderByDescending(dog => GetReservedSince(dog));
        }

        return dogs.OrderBy(dog => dog.Name);
    }

    private static DateTime? GetReservedSince(Dog dog)
    {
        var historyDate = dog.StatusHistories
            .Where(history => history.NewStatus == DogStatus.Reserved)
            .OrderByDescending(history => history.ChangedAt)
            .Select(history => (DateTime?)history.ChangedAt)
            .FirstOrDefault();

        if (historyDate.HasValue)
        {
            return historyDate.Value;
        }

        return dog.AdoptionRequests
            .Where(request => request.Status == AdoptionRequestStatus.VisitConfirmed || request.VisitStatus == AdoptionVisitStatus.Confirmed)
            .OrderByDescending(request => request.VisitConfirmedAt ?? request.UpdatedAt)
            .Select(request => (DateTime?)(request.VisitConfirmedAt ?? request.UpdatedAt))
            .FirstOrDefault();
    }

    private NaturalLanguageSearchAiRequest BuildAiRequest(string query, string role = IdentitySeedData.AdminRole)
    {
        var allowedScopes = role == IdentitySeedData.ShelterRole
            ? new[]
            {
                NaturalLanguageSearchScope.AdoptionRequests.ToString(),
                NaturalLanguageSearchScope.Dogs.ToString(),
                NaturalLanguageSearchScope.Resources.ToString(),
                NaturalLanguageSearchScope.Reports.ToString()
            }
            : Enum.GetNames<NaturalLanguageSearchScope>();

        var allowedIntents = role == IdentitySeedData.ShelterRole
            ? Enum.GetValues<NaturalLanguageSearchIntent>()
                .Where(intent => intent != NaturalLanguageSearchIntent.FindPendingShelterApplications &&
                                 intent != NaturalLanguageSearchIntent.FindSheltersWithLowStock &&
                                 intent != NaturalLanguageSearchIntent.FindSheltersWithManyPendingRequests &&
                                 intent != NaturalLanguageSearchIntent.FindRecentActivity)
                .Select(intent => intent.ToString())
                .ToArray()
            : Enum.GetNames<NaturalLanguageSearchIntent>();

        return new NaturalLanguageSearchAiRequest(
            query,
            role,
            DateTime.UtcNow,
            allowedScopes,
            allowedIntents,
            Enum.GetNames<AdoptionRequestStatus>(),
            Enum.GetNames<AdoptionVisitStatus>(),
            Enum.GetNames<DogStatus>(),
            AllowedSortFields);
    }

    private static NaturalLanguageSearchResult BuildEmptyResult(string query, NaturalLanguageSearchInterpretation interpretation)
    {
        var message = interpretation.ClarificationQuestion ?? interpretation.Explanation;
        return new NaturalLanguageSearchResult(query, interpretation, [], message);
    }

    private static NaturalLanguageSearchInterpretation UnknownInterpretation(string message)
    {
        return new NaturalLanguageSearchInterpretation
        {
            Intent = NaturalLanguageSearchIntent.Unknown,
            Scope = NaturalLanguageSearchScope.Unknown,
            NeedsClarification = true,
            ClarificationQuestion = message,
            Explanation = "The query did not match a supported safe search pattern.",
            Limit = DefaultLimit
        };
    }

    private static NaturalLanguageSearchInterpretation Interpreted(
        NaturalLanguageSearchIntent intent,
        NaturalLanguageSearchScope scope,
        string explanation,
        int limit,
        NaturalLanguageSearchDateRange? dateRange = null,
        AdoptionRequestStatus? requestStatus = null,
        AdoptionVisitStatus? visitStatus = null,
        DogStatus? dogStatus = null,
        ShelterRegistrationRequestStatus? shelterApplicationStatus = null,
        string? city = null,
        string? resourceCategory = null,
        bool lowStockOnly = false,
        bool noRequestsOnly = false,
        int? olderThanDays = null)
    {
        return new NaturalLanguageSearchInterpretation
        {
            Intent = intent,
            Scope = scope,
            Confidence = 0.7,
            RequestStatus = requestStatus,
            VisitStatus = visitStatus,
            DogStatus = dogStatus,
            ShelterApplicationStatus = shelterApplicationStatus,
            City = city,
            ResourceCategory = resourceCategory,
            LowStockOnly = lowStockOnly,
            NoRequestsOnly = noRequestsOnly,
            OlderThanDays = olderThanDays,
            DateRange = dateRange,
            Limit = limit,
            Explanation = explanation
        };
    }

    private static NaturalLanguageSearchDateRange? DetectDateRange(string query, DateTime now)
    {
        var todayStart = now.Date;
        if (query.Contains("today"))
        {
            return new NaturalLanguageSearchDateRange(todayStart, todayStart.AddDays(1).AddTicks(-1), "Today");
        }

        if (query.Contains("yesterday"))
        {
            var start = todayStart.AddDays(-1);
            return new NaturalLanguageSearchDateRange(start, start.AddDays(1).AddTicks(-1), "Yesterday");
        }

        if (query.Contains("last week"))
        {
            return new NaturalLanguageSearchDateRange(todayStart.AddDays(-7), now, "Last 7 days");
        }

        if (query.Contains("this month"))
        {
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            return new NaturalLanguageSearchDateRange(start, start.AddMonths(1).AddTicks(-1), "This month");
        }

        if (query.Contains("last month"))
        {
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
            return new NaturalLanguageSearchDateRange(start, start.AddMonths(1).AddTicks(-1), "Last month");
        }

        if (query.Contains("this year"))
        {
            var start = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return new NaturalLanguageSearchDateRange(start, start.AddYears(1).AddTicks(-1), "This year");
        }

        return null;
    }

    private static int DetectLimit(string query)
    {
        var match = Regex.Match(query, @"\b(?:top|first|limit)\s+(?<limit>\d{1,3})\b", RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups["limit"].Value, out var limit))
        {
            return DefaultLimit;
        }

        return Math.Clamp(limit, 1, MaxLimit);
    }

    private static int? DetectOlderThanDays(string query)
    {
        var match = Regex.Match(query, @"(?:more than|over|older than)\s+(?<days>\d{1,3})\s+days?", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups["days"].Value, out var days)
            ? days
            : null;
    }

    private static bool TryDetectRequestStatus(string query, out AdoptionRequestStatus status)
    {
        if (ContainsAny(query, "accepted request", "accepted requests"))
        {
            status = AdoptionRequestStatus.Accepted;
            return true;
        }

        if (ContainsAny(query, "rejected request", "rejected requests"))
        {
            status = AdoptionRequestStatus.Rejected;
            return true;
        }

        if (ContainsAny(query, "cancelled request", "cancelled requests", "canceled request", "canceled requests"))
        {
            status = AdoptionRequestStatus.Cancelled;
            return true;
        }

        if (ContainsAny(query, "confirmed request", "confirmed requests", "visit confirmed request", "visit confirmed requests"))
        {
            status = AdoptionRequestStatus.VisitConfirmed;
            return true;
        }

        status = AdoptionRequestStatus.Pending;
        return false;
    }

    private static DogStatus? DetectDogStatus(string query)
    {
        if (ContainsAny(query, "available dog", "available dogs"))
        {
            return DogStatus.Available;
        }

        if (ContainsAny(query, "reserved dog", "reserved dogs"))
        {
            return DogStatus.Reserved;
        }

        if (ContainsAny(query, "adopted dog", "adopted dogs"))
        {
            return DogStatus.Adopted;
        }

        if (ContainsAny(query, "in treatment", "intreatment", "treatment dogs"))
        {
            return DogStatus.InTreatment;
        }

        return null;
    }

    private static string? DetectCity(string query)
    {
        if (query.Contains("cluj", StringComparison.OrdinalIgnoreCase))
        {
            return "Cluj";
        }

        if (query.Contains("bucharest", StringComparison.OrdinalIgnoreCase))
        {
            return "Bucharest";
        }

        return null;
    }

    private static string? DetectResourceCategory(string query)
    {
        if (query.Contains("food")) return "Food";
        if (query.Contains("medicine") || query.Contains("medical")) return "Medicine";
        if (query.Contains("blanket")) return "Blankets";
        if (query.Contains("clean")) return "Cleaning Supplies";
        if (query.Contains("accessor") || query.Contains("leash") || query.Contains("collar")) return "Accessories";
        return null;
    }

    private static bool IsAdminOnlyIntent(NaturalLanguageSearchIntent intent)
    {
        return intent is NaturalLanguageSearchIntent.FindPendingShelterApplications
            or NaturalLanguageSearchIntent.FindSheltersWithLowStock
            or NaturalLanguageSearchIntent.FindSheltersWithManyPendingRequests
            or NaturalLanguageSearchIntent.FindRecentActivity;
    }

    private static bool ContainsText(string? source, string value)
    {
        return !string.IsNullOrWhiteSpace(source) &&
            source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAll(string query, params string[] terms)
    {
        return terms.All(term => query.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string query, params string[] terms)
    {
        return terms.Any(term => query.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static List<string> BuildChips(params string?[] values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatEnum<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return Regex.Replace(value.ToString(), "([a-z])([A-Z])", "$1 $2");
    }

    private static string FormatDate(DateTime value)
    {
        return value.ToLocalTime().ToString("dd MMM yyyy HH:mm");
    }

    private static string FormatReportType(string reportType)
    {
        return reportType switch
        {
            ReportHistoryTypes.ShelterSummaryReport => "Shelter summary",
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
}
