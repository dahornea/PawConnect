using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class CopilotFeedbackService(ApplicationDbContext context) : ICopilotFeedbackService
{
    public const int MaxCommentLength = 500;

    public async Task<CopilotFeedbackDto> SubmitFeedbackAsync(
        SubmitCopilotFeedbackRequest request,
        string adopterUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adopterUserId))
        {
            throw new InvalidOperationException("Current adopter account could not be found.");
        }

        if (!Enum.IsDefined(request.FeedbackType))
        {
            throw new InvalidOperationException("Select a valid Copilot feedback option.");
        }

        var comment = NormalizeComment(request.OptionalComment);
        var session = await context.CopilotSessions
            .FirstOrDefaultAsync(
                item => item.Id == request.SessionId && item.AdopterUserId == adopterUserId,
                cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException("Copilot session was not found for this adopter.");
        }

        var resultDogIds = CopilotHistoryService.DeserializeDogIds(session.ResultDogIdsJson);
        if (!resultDogIds.Contains(request.DogId))
        {
            throw new InvalidOperationException("Feedback can only be added for dogs returned in this Copilot session.");
        }

        var dogExists = await context.Dogs
            .AsNoTracking()
            .AnyAsync(dog => dog.Id == request.DogId, cancellationToken);

        if (!dogExists)
        {
            throw new InvalidOperationException("Dog was not found.");
        }

        var feedback = await context.CopilotResultFeedbacks
            .FirstOrDefaultAsync(
                item => item.CopilotSessionId == request.SessionId &&
                    item.DogId == request.DogId &&
                    item.AdopterUserId == adopterUserId,
                cancellationToken);

        if (feedback is null)
        {
            feedback = new CopilotResultFeedback
            {
                CopilotSessionId = request.SessionId,
                DogId = request.DogId,
                AdopterUserId = adopterUserId
            };
            context.CopilotResultFeedbacks.Add(feedback);
        }

        feedback.FeedbackType = request.FeedbackType;
        feedback.OptionalComment = comment;
        feedback.WasOpened = request.WasOpened;
        feedback.WasFavorited = request.WasFavorited;
        feedback.CreatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return ToDto(feedback);
    }

    public async Task<IReadOnlyDictionary<int, CopilotFeedbackDto>> GetFeedbackForSessionAsync(
        int sessionId,
        string adopterUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adopterUserId))
        {
            return new Dictionary<int, CopilotFeedbackDto>();
        }

        var ownsSession = await context.CopilotSessions
            .AsNoTracking()
            .AnyAsync(
                session => session.Id == sessionId && session.AdopterUserId == adopterUserId,
                cancellationToken);

        if (!ownsSession)
        {
            return new Dictionary<int, CopilotFeedbackDto>();
        }

        var feedback = await context.CopilotResultFeedbacks
            .AsNoTracking()
            .Where(item => item.CopilotSessionId == sessionId && item.AdopterUserId == adopterUserId)
            .ToListAsync(cancellationToken);

        return feedback.ToDictionary(item => item.DogId, ToDto);
    }

    public async Task<CopilotExplanationDto> BuildExplanationAsync(
        int sessionId,
        string adopterUserId,
        AdoptionCopilotDogResult result,
        IReadOnlyList<AdoptionCopilotConstraint> appliedConstraints,
        CancellationToken cancellationToken = default)
    {
        var session = await context.CopilotSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == sessionId && item.AdopterUserId == adopterUserId,
                cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException("Copilot session was not found for this adopter.");
        }

        var resultDogIds = CopilotHistoryService.DeserializeDogIds(session.ResultDogIdsJson);
        if (!resultDogIds.Contains(result.DogId))
        {
            throw new InvalidOperationException("This dog was not part of the selected Copilot session.");
        }

        var matchedCriteria = result.MatchedCriteria?.Count > 0
            ? result.MatchedCriteria
            : appliedConstraints;

        var directEvidence = FormatEvidenceItems(result.PositiveEvidence)
            .Concat(result.DisplayTags ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var indirectEvidence = result.Reasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        var cautionEvidence = FormatEvidenceItems(result.CautionEvidence)
            .Concat(result.CautionTags ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        var missingEvidence = FormatEvidenceItems(result.MissingEvidence)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        return new CopilotExplanationDto(
            sessionId,
            result.DogId,
            result.Dog.Name,
            session.SanitizedQuerySummary ?? session.QueryText ?? "Copilot search",
            matchedCriteria,
            directEvidence.Count > 0 ? directEvidence : ["The dog was returned by PawConnect's public-safe search."],
            indirectEvidence,
            cautionEvidence,
            missingEvidence.Count > 0 ? missingEvidence : ["No major missing evidence was flagged for this result."],
            BuildSuggestedQuestions(result, cautionEvidence, missingEvidence),
            $"{result.Dog.Name}'s match is advisory. Confirm behavior, medical details, and visit availability with the shelter before deciding.");
    }

    private static IReadOnlyList<string> FormatEvidenceItems(IReadOnlyList<EvidenceItem>? items)
    {
        return items?
            .Where(item => !string.IsNullOrWhiteSpace(item.Label))
            .Select(item =>
            {
                var label = item.Label.Trim();
                return string.IsNullOrWhiteSpace(item.MatchedText)
                    ? label
                    : $"{label}: {item.MatchedText.Trim()}";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private static IReadOnlyList<string> BuildSuggestedQuestions(
        AdoptionCopilotDogResult result,
        IReadOnlyList<string> cautionEvidence,
        IReadOnlyList<string> missingEvidence)
    {
        var questions = new List<string>();
        if (cautionEvidence.Count > 0)
        {
            questions.Add($"Ask the shelter to clarify: {cautionEvidence[0]}.");
        }

        if (missingEvidence.Count > 0 && !missingEvidence[0].StartsWith("No major", StringComparison.OrdinalIgnoreCase))
        {
            questions.Add($"Ask whether the shelter has more information about: {missingEvidence[0]}.");
        }

        if (questions.Count == 0)
        {
            questions.Add($"Ask the shelter whether {result.Dog.Name}'s current routine still matches this profile.");
        }

        return questions;
    }

    private static string? NormalizeComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxCommentLength)
        {
            throw new InvalidOperationException($"Feedback comment must be {MaxCommentLength} characters or fewer.");
        }

        return trimmed;
    }

    private static CopilotFeedbackDto ToDto(CopilotResultFeedback feedback)
    {
        return new CopilotFeedbackDto(
            feedback.Id,
            feedback.CopilotSessionId,
            feedback.DogId,
            feedback.FeedbackType,
            feedback.CreatedAt,
            feedback.OptionalComment,
            feedback.WasOpened,
            feedback.WasFavorited);
    }
}
