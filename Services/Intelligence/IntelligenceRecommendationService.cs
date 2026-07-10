using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services.Intelligence;

public sealed class IntelligenceRecommendationService(IDbContextFactory<ApplicationDbContext> contextFactory) : IIntelligenceRecommendationService
{
    private static readonly string[] AllowedRoutePrefixes =
    [
        "/admin/", "/shelter/", "/adopter/", "/dogs/", "/my-adoption-requests", "/favorites", "/notifications"
    ];

    public async Task<IReadOnlyList<RecommendedActionDto>> ValidateActionsAsync(
        IReadOnlyList<RecommendedActionDto> actions,
        IntelligenceScope scope,
        CancellationToken cancellationToken = default)
    {
        var validated = new List<RecommendedActionDto>(actions.Count);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        foreach (var action in actions)
        {
            var unavailableReason = ValidateRoleAndRoute(action, scope);
            if (unavailableReason is null && !string.IsNullOrWhiteSpace(action.EntityType) && !string.IsNullOrWhiteSpace(action.EntityId))
            {
                unavailableReason = await ValidateEntityAsync(db, action, scope, cancellationToken);
            }

            validated.Add(action with
            {
                IsAvailable = unavailableReason is null,
                UnavailableReason = unavailableReason
            });
        }

        return validated;
    }

    private static string? ValidateRoleAndRoute(RecommendedActionDto action, IntelligenceScope scope)
    {
        if (!string.Equals(action.RequiredRole, scope.AudienceType.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return "This action is not available for the current role.";
        }

        if (!Uri.TryCreate(action.Route, UriKind.Relative, out _) || !AllowedRoutePrefixes.Any(prefix => action.Route.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return "The destination is not an approved PawConnect workflow.";
        }

        return null;
    }

    private static async Task<string?> ValidateEntityAsync(
        ApplicationDbContext db,
        RecommendedActionDto action,
        IntelligenceScope scope,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(action.EntityId, out var entityId))
        {
            return "The related record is no longer available.";
        }

        var exists = action.EntityType switch
        {
            "Dog" => await db.Dogs.AnyAsync(dog => dog.Id == entityId && (scope.AudienceType != IntelligenceAudienceType.Shelter || dog.ShelterId == scope.ShelterId), cancellationToken),
            "AdoptionRequest" => await db.AdoptionRequests.AnyAsync(request => request.Id == entityId &&
                (scope.AudienceType == IntelligenceAudienceType.Admin ||
                 (scope.AudienceType == IntelligenceAudienceType.Adopter && request.AdopterId == scope.UserId) ||
                 (scope.AudienceType == IntelligenceAudienceType.Shelter && request.Dog != null && request.Dog.ShelterId == scope.ShelterId)), cancellationToken),
            "VolunteerTask" => await db.VolunteerTasks.AnyAsync(task => task.Id == entityId && (scope.AudienceType != IntelligenceAudienceType.Shelter || task.ShelterId == scope.ShelterId), cancellationToken),
            "DogTransferRequest" => await db.DogTransferRequests.AnyAsync(transfer => transfer.Id == entityId &&
                (scope.AudienceType != IntelligenceAudienceType.Shelter || transfer.SourceShelterId == scope.ShelterId || transfer.DestinationShelterId == scope.ShelterId), cancellationToken),
            "SavedDogSearch" => await db.SavedDogSearches.AnyAsync(search => search.Id == entityId && search.AdopterUserId == scope.UserId, cancellationToken),
            _ => true
        };

        return exists ? null : "The related record is no longer available or is outside your access scope.";
    }
}

