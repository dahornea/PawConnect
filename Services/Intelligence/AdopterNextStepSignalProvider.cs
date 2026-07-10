using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services.Intelligence;

public sealed class AdopterNextStepSignalProvider(IDbContextFactory<ApplicationDbContext> contextFactory) : IIntelligenceSignalProvider
{
    public string ProviderKey => "AdopterNextSteps";

    public async Task<IReadOnlyCollection<IntelligenceSignal>> CollectSignalsAsync(IntelligenceContext context, CancellationToken cancellationToken)
    {
        if (context.AudienceType != IntelligenceAudienceType.Adopter || string.IsNullOrWhiteSpace(context.UserId))
        {
            return [];
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var searches = await db.SavedDogSearches
            .AsNoTracking()
            .Include(search => search.Matches)
            .ThenInclude(match => match.Dog)
            .Where(search => search.AdopterUserId == context.UserId)
            .ToListAsync(cancellationToken);
        var signals = new List<IntelligenceSignal>();

        foreach (var search in searches)
        {
            var newMatches = search.Matches.Count(match => match.Status == SavedSearchMatchStatus.New && match.Dog != null && (match.Dog.Status == DogStatus.Available || match.Dog.Status == DogStatus.Reserved));
            if (newMatches > 0)
            {
                signals.Add(new IntelligenceSignal(
                    $"SavedSearchNewMatches:{search.Id}", IntelligenceCategory.Matching, ProviderKey, "SavedDogSearch", search.Id.ToString(), search.Name,
                    context.UserId, search.ShelterId,
                    $"{newMatches} new dog match{(newMatches == 1 ? string.Empty : "es")} for {search.Name}",
                    "A saved search has new public-safe dog matches ready to review.",
                    "Reviewing new matches early can help the adopter notice relevant dogs before availability changes.",
                    "the new matches are reviewed or no longer match",
                    "At least one saved-search match has New status",
                    [$"New matches: {newMatches}", $"Alerts: {(search.AlertsEnabled ? "enabled" : "disabled")}", $"Last evaluated: {search.LastEvaluatedAtUtc:dd MMM yyyy HH:mm}"],
                    [new("New opportunity", Math.Min(42, 26 + newMatches * 4), $"{newMatches} new matches were found."), new("Time sensitivity", 18, "Dog availability may change."), new("Data confidence", 18, "Matches were generated from saved structured filters.")],
                    [new("view-matches", "Review new matches", "Open this saved search and inspect the matching dogs.", "Navigate", $"/adopter/saved-searches/{search.Id}", "Adopter", "SavedDogSearch", search.Id.ToString(), true)],
                    context.UtcNow));
            }
            else if (search.AlertsEnabled && !search.Matches.Any(match => match.Status is SavedSearchMatchStatus.New or SavedSearchMatchStatus.Seen))
            {
                signals.Add(new IntelligenceSignal(
                    $"SavedSearchNoMatches:{search.Id}", IntelligenceCategory.UserNextStep, ProviderKey, "SavedDogSearch", search.Id.ToString(), search.Name,
                    context.UserId, search.ShelterId,
                    $"No current matches for {search.Name}",
                    "Alerts are active, but this saved search has no current public dog matches.",
                    "The alert will continue checking automatically; the adopter can also broaden optional criteria.",
                    "a matching public dog is found or alerts are disabled",
                    "Alerts enabled with zero active matches",
                    ["Active matches: 0", "Alerts: enabled"],
                    [new("Helpful next step", 18, "The saved search has no current match."), new("Low urgency", 8, "Automatic alerts remain active.")],
                    [new("review-search", "Review search criteria", "Open the saved search to keep it or adjust optional filters.", "Navigate", $"/adopter/saved-searches/{search.Id}", "Adopter", "SavedDogSearch", search.Id.ToString(), true)],
                    context.UtcNow,
                    "Medium"));
            }
        }

        return signals;
    }
}
