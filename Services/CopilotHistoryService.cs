using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class CopilotHistoryService(ApplicationDbContext context) : ICopilotHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> SaveSessionAsync(
        string adopterUserId,
        string queryText,
        AdoptionCopilotResponse response,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adopterUserId))
        {
            throw new InvalidOperationException("Current adopter account could not be found.");
        }

        var summary = SanitizeQuerySummary(queryText);
        var constraints = AdoptionCopilotConstraintNormalizer.Normalize(response.AppliedConstraints ?? []);
        var resultDogIds = response.Results
            .Select(result => result.DogId)
            .Distinct()
            .ToList();

        var session = new CopilotSession
        {
            AdopterUserId = adopterUserId,
            CreatedAt = DateTime.UtcNow,
            QueryText = summary,
            SanitizedQuerySummary = summary,
            PrimaryIntent = DeterminePrimaryIntent(constraints),
            CompatibilityTarget = GetConstraintValue(constraints, "Compatibility"),
            HomeType = GetConstraintValue(constraints, "Home"),
            ActivityLevel = GetConstraintValue(constraints, "Activity") ?? GetConstraintValue(constraints, "Lifestyle"),
            City = ExtractCity(GetConstraintValue(constraints, "Location")),
            Neighborhood = ExtractNeighborhood(GetConstraintValue(constraints, "Location")),
            UsedAiEnhancement = response.UsedAiEnhancement,
            UsedSemanticSearch = response.UsedSemanticSearch,
            UsedToolCalling = response.UsedToolCalling,
            FallbackReason = TrimToLength(response.FallbackReason, 500),
            AppliedConstraintsJson = JsonSerializer.Serialize(constraints, JsonOptions),
            ResultDogIdsJson = JsonSerializer.Serialize(resultDogIds, JsonOptions),
            ResultCount = resultDogIds.Count
        };

        context.CopilotSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    public async Task<IReadOnlyList<CopilotHistoryItemDto>> GetRecentSessionsAsync(
        string adopterUserId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adopterUserId))
        {
            return [];
        }

        var safeCount = Math.Clamp(count, 1, 25);
        var sessions = await context.CopilotSessions
            .AsNoTracking()
            .Where(session => session.AdopterUserId == adopterUserId)
            .OrderByDescending(session => session.CreatedAt)
            .Take(safeCount)
            .ToListAsync(cancellationToken);

        return sessions.Select(ToHistoryItem).ToList();
    }

    public async Task<CopilotSessionDto?> GetSessionAsync(
        int sessionId,
        string adopterUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adopterUserId))
        {
            return null;
        }

        var session = await context.CopilotSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == sessionId && item.AdopterUserId == adopterUserId,
                cancellationToken);

        return session is null ? null : ToSessionDto(session);
    }

    internal static string SanitizeQuerySummary(string? queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return "Copilot search";
        }

        var value = queryText.Trim();
        value = Regex.Replace(value, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", "[email]", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\+?\d[\d\s().-]{6,}\d", "[phone]", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\s+", " ");
        return TrimToLength(value, 500) ?? "Copilot search";
    }

    internal static IReadOnlyList<AdoptionCopilotConstraint> DeserializeConstraints(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<AdoptionCopilotConstraint>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static IReadOnlyList<int> DeserializeDogIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<int>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static CopilotHistoryItemDto ToHistoryItem(CopilotSession session)
    {
        return new CopilotHistoryItemDto(
            session.Id,
            session.CreatedAt,
            GetQuerySummary(session),
            session.ResultCount,
            session.UsedAiEnhancement,
            session.UsedSemanticSearch,
            session.UsedToolCalling,
            session.FallbackReason,
            DeserializeConstraints(session.AppliedConstraintsJson));
    }

    private static CopilotSessionDto ToSessionDto(CopilotSession session)
    {
        return new CopilotSessionDto(
            session.Id,
            session.CreatedAt,
            GetQuerySummary(session),
            DeserializeDogIds(session.ResultDogIdsJson),
            session.ResultCount,
            session.UsedAiEnhancement,
            session.UsedSemanticSearch,
            session.UsedToolCalling,
            session.FallbackReason,
            DeserializeConstraints(session.AppliedConstraintsJson));
    }

    private static string GetQuerySummary(CopilotSession session)
    {
        return string.IsNullOrWhiteSpace(session.SanitizedQuerySummary)
            ? session.QueryText ?? "Copilot search"
            : session.SanitizedQuerySummary;
    }

    private static string? DeterminePrimaryIntent(IReadOnlyList<AdoptionCopilotConstraint> constraints)
    {
        if (constraints.Any(constraint => constraint.Label.Equals("Compatibility", StringComparison.OrdinalIgnoreCase)))
        {
            return "Compatibility";
        }

        if (constraints.Any(constraint =>
                constraint.Label.Equals("Home", StringComparison.OrdinalIgnoreCase) ||
                constraint.Label.Equals("Activity", StringComparison.OrdinalIgnoreCase) ||
                constraint.Label.Equals("Lifestyle", StringComparison.OrdinalIgnoreCase) ||
                constraint.Label.Equals("Temperament", StringComparison.OrdinalIgnoreCase)))
        {
            return "Lifestyle";
        }

        if (constraints.Count > 0)
        {
            return "Filter";
        }

        return "Search";
    }

    private static string? GetConstraintValue(
        IReadOnlyList<AdoptionCopilotConstraint> constraints,
        string label)
    {
        return constraints
            .FirstOrDefault(constraint => constraint.Label.Equals(label, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string? ExtractCity(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        return location.Contains("Cluj", StringComparison.OrdinalIgnoreCase)
            ? "Cluj-Napoca"
            : null;
    }

    private static string? ExtractNeighborhood(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var parts = location.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.FirstOrDefault(part => !part.Contains("Cluj", StringComparison.OrdinalIgnoreCase));
    }

    private static string? TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
