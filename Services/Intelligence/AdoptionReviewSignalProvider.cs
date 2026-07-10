using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services.Intelligence;

public sealed class AdoptionReviewSignalProvider(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IOptions<IntelligenceHubOptions> options) : IIntelligenceSignalProvider
{
    private readonly IntelligenceHubOptions settings = options.Value;

    public string ProviderKey => "AdoptionReview";

    public async Task<IReadOnlyCollection<IntelligenceSignal>> CollectSignalsAsync(IntelligenceContext context, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.AdoptionRequests.AsNoTracking().Include(request => request.Dog).ThenInclude(dog => dog!.Shelter).AsQueryable();

        if (context.AudienceType == IntelligenceAudienceType.Adopter)
        {
            query = query.Where(request => request.AdopterId == context.UserId);
        }
        else if (context.AudienceType == IntelligenceAudienceType.Shelter)
        {
            query = query.Where(request => request.Dog != null && request.Dog.ShelterId == context.ShelterId);
        }

        var requests = await query.ToListAsync(cancellationToken);
        var signals = new List<IntelligenceSignal>();

        if (context.AudienceType == IntelligenceAudienceType.Adopter)
        {
            foreach (var request in requests.Where(request => request.Status == AdoptionRequestStatus.VisitConfirmed))
            {
                signals.Add(new IntelligenceSignal(
                    $"AdopterVisitNextStep:{request.Id}", IntelligenceCategory.UserNextStep, ProviderKey, "AdoptionRequest",
                    request.Id.ToString(), request.Dog?.Name, context.UserId, request.Dog?.ShelterId,
                    $"Prepare for your visit with {request.Dog?.Name ?? "the dog"}",
                    "The shelter confirmed a visit for this adoption request.",
                    "Reviewing the confirmed time and shelter notes helps the visit go smoothly.",
                    "the visit or adoption request moves to another status",
                    "Visit status is confirmed",
                    [request.PreferredVisitDateTime.HasValue ? $"Visit: {request.PreferredVisitDateTime.Value:dd MMM yyyy HH:mm}" : "Visit confirmed", $"Request status: {request.Status}"],
                    [new("Immediate next step", 36, "A shelter visit is confirmed."), new("User impact", 24, "The adopter has a scheduled workflow action.")],
                    [new("view-request", "View adoption request", "Open the request and review the visit details.", "Navigate", "/my-adoption-requests", "Adopter", "AdoptionRequest", request.Id.ToString(), true)],
                    context.UtcNow));
            }

            return signals;
        }

        foreach (var request in requests.Where(request => request.Status == AdoptionRequestStatus.Pending))
        {
            var ageHours = Math.Max(0, (context.UtcNow - request.CreatedAt).TotalHours);
            if (ageHours < settings.ApplicationReviewWarningHours)
            {
                continue;
            }

            var critical = ageHours >= settings.ApplicationReviewCriticalHours;
            var route = context.AudienceType == IntelligenceAudienceType.Shelter ? "/shelter/adoption-requests" : "/admin/adoption-requests";
            signals.Add(new IntelligenceSignal(
                $"ApplicationReviewDelay:{request.Id}", IntelligenceCategory.ApplicationReview, ProviderKey, "AdoptionRequest",
                request.Id.ToString(), request.Dog?.Name, request.AdopterId, request.Dog?.ShelterId,
                $"Application for {request.Dog?.Name ?? "a dog"} is waiting for review",
                $"The application has remained pending for {Math.Floor(ageHours)} hours.",
                "Long review delays can slow adopter communication and leave dog availability decisions unresolved.",
                "the request is reviewed, confirmed, rejected, cancelled, or accepted",
                $"Pending longer than {settings.ApplicationReviewWarningHours} hours",
                [$"Pending for {Math.Floor(ageHours)} hours", $"Submitted: {request.CreatedAt:dd MMM yyyy HH:mm}", $"Dog: {request.Dog?.Name ?? "Unknown"}"],
                [new("Urgency", critical ? 42 : 30, $"Pending for {Math.Floor(ageHours)} hours."), new("Applicant impact", 26, "The adopter is waiting for a shelter response."), new("Workflow impact", 18, "The dog application queue remains unresolved.")],
                [new("review-application", "Review application", "Open the adoption request queue and review this application.", "Navigate", route, context.AudienceType.ToString(), "AdoptionRequest", request.Id.ToString(), true)],
                context.UtcNow));
        }

        foreach (var group in requests.Where(request => request.Status == AdoptionRequestStatus.Pending).GroupBy(request => request.DogId).Where(group => group.Count() >= 3))
        {
            var dog = group.First().Dog;
            signals.Add(new IntelligenceSignal(
                $"MultiplePendingApplications:{group.Key}", IntelligenceCategory.Workload, ProviderKey, "Dog",
                group.Key.ToString(), dog?.Name, null, dog?.ShelterId,
                $"{group.Count()} applications need coordination for {dog?.Name ?? "one dog"}",
                "Several pending applications are attached to the same dog.",
                "Reviewing them together helps the shelter communicate consistently and avoid conflicting next steps.",
                "fewer than three applications remain pending for this dog",
                "Three or more pending applications for one dog",
                [$"Pending applications: {group.Count()}", $"Dog: {dog?.Name ?? "Unknown"}"],
                [new("Queue size", Math.Min(35, 18 + group.Count() * 4), $"{group.Count()} applications are pending."), new("Coordination impact", 24, "All requests concern the same dog.")],
                [new("review-queue", "Review applications", "Open the adoption request queue for coordinated review.", "Navigate", context.AudienceType == IntelligenceAudienceType.Shelter ? "/shelter/adoption-requests" : "/admin/adoption-requests", context.AudienceType.ToString(), "Dog", group.Key.ToString(), true)],
                context.UtcNow));
        }

        return signals;
    }
}
