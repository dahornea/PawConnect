using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ConversationService(IDbContextFactory<ApplicationDbContext> contextFactory) : IConversationService
{
    public async Task<ConversationDto> GetOrCreateForAdoptionRequestAsync(
        int adoptionRequestId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Current user could not be found.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var request = await LoadRequestForAccessAsync(context, adoptionRequestId, cancellationToken);
        EnsureCanAccessRequest(request, userId);

        var conversation = await context.Conversations
            .FirstOrDefaultAsync(c => c.AdoptionRequestId == adoptionRequestId, cancellationToken);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                AdoptionRequestId = adoptionRequestId,
                CreatedAt = DateTime.UtcNow
            };

            context.Conversations.Add(conversation);
            await context.SaveChangesAsync(cancellationToken);
        }

        return ToConversationDto(conversation, request!, userId);
    }

    public async Task<ConversationDto> GetByIdForUserAsync(
        int conversationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await context.Conversations
            .Include(c => c.AdoptionRequest)
            .ThenInclude(r => r!.Adopter)
            .Include(c => c.AdoptionRequest)
            .ThenInclude(r => r!.Dog)
            .ThenInclude(d => d!.Shelter)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        EnsureCanAccessRequest(conversation?.AdoptionRequest, userId);
        return ToConversationDto(conversation!, conversation!.AdoptionRequest!, userId);
    }

    public async Task<bool> CanAccessConversationAsync(
        int conversationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await context.Conversations
            .Include(c => c.AdoptionRequest)
            .ThenInclude(r => r!.Dog)
            .ThenInclude(d => d!.Shelter)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        return CanAccessRequest(conversation?.AdoptionRequest, userId);
    }

    internal static bool CanAccessRequest(AdoptionRequest? request, string userId)
    {
        if (request?.Dog?.Shelter is null || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return request.AdopterId == userId || request.Dog.Shelter.ApplicationUserId == userId;
    }

    internal static void EnsureCanAccessRequest(AdoptionRequest? request, string userId)
    {
        if (!CanAccessRequest(request, userId))
        {
            throw new InvalidOperationException("You cannot access messages for this adoption request.");
        }
    }

    internal static string GetAdopterDisplayName(AdoptionRequest request)
    {
        return DisplayName(request.Adopter?.FullName, request.Adopter?.Email, "Adopter");
    }

    internal static string GetShelterDisplayName(AdoptionRequest request)
    {
        return DisplayName(request.Dog?.Shelter?.Name, request.Dog?.Shelter?.Email, "Shelter");
    }

    private static async Task<AdoptionRequest?> LoadRequestForAccessAsync(
        ApplicationDbContext context,
        int adoptionRequestId,
        CancellationToken cancellationToken)
    {
        return await context.AdoptionRequests
            .Include(r => r.Adopter)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == adoptionRequestId, cancellationToken);
    }

    private static ConversationDto ToConversationDto(Conversation conversation, AdoptionRequest request, string userId)
    {
        var dogName = string.IsNullOrWhiteSpace(request.Dog?.Name) ? "the selected dog" : request.Dog.Name;
        var adopterName = GetAdopterDisplayName(request);
        var shelterName = GetShelterDisplayName(request);
        var otherParticipantName = request.AdopterId == userId ? shelterName : adopterName;

        return new ConversationDto(
            conversation.Id,
            request.Id,
            dogName,
            shelterName,
            adopterName,
            otherParticipantName,
            conversation.CreatedAt,
            conversation.LastMessageAt);
    }

    private static string DisplayName(string? primary, string? secondary, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        return string.IsNullOrWhiteSpace(secondary) ? fallback : secondary.Trim();
    }
}
