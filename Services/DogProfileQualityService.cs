using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public partial class DogProfileQualityService(
    ApplicationDbContext context,
    IOpenAiDogProfileQualityClient openAiClient,
    ILogger<DogProfileQualityService> logger) : IDogProfileQualityService
{
    private const int MaxIssues = 10;
    private const int MaxStrengths = 8;
    private const int MaxQuestions = 5;
    private const int MaxSuggestions = 5;

    private static readonly string[] ActivityTerms =
    [
        "walk", "walks", "exercise", "active", "activity", "play", "fetch", "training",
        "outdoor", "yard", "run", "enrichment", "settles", "calm routine", "quiet routine"
    ];

    private static readonly string[] CompatibilityTerms =
    [
        "cat", "cats", "children", "kids", "dog", "dogs", "introductions", "family",
        "older children", "small pets", "supervised", "gentle", "redirect"
    ];

    private static readonly string[] IdealHomeTerms =
    [
        "home", "apartment", "yard", "house", "family", "routine", "quiet", "active",
        "patient", "experienced", "first-time", "children", "cats", "dogs"
    ];

    private static readonly string[] OverconfidentPhrases =
    [
        "perfect for everyone",
        "perfect family dog",
        "guaranteed",
        "no problems",
        "best dog for any family",
        "safe with all children",
        "safe with cats",
        "does not bite",
        "fully trained"
    ];

    public async Task<DogProfileQualityResult> CheckFormAsync(
        DogProfileQualityRequest request,
        CancellationToken cancellationToken = default)
    {
        var deterministic = BuildDeterministicResult(request, aiError: null);

        try
        {
            var aiResponse = await openAiClient.CheckAsync(request, deterministic, cancellationToken);
            if (!aiResponse.Success || aiResponse.Result is null)
            {
                return deterministic with
                {
                    FallbackReason = aiResponse.ErrorMessage ?? deterministic.FallbackReason
                };
            }

            return MergeResults(request, deterministic, aiResponse.Result);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Dog profile quality AI check failed; using deterministic result.");
            return deterministic with
            {
                FallbackReason = "AI profile quality check failed. Local checks were used instead."
            };
        }
    }

    public async Task<DogProfileQualityResult> CheckDogAsync(
        int dogId,
        int shelterId,
        CancellationToken cancellationToken = default)
    {
        var dog = await context.Dogs
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
            .Include(d => d.PreferredFoodType)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dogId && d.ShelterId == shelterId, cancellationToken);

        if (dog is null)
        {
            throw new InvalidOperationException("Dog was not found for your shelter.");
        }

        return await CheckFormAsync(BuildRequestFromDog(dog, shelterId), cancellationToken);
    }

    public DogProfileQualityRequest BuildRequestFromDog(Dog dog, int shelterId)
    {
        return new DogProfileQualityRequest
        {
            DogId = dog.Id == 0 ? null : dog.Id,
            ShelterId = shelterId,
            Name = dog.Name,
            AgeYears = dog.AgeYears,
            AgeMonths = dog.AgeMonths,
            Size = dog.Size,
            Status = dog.Status,
            BreedDisplay = DogBreedFormatter.Format(dog),
            CoatColor = dog.CoatColor,
            Description = dog.Description,
            BehaviorDescription = dog.BehaviorDescription,
            MedicalStatus = dog.MedicalStatus,
            PreferredFoodType = dog.PreferredFoodType?.Name,
            DailyFoodAmountGrams = dog.DailyFoodAmountGrams,
            CatCompatibility = dog.CatCompatibility,
            DogCompatibility = dog.DogCompatibility,
            ChildrenCompatibility = dog.ChildrenCompatibility,
            ActivityLevel = dog.ActivityLevel,
            ExperienceNeeded = dog.ExperienceNeeded,
            ApartmentSuitability = dog.ApartmentSuitability,
            CompatibilityNotes = dog.CompatibilityNotes
        };
    }

    private static DogProfileQualityResult BuildDeterministicResult(DogProfileQualityRequest request, string? aiError)
    {
        var issues = new List<DogProfileQualityIssue>();
        var strengths = new List<string>();
        var questions = new List<string>();
        var safetyNotes = new List<string>
        {
            "Suggestions are AI-assisted or rule-based drafting help. Shelter staff must confirm facts before saving profile text.",
            "Do not add compatibility, medical, or training claims unless they are supported by shelter observations."
        };

        var description = Clean(request.Description);
        var behavior = Clean(request.BehaviorDescription);
        var medical = Clean(request.MedicalStatus);
        var compatibilityNotes = Clean(request.CompatibilityNotes);
        var combinedPublicText = string.Join(" ", description, behavior, medical, compatibilityNotes);

        if (string.IsNullOrWhiteSpace(description))
        {
            issues.Add(new DogProfileQualityIssue(
                DogProfileQualityCategory.MissingBehaviorInfo,
                DogProfileQualitySeverity.High,
                "The public description is missing.",
                nameof(request.Description),
                "Add 2-4 natural sentences about the dog's routine, personality, and ideal adopter."));
        }
        else if (description.Length < 80)
        {
            issues.Add(new DogProfileQualityIssue(
                DogProfileQualityCategory.TooShort,
                DogProfileQualitySeverity.Medium,
                "The description is quite short and may not give adopters enough context.",
                nameof(request.Description),
                "Add concrete observations, such as walk routine, indoor behavior, and what kind of home may suit the dog."));
        }
        else
        {
            strengths.Add("The profile includes a public description with some context.");
        }

        if (IsGenericDescription(description))
        {
            issues.Add(new DogProfileQualityIssue(
                DogProfileQualityCategory.VagueDescription,
                DogProfileQualitySeverity.Medium,
                "The description sounds generic and does not include enough observable evidence.",
                nameof(request.Description),
                "Replace generic praise with specific shelter observations."));
        }

        if (string.IsNullOrWhiteSpace(behavior))
        {
            issues.Add(new DogProfileQualityIssue(
                DogProfileQualityCategory.MissingBehaviorInfo,
                DogProfileQualitySeverity.High,
                "Behavior description is missing.",
                nameof(request.BehaviorDescription),
                "Describe how the dog behaves with people, routines, handling, other animals, or new situations."));
        }
        else if (behavior.Length < 50)
        {
            issues.Add(new DogProfileQualityIssue(
                DogProfileQualityCategory.TooShort,
                DogProfileQualitySeverity.Medium,
                "Behavior description is too brief to support confident adopter decisions.",
                nameof(request.BehaviorDescription),
                "Add one or two specific behavior observations."));
        }
        else
        {
            strengths.Add("Behavior details are present.");
        }

        if (!HasAny(combinedPublicText, ActivityTerms) && request.ActivityLevel == DogActivityLevel.Unknown)
        {
            issues.Add(new DogProfileQualityIssue(
                DogProfileQualityCategory.MissingActivityInfo,
                DogProfileQualitySeverity.Medium,
                "The profile does not clearly explain the dog's daily activity needs.",
                nameof(request.Description),
                "Mention short walks, longer walks, play, enrichment, or how quickly the dog settles."));
            questions.Add("What kind of daily activity does this dog need to stay comfortable?");
        }
        else
        {
            strengths.Add("Activity or routine information is available.");
        }

        if (!HasAny(combinedPublicText, CompatibilityTerms) &&
            request.CatCompatibility == CatCompatibility.Unknown &&
            request.DogCompatibility == DogCompatibility.Unknown &&
            request.ChildrenCompatibility == ChildrenCompatibility.Unknown)
        {
            issues.Add(new DogProfileQualityIssue(
                DogProfileQualityCategory.MissingCompatibilityInfo,
                DogProfileQualitySeverity.Medium,
                "Compatibility with cats, dogs, or children is not clear.",
                nameof(request.CompatibilityNotes),
                "Add known observations or say that adopters should ask the shelter if compatibility has not been tested."));
            questions.Add("Has this dog been observed around cats, other dogs, or children?");
        }
        else
        {
            strengths.Add("The profile includes compatibility information or structured compatibility fields.");
        }

        if (!HasAny(combinedPublicText, IdealHomeTerms) &&
            request.ExperienceNeeded == DogExperienceNeeded.Unknown &&
            request.ApartmentSuitability == ApartmentSuitability.Unknown)
        {
            issues.Add(new DogProfileQualityIssue(
                DogProfileQualityCategory.MissingIdealHomeInfo,
                DogProfileQualitySeverity.Low,
                "The ideal home or adopter type is not described.",
                nameof(request.Description),
                "Mention whether the dog may suit a calm home, active home, apartment routine, yard, patient adopter, or experienced adopter."));
            questions.Add("What type of home or adopter would help this dog settle well?");
        }

        if (string.IsNullOrWhiteSpace(medical))
        {
            issues.Add(new DogProfileQualityIssue(
                DogProfileQualityCategory.MissingMedicalContext,
                DogProfileQualitySeverity.Low,
                "Public medical status is missing.",
                nameof(request.MedicalStatus),
                "Add a short public-safe medical summary, or say that medical details are available from the shelter."));
        }
        else
        {
            strengths.Add("A public medical status summary is present.");
        }

        foreach (var phrase in OverconfidentPhrases.Where(phrase =>
                     combinedPublicText.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new DogProfileQualityIssue(
                DogProfileQualityCategory.OverconfidentClaim,
                DogProfileQualitySeverity.High,
                $"The phrase \"{phrase}\" may be too broad or unsafe.",
                null,
                "Use careful wording based on observed behavior instead of guarantees."));
        }

        if (request.CoatColor is not null)
        {
            strengths.Add("Coat color is filled in.");
        }

        if (request.BreedDisplay != "Unknown")
        {
            strengths.Add("Breed display information is available.");
        }

        var score = CalculateScore(issues, strengths);
        var summary = BuildSummary(score, issues.Count);

        return new DogProfileQualityResult(
            score,
            summary,
            LimitDistinctIssues(issues),
            strengths.Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxStrengths).ToList(),
            [],
            null,
            questions.Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxQuestions).ToList(),
            safetyNotes,
            UsedAi: false,
            FallbackReason: aiError ?? "Local rule-based profile quality checks were used.");
    }

    private static DogProfileQualityResult MergeResults(
        DogProfileQualityRequest request,
        DogProfileQualityResult deterministic,
        DogProfileQualityResult aiResult)
    {
        var issues = deterministic.Issues
            .Concat(aiResult.Issues)
            .Where(issue => !string.IsNullOrWhiteSpace(issue.Message))
            .GroupBy(issue => NormalizeKey($"{issue.Category}|{issue.FieldName}|{issue.Message}"))
            .Select(group => group.First())
            .Take(MaxIssues)
            .ToList();

        var strengths = deterministic.Strengths
            .Concat(aiResult.Strengths)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxStrengths)
            .ToList();

        var suggestions = aiResult.Suggestions
            .Where(suggestion => !string.IsNullOrWhiteSpace(suggestion.FieldName) &&
                                 !string.IsNullOrWhiteSpace(suggestion.SuggestedText))
            .Take(MaxSuggestions)
            .ToList();

        var safetyNotes = deterministic.SafetyNotes
            .Concat(aiResult.SafetyNotes)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var rewrite = SanitizeRewrite(request, aiResult.SuggestedRewrite, safetyNotes);
        var score = Math.Clamp((deterministic.OverallScore + aiResult.OverallScore) / 2, 0, 100);
        if (issues.Any(issue => issue.Severity == DogProfileQualitySeverity.High))
        {
            score = Math.Min(score, 76);
        }

        return new DogProfileQualityResult(
            score,
            string.IsNullOrWhiteSpace(aiResult.Summary)
                ? BuildSummary(score, issues.Count)
                : Trim(aiResult.Summary, 220) ?? BuildSummary(score, issues.Count),
            issues,
            strengths,
            suggestions,
            rewrite,
            aiResult.QuestionsForShelter
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxQuestions)
                .ToList(),
            safetyNotes,
            UsedAi: true,
            FallbackReason: null);
    }

    private static DogProfileRewriteSuggestion? SanitizeRewrite(
        DogProfileQualityRequest request,
        DogProfileRewriteSuggestion? rewrite,
        List<string> safetyNotes)
    {
        if (rewrite is null)
        {
            return null;
        }

        var sourceText = string.Join(" ", request.Description, request.BehaviorDescription, request.MedicalStatus, request.CompatibilityNotes);

        var title = Trim(rewrite.Title, 80);
        var description = SanitizeSuggestedText(rewrite.Description, sourceText, safetyNotes, "description");
        var behavior = SanitizeSuggestedText(rewrite.BehaviorDescription, sourceText, safetyNotes, "behavior description");

        return string.IsNullOrWhiteSpace(title) &&
               string.IsNullOrWhiteSpace(description) &&
               string.IsNullOrWhiteSpace(behavior)
            ? null
            : new DogProfileRewriteSuggestion(title, description, behavior);
    }

    private static string? SanitizeSuggestedText(string? value, string sourceText, List<string> safetyNotes, string fieldName)
    {
        var text = Trim(value, 1000);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var riskyClaims = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["good with cats"] = ["cat", "cats"],
            ["safe with cats"] = ["cat", "cats"],
            ["good with children"] = ["children", "kids", "older children"],
            ["safe with children"] = ["children", "kids", "older children"],
            ["fully trained"] = ["training", "trained"],
            ["vaccinated"] = ["vaccinated", "vaccine"],
            ["healthy"] = ["healthy", "medical", "vaccine", "check"],
            ["does not bite"] = ["bite", "mouth", "handling"]
        };

        foreach (var (claim, evidenceTerms) in riskyClaims)
        {
            if (text.Contains(claim, StringComparison.OrdinalIgnoreCase) &&
                !evidenceTerms.Any(term => sourceText.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                safetyNotes.Add($"A suggested {fieldName} rewrite contained an unsupported \"{claim}\" claim, so that rewrite field was hidden.");
                return null;
            }
        }

        return text;
    }

    private static int CalculateScore(IReadOnlyList<DogProfileQualityIssue> issues, IReadOnlyList<string> strengths)
    {
        var penalty = issues.Sum(issue => issue.Severity switch
        {
            DogProfileQualitySeverity.High => 18,
            DogProfileQualitySeverity.Medium => 10,
            DogProfileQualitySeverity.Low => 5,
            _ => 2
        });

        var strengthBonus = Math.Min(strengths.Count * 2, 10);
        return Math.Clamp(80 - penalty + strengthBonus, 0, 100);
    }

    private static string BuildSummary(int score, int issueCount)
    {
        return score switch
        {
            >= 85 => "This profile looks strong and gives adopters useful public context.",
            >= 70 => issueCount == 0
                ? "This profile is usable and mostly complete."
                : "This profile is usable, but a few details could make it clearer.",
            >= 50 => "This profile needs more detail before it will feel clear to adopters.",
            _ => "This profile is missing important public information."
        };
    }

    private static IReadOnlyList<DogProfileQualityIssue> LimitDistinctIssues(IEnumerable<DogProfileQualityIssue> issues)
    {
        return issues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.Message))
            .GroupBy(issue => NormalizeKey($"{issue.Category}|{issue.FieldName}|{issue.Message}"))
            .Select(group => group.First())
            .Take(MaxIssues)
            .ToList();
    }

    private static bool HasAny(string text, IReadOnlyList<string> terms)
    {
        return !string.IsNullOrWhiteSpace(text) &&
            terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGenericDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var normalized = NormalizeKey(description);
        return normalized is "friendly dog" or "very friendly dog" or "good dog" or "nice dog" ||
            (description.Length < 120 && GenericPraiseRegex().IsMatch(description) && !description.Contains('.', StringComparison.Ordinal));
    }

    private static string Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex().Replace(value.Trim(), " ");
    }

    private static string NormalizeKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex().Replace(value.Trim().ToLowerInvariant(), " ");
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var clean = Clean(value);
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\b(friendly|cute|nice|sweet|lovely|good)\b", RegexOptions.IgnoreCase)]
    private static partial Regex GenericPraiseRegex();
}
