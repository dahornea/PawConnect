using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class SavedDogSearchService(
    ApplicationDbContext context,
    IDistanceService distanceService,
    INotificationService? notificationService = null,
    ILogger<SavedDogSearchService>? logger = null) : ISavedDogSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int DefaultMatchScore = 55;

    public async Task<IReadOnlyList<SavedDogSearchDto>> GetSavedSearchesForAdopterAsync(
        string adopterUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdopterAsync(adopterUserId, cancellationToken);

        var searches = await context.SavedDogSearches
            .Include(search => search.Shelter)
            .Include(search => search.Matches)
            .Where(search => search.AdopterUserId == adopterUserId)
            .OrderByDescending(search => search.UpdatedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return searches.Select(ToDto).ToList();
    }

    public async Task<SavedDogSearchDetailsDto?> GetSavedSearchDetailsAsync(
        int savedSearchId,
        string adopterUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdopterAsync(adopterUserId, cancellationToken);

        var search = await context.SavedDogSearches
            .Include(savedSearch => savedSearch.Shelter)
            .Include(savedSearch => savedSearch.Matches)
                .ThenInclude(match => match.Dog)
                    .ThenInclude(dog => dog!.Shelter)
            .Include(savedSearch => savedSearch.Matches)
                .ThenInclude(match => match.Dog)
                    .ThenInclude(dog => dog!.DogBreed)
            .Include(savedSearch => savedSearch.Matches)
                .ThenInclude(match => match.Dog)
                    .ThenInclude(dog => dog!.SecondaryBreed)
            .Include(savedSearch => savedSearch.Matches)
                .ThenInclude(match => match.Dog)
                    .ThenInclude(dog => dog!.Images)
            .AsNoTracking()
            .FirstOrDefaultAsync(search => search.Id == savedSearchId && search.AdopterUserId == adopterUserId, cancellationToken);

        return search is null
            ? null
            : new SavedDogSearchDetailsDto(ToDto(search), ToMatchDtos(search.Matches).ToList());
    }

    public async Task<SavedSearchStatsDto> GetStatsForAdopterAsync(
        string adopterUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdopterAsync(adopterUserId, cancellationToken);

        var searches = await context.SavedDogSearches
            .Include(search => search.Matches)
            .Where(search => search.AdopterUserId == adopterUserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new SavedSearchStatsDto(
            searches.Count,
            searches.Count(search => search.AlertsEnabled),
            searches.Sum(search => search.Matches.Count(match => match.Status == SavedSearchMatchStatus.New)),
            searches.Max(search => search.LastMatchAtUtc));
    }

    public async Task<SavedDogSearchDto> CreateSavedSearchAsync(
        string adopterUserId,
        SavedDogSearchCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdopterAsync(adopterUserId, cancellationToken);
        var normalizedName = NormalizeName(request.Name);
        var criteria = NormalizeCriteria(request.Criteria);
        ValidateCriteria(criteria);
        await EnsureUniqueNameAsync(adopterUserId, normalizedName, null, cancellationToken);

        var now = DateTime.UtcNow;
        var search = new SavedDogSearch
        {
            AdopterUserId = adopterUserId,
            Name = normalizedName,
            AlertsEnabled = request.AlertsEnabled && request.AlertFrequency != SavedSearchAlertFrequency.Disabled,
            AlertFrequency = request.AlertFrequency,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        ApplyCriteria(search, criteria);
        context.SavedDogSearches.Add(search);
        await context.SaveChangesAsync(cancellationToken);

        await EvaluateSearchAsync(search.Id, cancellationToken);

        var saved = await context.SavedDogSearches
            .Include(item => item.Shelter)
            .Include(item => item.Matches)
            .AsNoTracking()
            .FirstAsync(item => item.Id == search.Id, cancellationToken);

        return ToDto(saved);
    }

    public async Task<SavedDogSearchDto> UpdateSavedSearchAsync(
        int savedSearchId,
        string adopterUserId,
        SavedDogSearchUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdopterAsync(adopterUserId, cancellationToken);
        var search = await GetOwnedSearchForUpdateAsync(savedSearchId, adopterUserId, cancellationToken);
        var normalizedName = NormalizeName(request.Name);
        var criteria = NormalizeCriteria(request.Criteria);
        ValidateCriteria(criteria);
        await EnsureUniqueNameAsync(adopterUserId, normalizedName, savedSearchId, cancellationToken);

        search.Name = normalizedName;
        search.AlertsEnabled = request.AlertsEnabled && request.AlertFrequency != SavedSearchAlertFrequency.Disabled;
        search.AlertFrequency = request.AlertFrequency;
        search.UpdatedAtUtc = DateTime.UtcNow;
        ApplyCriteria(search, criteria);

        await context.SaveChangesAsync(cancellationToken);
        await EvaluateSearchAsync(search.Id, cancellationToken);

        var saved = await context.SavedDogSearches
            .Include(item => item.Shelter)
            .Include(item => item.Matches)
            .AsNoTracking()
            .FirstAsync(item => item.Id == search.Id, cancellationToken);

        return ToDto(saved);
    }

    public async Task DeleteSavedSearchAsync(int savedSearchId, string adopterUserId, CancellationToken cancellationToken = default)
    {
        await EnsureAdopterAsync(adopterUserId, cancellationToken);
        var search = await GetOwnedSearchForUpdateAsync(savedSearchId, adopterUserId, cancellationToken);
        context.SavedDogSearches.Remove(search);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<SavedDogSearchDetailsDto?> EvaluateSavedSearchAsync(
        int savedSearchId,
        string adopterUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdopterAsync(adopterUserId, cancellationToken);
        var exists = await context.SavedDogSearches
            .AnyAsync(search => search.Id == savedSearchId && search.AdopterUserId == adopterUserId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        await EvaluateSearchAsync(savedSearchId, cancellationToken);
        return await GetSavedSearchDetailsAsync(savedSearchId, adopterUserId, cancellationToken);
    }

    public async Task EvaluateDogAgainstActiveSavedSearchesAsync(int dogId, CancellationToken cancellationToken = default)
    {
        var dogExists = await context.Dogs.AnyAsync(dog => dog.Id == dogId, cancellationToken);
        if (!dogExists)
        {
            return;
        }

        var searchIds = await context.SavedDogSearches
            .Where(search => search.AlertsEnabled && search.AlertFrequency == SavedSearchAlertFrequency.Immediate)
            .Select(search => search.Id)
            .ToListAsync(cancellationToken);

        foreach (var searchId in searchIds)
        {
            await EvaluateSearchAsync(searchId, cancellationToken);
        }
    }

    public async Task SetAlertsAsync(int savedSearchId, string adopterUserId, bool enabled, CancellationToken cancellationToken = default)
    {
        await EnsureAdopterAsync(adopterUserId, cancellationToken);
        var search = await GetOwnedSearchForUpdateAsync(savedSearchId, adopterUserId, cancellationToken);
        search.AlertsEnabled = enabled;
        search.AlertFrequency = enabled ? SavedSearchAlertFrequency.Immediate : SavedSearchAlertFrequency.Disabled;
        search.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkMatchAsSeenAsync(int matchId, string adopterUserId, CancellationToken cancellationToken = default)
    {
        await EnsureAdopterAsync(adopterUserId, cancellationToken);
        var match = await GetOwnedMatchForUpdateAsync(matchId, adopterUserId, cancellationToken);
        if (match.Status != SavedSearchMatchStatus.New)
        {
            return;
        }

        match.Status = SavedSearchMatchStatus.Seen;
        match.SeenAtUtc = DateTime.UtcNow;
        match.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DismissMatchAsync(int matchId, string adopterUserId, CancellationToken cancellationToken = default)
    {
        await EnsureAdopterAsync(adopterUserId, cancellationToken);
        var match = await GetOwnedMatchForUpdateAsync(matchId, adopterUserId, cancellationToken);
        match.Status = SavedSearchMatchStatus.Dismissed;
        match.DismissedAtUtc = DateTime.UtcNow;
        match.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task EvaluateSearchAsync(int savedSearchId, CancellationToken cancellationToken)
    {
        var search = await context.SavedDogSearches
            .Include(item => item.Matches)
            .FirstOrDefaultAsync(item => item.Id == savedSearchId, cancellationToken);
        if (search is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var matchedDogs = await GetMatchingDogsAsync(search, cancellationToken);
        var matchedDogIds = matchedDogs.Select(result => result.Dog.Id).ToHashSet();
        var existingByDogId = search.Matches.ToDictionary(match => match.DogId);
        var newNotificationMatches = new List<SavedSearchMatch>();

        foreach (var result in matchedDogs)
        {
            if (existingByDogId.TryGetValue(result.Dog.Id, out var existingMatch))
            {
                existingMatch.MatchScore = result.Score;
                existingMatch.MatchReasonsJson = SerializeReasons(result.Reasons);
                existingMatch.LastMatchedAtUtc = now;
                existingMatch.UpdatedAtUtc = now;
                if (existingMatch.Status == SavedSearchMatchStatus.NoLongerMatching)
                {
                    existingMatch.Status = SavedSearchMatchStatus.New;
                    existingMatch.SeenAtUtc = null;
                }

                continue;
            }

            var match = new SavedSearchMatch
            {
                SavedDogSearchId = search.Id,
                DogId = result.Dog.Id,
                MatchScore = result.Score,
                MatchReasonsJson = SerializeReasons(result.Reasons),
                Status = SavedSearchMatchStatus.New,
                FirstMatchedAtUtc = now,
                LastMatchedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            if (search.AlertsEnabled && search.AlertFrequency == SavedSearchAlertFrequency.Immediate)
            {
                match.NotificationSentAtUtc = now;
                newNotificationMatches.Add(match);
            }

            context.SavedSearchMatches.Add(match);
        }

        foreach (var match in search.Matches.Where(match => !matchedDogIds.Contains(match.DogId)))
        {
            if (match.Status is SavedSearchMatchStatus.New or SavedSearchMatchStatus.Seen)
            {
                match.Status = SavedSearchMatchStatus.NoLongerMatching;
                match.UpdatedAtUtc = now;
            }
        }

        search.LastEvaluatedAtUtc = now;
        search.LastMatchAtUtc = matchedDogs.Count > 0 ? now : search.LastMatchAtUtc;
        search.UpdatedAtUtc = now;

        await context.SaveChangesAsync(cancellationToken);
        await SendMatchNotificationsAsync(search, matchedDogs, newNotificationMatches, cancellationToken);
    }

    private async Task<List<SearchMatchCandidate>> GetMatchingDogsAsync(SavedDogSearch search, CancellationToken cancellationToken)
    {
        var query = context.Dogs
            .Include(dog => dog.Shelter)
            .Include(dog => dog.DogBreed)
            .Include(dog => dog.SecondaryBreed)
            .Include(dog => dog.Images)
            .Where(dog => dog.Status == DogStatus.Available || dog.Status == DogStatus.Reserved)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search.SearchText))
        {
            var normalizedSearch = search.SearchText.Trim().ToUpper();
            query = query.Where(dog =>
                dog.Name.ToUpper().Contains(normalizedSearch) ||
                dog.Breed.ToUpper().Contains(normalizedSearch) ||
                (dog.Description != null && dog.Description.ToUpper().Contains(normalizedSearch)) ||
                (dog.BehaviorDescription != null && dog.BehaviorDescription.ToUpper().Contains(normalizedSearch)) ||
                (dog.Shelter != null && dog.Shelter.Name.ToUpper().Contains(normalizedSearch)) ||
                (dog.Shelter != null && dog.Shelter.Neighborhood != null && dog.Shelter.Neighborhood.ToUpper().Contains(normalizedSearch)));
        }

        if (search.ShelterId.HasValue)
        {
            query = query.Where(dog => dog.ShelterId == search.ShelterId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search.Breed))
        {
            var normalizedBreed = search.Breed.Trim().ToUpper();
            query = query.Where(dog =>
                dog.Breed.ToUpper().Contains(normalizedBreed) ||
                (dog.CustomBreedName != null && dog.CustomBreedName.ToUpper().Contains(normalizedBreed)) ||
                (dog.DogBreed != null && dog.DogBreed.Name.ToUpper().Contains(normalizedBreed)) ||
                (dog.SecondaryBreed != null && dog.SecondaryBreed.Name.ToUpper().Contains(normalizedBreed)));
        }

        if (!string.IsNullOrWhiteSpace(search.CoatColor))
        {
            var normalizedCoatColor = search.CoatColor.Trim().ToUpper();
            query = query.Where(dog => dog.CoatColor != null && dog.CoatColor.ToUpper() == normalizedCoatColor);
        }

        if (search.MaxAgeYears.HasValue)
        {
            query = query.Where(dog => dog.AgeYears <= search.MaxAgeYears.Value);
        }

        if (search.Size.HasValue)
        {
            query = query.Where(dog => dog.Size == search.Size.Value);
        }

        if (!string.IsNullOrWhiteSpace(search.Location))
        {
            query = query.Where(dog => dog.Location == search.Location);
        }

        if (!string.IsNullOrWhiteSpace(search.Neighborhood))
        {
            var normalizedNeighborhood = search.Neighborhood.Trim().ToUpper();
            query = query.Where(dog => dog.Shelter != null && dog.Shelter.Neighborhood != null && dog.Shelter.Neighborhood.ToUpper() == normalizedNeighborhood);
        }

        if (search.Status.HasValue)
        {
            query = query.Where(dog => dog.Status == search.Status.Value);
        }

        if (search.CatCompatibility is { } catCompatibility and not CatCompatibility.Unknown)
        {
            query = query.Where(dog => dog.CatCompatibility == catCompatibility);
        }

        if (search.ChildrenCompatibility is { } childrenCompatibility and not ChildrenCompatibility.Unknown)
        {
            query = query.Where(dog => dog.ChildrenCompatibility == childrenCompatibility);
        }

        if (search.ActivityLevel is { } activityLevel and not DogActivityLevel.Unknown)
        {
            query = query.Where(dog => dog.ActivityLevel == activityLevel);
        }

        if (search.ApartmentSuitability is { } apartmentSuitability and not ApartmentSuitability.Unknown)
        {
            query = query.Where(dog => dog.ApartmentSuitability == apartmentSuitability);
        }

        var dogs = await ApplySort(query, search.SortOption)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var candidates = new List<SearchMatchCandidate>();
        foreach (var dog in dogs)
        {
            if (search.NearbyLatitude.HasValue && search.NearbyLongitude.HasValue && search.RadiusKm.HasValue)
            {
                if (dog.Shelter?.Latitude is null || dog.Shelter.Longitude is null)
                {
                    continue;
                }

                var distanceKm = distanceService.CalculateDistanceKm(
                    search.NearbyLatitude.Value,
                    search.NearbyLongitude.Value,
                    dog.Shelter.Latitude.Value,
                    dog.Shelter.Longitude.Value);
                if (distanceKm > search.RadiusKm.Value)
                {
                    continue;
                }
            }

            candidates.Add(BuildCandidate(search, dog));
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Dog.Name)
            .ToList();
    }

    private static IQueryable<Dog> ApplySort(IQueryable<Dog> query, DogSortOption sortOption)
    {
        return sortOption switch
        {
            DogSortOption.NameDesc => query.OrderByDescending(dog => dog.Name),
            DogSortOption.AgeAsc => query.OrderBy(dog => dog.AgeYears).ThenBy(dog => dog.AgeMonths).ThenBy(dog => dog.Name),
            DogSortOption.AgeDesc => query.OrderByDescending(dog => dog.AgeYears).ThenByDescending(dog => dog.AgeMonths).ThenBy(dog => dog.Name),
            DogSortOption.BreedAsc => query.OrderBy(dog => dog.DogBreed != null ? dog.DogBreed.Name : dog.CustomBreedName ?? dog.Breed).ThenBy(dog => dog.Name),
            DogSortOption.LocationAsc => query.OrderBy(dog => dog.Location).ThenBy(dog => dog.Name),
            DogSortOption.Status => query.OrderBy(dog => dog.Status).ThenBy(dog => dog.Name),
            DogSortOption.NewestFirst => query.OrderByDescending(dog => dog.Id),
            _ => query.OrderBy(dog => dog.Name)
        };
    }

    private static SearchMatchCandidate BuildCandidate(SavedDogSearch search, Dog dog)
    {
        var reasons = new List<string>();
        var score = DefaultMatchScore;

        AddReasonIf(reasons, !string.IsNullOrWhiteSpace(search.SearchText), $"Matches search text '{search.SearchText}'", ref score, 5);
        AddReasonIf(reasons, search.ShelterId.HasValue, $"Shelter: {dog.Shelter?.Name ?? "selected shelter"}", ref score, 8);
        AddReasonIf(reasons, !string.IsNullOrWhiteSpace(search.Breed), $"Breed: {DogBreedFormatter.Format(dog)}", ref score, 10);
        AddReasonIf(reasons, !string.IsNullOrWhiteSpace(search.CoatColor), $"Coat color: {dog.CoatColor}", ref score, 8);
        AddReasonIf(reasons, search.MaxAgeYears.HasValue, $"Age: {DogAgeFormatter.Format(dog)}", ref score, 4);
        AddReasonIf(reasons, search.Size.HasValue, $"Size: {dog.Size}", ref score, 8);
        AddReasonIf(reasons, !string.IsNullOrWhiteSpace(search.Location), $"Location: {dog.Location}", ref score, 6);
        AddReasonIf(reasons, !string.IsNullOrWhiteSpace(search.Neighborhood), $"Neighborhood: {dog.Shelter?.Neighborhood}", ref score, 6);
        AddReasonIf(reasons, search.Status.HasValue, $"Status: {dog.Status}", ref score, 3);
        AddReasonIf(reasons, search.CatCompatibility is { } cat and not CatCompatibility.Unknown, $"Cats: {DogCompatibilityFormatter.FormatCat(dog.CatCompatibility)}", ref score, 7);
        AddReasonIf(reasons, search.ChildrenCompatibility is { } children and not ChildrenCompatibility.Unknown, $"Children: {DogCompatibilityFormatter.FormatChildren(dog.ChildrenCompatibility)}", ref score, 7);
        AddReasonIf(reasons, search.ActivityLevel is { } activity and not DogActivityLevel.Unknown, $"Activity: {DogCompatibilityFormatter.FormatActivity(dog.ActivityLevel)}", ref score, 7);
        AddReasonIf(reasons, search.ApartmentSuitability is { } apartment and not ApartmentSuitability.Unknown, $"Apartment: {DogCompatibilityFormatter.FormatApartment(dog.ApartmentSuitability)}", ref score, 7);
        AddReasonIf(reasons, search.NearbyLatitude.HasValue, $"Within {search.RadiusKm} km of {search.NearbyLabel ?? "selected location"}", ref score, 8);

        if (reasons.Count == 0)
        {
            reasons.Add("Currently public and available for adoption review.");
        }

        return new SearchMatchCandidate(dog, Math.Clamp(score, 45, 98), reasons);
    }

    private static void AddReasonIf(List<string> reasons, bool condition, string reason, ref int score, int weight)
    {
        if (!condition)
        {
            return;
        }

        reasons.Add(reason);
        score += weight;
    }

    private async Task SendMatchNotificationsAsync(
        SavedDogSearch search,
        IReadOnlyList<SearchMatchCandidate> candidates,
        IReadOnlyList<SavedSearchMatch> newMatches,
        CancellationToken cancellationToken)
    {
        if (notificationService is null || newMatches.Count == 0)
        {
            return;
        }

        var dogNamesById = candidates.ToDictionary(candidate => candidate.Dog.Id, candidate => candidate.Dog.Name);
        foreach (var match in newMatches)
        {
            var dogName = dogNamesById.TryGetValue(match.DogId, out var candidateDogName)
                ? candidateDogName
                : "A dog";

            try
            {
                await notificationService.CreateNotificationAsync(
                    search.AdopterUserId,
                    "New dog match for saved search",
                    $"{dogName} matches your saved search '{search.Name}'.",
                    NotificationCategory.SavedSearch,
                    NotificationType.Info,
                    $"/adopter/saved-searches/{search.Id}",
                    nameof(SavedDogSearch),
                    search.Id.ToString(),
                    TimeSpan.FromMinutes(10));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger?.LogWarning(ex, "Could not create saved search notification for search {SavedSearchId} and dog {DogId}.", search.Id, match.DogId);
            }
        }
    }

    private static SavedDogSearchDto ToDto(SavedDogSearch search)
    {
        var activeMatches = search.Matches.Count(match => match.Status is SavedSearchMatchStatus.New or SavedSearchMatchStatus.Seen);
        var newMatches = search.Matches.Count(match => match.Status == SavedSearchMatchStatus.New);
        return new SavedDogSearchDto(
            search.Id,
            search.Name,
            search.AlertsEnabled,
            search.AlertFrequency,
            search.CreatedAtUtc,
            search.UpdatedAtUtc,
            search.LastEvaluatedAtUtc,
            search.LastMatchAtUtc,
            BuildCriteriaLabels(search),
            activeMatches,
            newMatches);
    }

    private static IEnumerable<SavedSearchMatchDto> ToMatchDtos(IEnumerable<SavedSearchMatch> matches)
    {
        return matches
            .Where(match => match.Dog is not null && match.Status is SavedSearchMatchStatus.New or SavedSearchMatchStatus.Seen)
            .OrderByDescending(match => match.Status == SavedSearchMatchStatus.New)
            .ThenByDescending(match => match.MatchScore)
            .ThenByDescending(match => match.LastMatchedAtUtc)
            .Select(match => new SavedSearchMatchDto(
                match.Id,
                match.DogId,
                match.Dog!.Name,
                DogBreedFormatter.Format(match.Dog),
                DogAgeFormatter.Format(match.Dog),
                match.Dog.Size,
                match.Dog.Location,
                match.Dog.Status,
                match.Dog.Shelter?.Name,
                match.Dog.Shelter?.Neighborhood,
                DogImageUrlValidator.GetPrimaryRealDogImageUrl(match.Dog.Images),
                match.MatchScore,
                match.Status,
                DeserializeReasons(match.MatchReasonsJson),
                match.FirstMatchedAtUtc,
                match.LastMatchedAtUtc));
    }

    private static IReadOnlyList<string> BuildCriteriaLabels(SavedDogSearch search)
    {
        var labels = new List<string>();
        if (!string.IsNullOrWhiteSpace(search.SearchText)) labels.Add($"Search: {search.SearchText}");
        if (search.ShelterId.HasValue) labels.Add($"Shelter: {search.Shelter?.Name ?? "selected shelter"}");
        if (!string.IsNullOrWhiteSpace(search.Breed)) labels.Add($"Breed: {search.Breed}");
        if (!string.IsNullOrWhiteSpace(search.CoatColor)) labels.Add($"Coat color: {search.CoatColor}");
        if (search.MaxAgeYears.HasValue) labels.Add($"Max age: {search.MaxAgeYears}");
        if (search.Size.HasValue) labels.Add($"Size: {search.Size}");
        if (!string.IsNullOrWhiteSpace(search.Location)) labels.Add($"Location: {search.Location}");
        if (!string.IsNullOrWhiteSpace(search.Neighborhood)) labels.Add($"Neighborhood: {search.Neighborhood}");
        if (search.Status.HasValue) labels.Add($"Status: {search.Status}");
        if (search.CatCompatibility is { } cat and not CatCompatibility.Unknown) labels.Add($"Cats: {DogCompatibilityFormatter.FormatCat(cat)}");
        if (search.ChildrenCompatibility is { } children and not ChildrenCompatibility.Unknown) labels.Add($"Children: {DogCompatibilityFormatter.FormatChildren(children)}");
        if (search.ActivityLevel is { } activity and not DogActivityLevel.Unknown) labels.Add($"Activity: {DogCompatibilityFormatter.FormatActivity(activity)}");
        if (search.ApartmentSuitability is { } apartment and not ApartmentSuitability.Unknown) labels.Add($"Apartment: {DogCompatibilityFormatter.FormatApartment(apartment)}");
        if (search.NearbyLatitude.HasValue) labels.Add($"Near: {search.NearbyLabel ?? "selected location"}, {search.RadiusKm ?? 25} km");
        return labels;
    }

    private static void ApplyCriteria(SavedDogSearch search, SavedDogSearchCriteriaDto criteria)
    {
        search.SearchText = NormalizeOptional(criteria.SearchText, 200);
        search.ShelterId = criteria.ShelterId;
        search.Breed = NormalizeOptional(criteria.Breed, 120);
        search.CoatColor = DogCoatColorOptions.Normalize(criteria.CoatColor);
        search.MaxAgeYears = criteria.MaxAgeYears;
        search.Size = criteria.Size;
        search.Location = NormalizeOptional(criteria.Location, 120);
        search.Neighborhood = NormalizeOptional(criteria.Neighborhood, 120);
        search.Status = criteria.Status is DogStatus.Available or DogStatus.Reserved ? criteria.Status : null;
        search.CatCompatibility = criteria.CatCompatibility is CatCompatibility.Unknown ? null : criteria.CatCompatibility;
        search.ChildrenCompatibility = criteria.ChildrenCompatibility is ChildrenCompatibility.Unknown ? null : criteria.ChildrenCompatibility;
        search.ActivityLevel = criteria.ActivityLevel is DogActivityLevel.Unknown ? null : criteria.ActivityLevel;
        search.ApartmentSuitability = criteria.ApartmentSuitability is ApartmentSuitability.Unknown ? null : criteria.ApartmentSuitability;
        search.SortOption = criteria.SortOption;
        search.NearbyLabel = NormalizeOptional(criteria.NearbyLabel, 250);
        search.NearbyLatitude = criteria.NearbyLatitude;
        search.NearbyLongitude = criteria.NearbyLongitude;
        search.RadiusKm = criteria.RadiusKm;
        search.CriteriaJson = JsonSerializer.Serialize(criteria, JsonOptions);
    }

    private static SavedDogSearchCriteriaDto NormalizeCriteria(SavedDogSearchCriteriaDto criteria)
    {
        var radius = criteria.RadiusKm is > 0 and <= 250 ? criteria.RadiusKm : null;
        var hasCoordinates = criteria.NearbyLatitude.HasValue && criteria.NearbyLongitude.HasValue;
        return criteria with
        {
            SearchText = NormalizeOptional(criteria.SearchText, 200),
            Breed = NormalizeOptional(criteria.Breed, 120),
            CoatColor = DogCoatColorOptions.Normalize(criteria.CoatColor),
            Location = NormalizeOptional(criteria.Location, 120),
            Neighborhood = NormalizeOptional(criteria.Neighborhood, 120),
            Status = criteria.Status is DogStatus.Available or DogStatus.Reserved ? criteria.Status : null,
            CatCompatibility = criteria.CatCompatibility is CatCompatibility.Unknown ? null : criteria.CatCompatibility,
            ChildrenCompatibility = criteria.ChildrenCompatibility is ChildrenCompatibility.Unknown ? null : criteria.ChildrenCompatibility,
            ActivityLevel = criteria.ActivityLevel is DogActivityLevel.Unknown ? null : criteria.ActivityLevel,
            ApartmentSuitability = criteria.ApartmentSuitability is ApartmentSuitability.Unknown ? null : criteria.ApartmentSuitability,
            NearbyLabel = hasCoordinates ? NormalizeOptional(criteria.NearbyLabel, 250) : null,
            NearbyLatitude = hasCoordinates ? criteria.NearbyLatitude : null,
            NearbyLongitude = hasCoordinates ? criteria.NearbyLongitude : null,
            RadiusKm = hasCoordinates ? radius ?? 25 : null
        };
    }

    private static void ValidateCriteria(SavedDogSearchCriteriaDto criteria)
    {
        var hasCriteria = !string.IsNullOrWhiteSpace(criteria.SearchText)
            || criteria.ShelterId.HasValue
            || !string.IsNullOrWhiteSpace(criteria.Breed)
            || !string.IsNullOrWhiteSpace(criteria.CoatColor)
            || criteria.MaxAgeYears.HasValue
            || criteria.Size.HasValue
            || !string.IsNullOrWhiteSpace(criteria.Location)
            || !string.IsNullOrWhiteSpace(criteria.Neighborhood)
            || criteria.Status.HasValue
            || criteria.CatCompatibility.HasValue
            || criteria.ChildrenCompatibility.HasValue
            || criteria.ActivityLevel.HasValue
            || criteria.ApartmentSuitability.HasValue
            || criteria.NearbyLatitude.HasValue;

        if (!hasCriteria)
        {
            throw new InvalidOperationException("Choose at least one search filter before saving this search.");
        }

        if (criteria.MaxAgeYears is < 0 or > 30)
        {
            throw new InvalidOperationException("Maximum age must be between 0 and 30 years.");
        }
    }

    private async Task EnsureAdopterAsync(string adopterUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adopterUserId))
        {
            throw new InvalidOperationException("Current adopter account could not be found.");
        }

        var isAdopter = await context.UserRoles.AnyAsync(
            role => role.UserId == adopterUserId && role.RoleId == IdentitySeedData.AdopterRole,
            cancellationToken);
        if (!isAdopter)
        {
            throw new InvalidOperationException("Only adopter accounts can manage saved searches.");
        }
    }

    private async Task EnsureUniqueNameAsync(
        string adopterUserId,
        string normalizedName,
        int? existingSearchId,
        CancellationToken cancellationToken)
    {
        var duplicateExists = await context.SavedDogSearches.AnyAsync(search =>
            search.AdopterUserId == adopterUserId &&
            search.Name == normalizedName &&
            (!existingSearchId.HasValue || search.Id != existingSearchId.Value),
            cancellationToken);
        if (duplicateExists)
        {
            throw new InvalidOperationException("You already have a saved search with this name.");
        }
    }

    private async Task<SavedDogSearch> GetOwnedSearchForUpdateAsync(
        int savedSearchId,
        string adopterUserId,
        CancellationToken cancellationToken)
    {
        return await context.SavedDogSearches
            .Include(search => search.Matches)
            .FirstOrDefaultAsync(search => search.Id == savedSearchId && search.AdopterUserId == adopterUserId, cancellationToken)
            ?? throw new InvalidOperationException("Saved search was not found.");
    }

    private async Task<SavedSearchMatch> GetOwnedMatchForUpdateAsync(
        int matchId,
        string adopterUserId,
        CancellationToken cancellationToken)
    {
        return await context.SavedSearchMatches
            .Include(match => match.SavedDogSearch)
            .FirstOrDefaultAsync(match => match.Id == matchId && match.SavedDogSearch != null && match.SavedDogSearch.AdopterUserId == adopterUserId, cancellationToken)
            ?? throw new InvalidOperationException("Saved search match was not found.");
    }

    private static string NormalizeName(string name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Saved search name is required.");
        }

        return normalized.Length <= 120 ? normalized : normalized[..120];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string SerializeReasons(IReadOnlyList<string> reasons)
    {
        return JsonSerializer.Serialize(reasons, JsonOptions);
    }

    private static IReadOnlyList<string> DeserializeReasons(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed record SearchMatchCandidate(Dog Dog, int Score, IReadOnlyList<string> Reasons);
}
