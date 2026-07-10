using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services.Intelligence;

public sealed class DogOperationsSignalProvider(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IDogProfileCompletenessService completenessService,
    IOptions<IntelligenceHubOptions> options) : IIntelligenceSignalProvider
{
    private readonly IntelligenceHubOptions settings = options.Value;

    public string ProviderKey => "DogOperations";

    public async Task<IReadOnlyCollection<IntelligenceSignal>> CollectSignalsAsync(
        IntelligenceContext context,
        CancellationToken cancellationToken)
    {
        if (context.AudienceType == IntelligenceAudienceType.Adopter)
        {
            return [];
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Dogs
            .AsNoTracking()
            .Include(dog => dog.Shelter)
            .Include(dog => dog.DogBreed)
            .Include(dog => dog.SecondaryBreed)
            .Include(dog => dog.Images)
            .Include(dog => dog.PreferredFoodType)
            .Include(dog => dog.MedicalRecords)
            .Include(dog => dog.AdoptionRequests)
            .Include(dog => dog.StatusHistories)
            .AsQueryable();

        if (context.AudienceType == IntelligenceAudienceType.Shelter)
        {
            query = query.Where(dog => dog.ShelterId == context.ShelterId);
        }

        var dogs = await query.ToListAsync(cancellationToken);
        var signals = new List<IntelligenceSignal>();

        foreach (var dog in dogs)
        {
            var completeness = completenessService.CalculateForDog(dog);
            if (completeness.ScorePercent < settings.LowProfileCompletenessThreshold)
            {
                var critical = completeness.ScorePercent < settings.CriticalProfileCompletenessThreshold;
                var missing = completeness.MissingItems.Take(4).Select(item => item.Label).ToList();
                var route = context.AudienceType == IntelligenceAudienceType.Shelter
                    ? $"/shelter/dogs/edit/{dog.Id}"
                    : "/admin/dogs";
                signals.Add(new IntelligenceSignal(
                    $"DogProfileIncomplete:{dog.Id}",
                    IntelligenceCategory.DogProfileQuality,
                    ProviderKey,
                    "Dog",
                    dog.Id.ToString(),
                    dog.Name,
                    null,
                    dog.ShelterId,
                    $"{dog.Name}'s profile needs attention",
                    $"The public profile is {completeness.ScorePercent}% complete.",
                    "Missing photos, compatibility details, or care information can reduce adopter confidence and matching quality.",
                    $"the profile reaches at least {settings.LowProfileCompletenessThreshold}% completeness",
                    $"Profile completeness below {settings.LowProfileCompletenessThreshold}%",
                    [$"Completeness: {completeness.ScorePercent}%", .. missing.Select(item => $"Missing: {item}")],
                    [
                        new("Data gap", critical ? 38 : 28, $"Completeness is {completeness.ScorePercent}%"),
                        new("Public impact", 24, "The profile is used by public browsing and matching."),
                        new("Confidence", missing.Count >= 3 ? 14 : 8, $"{missing.Count} important gaps are visible.")
                    ],
                    [new("edit-dog", "Complete dog profile", "Open the dog editor and fill the missing public details.", "Navigate", route, context.AudienceType.ToString(), "Dog", dog.Id.ToString(), true)],
                    context.UtcNow));
            }

            if (dog.Status != DogStatus.Available)
            {
                continue;
            }

            var availableSince = dog.StatusHistories
                .Where(history => history.NewStatus == DogStatus.Available)
                .OrderByDescending(history => history.ChangedAt)
                .Select(history => (DateTime?)history.ChangedAt)
                .FirstOrDefault();
            if (!availableSince.HasValue)
            {
                continue;
            }

            var daysAvailable = (int)Math.Floor((context.UtcNow - availableSince.Value).TotalDays);
            var hasRecentApplication = dog.AdoptionRequests.Any(request =>
                request.CreatedAt >= availableSince.Value &&
                request.Status is AdoptionRequestStatus.Pending or AdoptionRequestStatus.VisitConfirmed or AdoptionRequestStatus.Accepted);
            if (daysAvailable < settings.DogNoApplicationWarningDays || hasRecentApplication)
            {
                continue;
            }

            var criticalDelay = daysAvailable >= settings.DogNoApplicationCriticalDays;
            var delayRoute = context.AudienceType == IntelligenceAudienceType.Shelter
                ? $"/shelter/dogs/edit/{dog.Id}"
                : "/admin/dogs";
            signals.Add(new IntelligenceSignal(
                $"DogAdoptionDelay:{dog.Id}",
                IntelligenceCategory.Adoption,
                ProviderKey,
                "Dog",
                dog.Id.ToString(),
                dog.Name,
                null,
                dog.ShelterId,
                $"{dog.Name} has received no active applications",
                $"{dog.Name} has been available for {daysAvailable} days without an active adoption request.",
                "A long period without applications can indicate that the profile, visibility, or adoption requirements need review.",
                "a new active application is received or the dog is no longer available",
                $"No active application after {settings.DogNoApplicationWarningDays} days",
                [$"Available for {daysAvailable} days", "Active applications: 0", $"Profile completeness: {completeness.ScorePercent}%"],
                [
                    new("Time sensitivity", criticalDelay ? 40 : 28, $"Available for {daysAvailable} days."),
                    new("Adoption impact", 30, "No active adoption request exists."),
                    new("Profile evidence", completeness.ScorePercent < 70 ? 16 : 6, $"Profile completeness is {completeness.ScorePercent}%.")
                ],
                [new("review-dog", "Review dog profile", "Review visibility, description, photos, and adoption requirements.", "Navigate", delayRoute, context.AudienceType.ToString(), "Dog", dog.Id.ToString(), true)],
                context.UtcNow));
        }

        return signals;
    }
}
