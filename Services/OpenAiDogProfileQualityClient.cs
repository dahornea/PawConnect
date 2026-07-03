using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PawConnect.Services;

public class OpenAiDogProfileQualityClient(
    HttpClient httpClient,
    IOptions<OpenAiSettings> options,
    ILogger<OpenAiDogProfileQualityClient> logger) : IOpenAiDogProfileQualityClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxIssues = 10;
    private const int MaxQuestions = 5;
    private const int MaxTextLength = 1000;

    public async Task<OpenAiDogProfileQualityResponse> CheckAsync(
        DogProfileQualityRequest request,
        DogProfileQualityResult deterministicResult,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.DogProfileQualityEnabled || !settings.HasApiKey)
        {
            return OpenAiDogProfileQualityResponse.Failed("OpenAI dog profile quality checker is disabled or not configured.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());
            httpRequest.Content = JsonContent.Create(new
            {
                model = settings.GetSafeDogProfileQualityModel(),
                input = BuildInput(request, deterministicResult),
                text = new
                {
                    format = BuildResponseFormat()
                }
            }, options: JsonOptions);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenAI dog profile quality request failed with status {StatusCode}.", response.StatusCode);
                return OpenAiDogProfileQualityResponse.Failed("OpenAI profile quality request failed.");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var outputText = ExtractOutputText(responseJson);
            var payload = DeserializePayload(outputText);
            if (payload is null)
            {
                return OpenAiDogProfileQualityResponse.Failed("OpenAI profile quality response was not valid JSON.");
            }

            return OpenAiDogProfileQualityResponse.Successful(ToResult(payload));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "OpenAI dog profile quality checker failed.");
            return OpenAiDogProfileQualityResponse.Failed("OpenAI profile quality checker failed.");
        }
    }

    private static object[] BuildInput(DogProfileQualityRequest request, DogProfileQualityResult deterministicResult)
    {
        return
        [
            new
            {
                role = "system",
                content = """
                You are PawConnect's dog profile quality assistant for shelter staff.
                Review only the public-safe dog profile summary provided by PawConnect.
                Help the shelter identify missing, vague, risky, or unclear public information.
                Do not invent facts. Do not claim a dog is good with cats, children, other dogs, vaccinated, healthy, fully trained, or safe unless that information exists in the input.
                Suggested rewrites must be cautious and based only on source profile fields.
                The shelter manually reviews suggestions before saving; do not imply that text has been saved.
                Return valid JSON only.
                """
            },
            new
            {
                role = "user",
                content = JsonSerializer.Serialize(new
                {
                    profile = new
                    {
                        request.Name,
                        request.AgeYears,
                        request.AgeMonths,
                        request.Size,
                        request.Status,
                        request.BreedDisplay,
                        request.CoatColor,
                        request.Description,
                        request.BehaviorDescription,
                        request.MedicalStatus,
                        request.PreferredFoodType,
                        request.DailyFoodAmountGrams,
                        catCompatibility = DogCompatibilityFormatter.FormatCat(request.CatCompatibility),
                        dogCompatibility = DogCompatibilityFormatter.FormatDog(request.DogCompatibility),
                        childrenCompatibility = DogCompatibilityFormatter.FormatChildren(request.ChildrenCompatibility),
                        activityLevel = DogCompatibilityFormatter.FormatActivity(request.ActivityLevel),
                        experienceNeeded = DogCompatibilityFormatter.FormatExperience(request.ExperienceNeeded),
                        apartmentSuitability = DogCompatibilityFormatter.FormatApartment(request.ApartmentSuitability),
                        request.CompatibilityNotes
                    },
                    deterministicFindings = new
                    {
                        deterministicResult.OverallScore,
                        deterministicResult.Summary,
                        issues = deterministicResult.Issues.Select(issue => new
                        {
                            category = issue.Category.ToString(),
                            severity = issue.Severity.ToString(),
                            issue.Message,
                            issue.FieldName,
                            issue.SuggestedAction
                        }),
                        deterministicResult.Strengths
                    }
                }, JsonOptions)
            }
        ];
    }

    private static object BuildResponseFormat()
    {
        return new
        {
            type = "json_schema",
            name = "dog_profile_quality_response",
            strict = true,
            schema = new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    overallScore = NumberSchema("Profile quality score from 0 to 100."),
                    summary = StringSchema("One short shelter-facing summary."),
                    issues = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                category = StringSchema("One known category."),
                                severity = StringSchema("Info, Low, Medium, or High."),
                                message = StringSchema("Concrete issue or warning."),
                                fieldName = NullableStringSchema("Related field name, if any."),
                                suggestedAction = NullableStringSchema("Practical action for shelter staff.")
                            },
                            required = new[] { "category", "severity", "message", "fieldName", "suggestedAction" }
                        }
                    },
                    strengths = ArraySchema("Profile strengths already present."),
                    suggestions = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                fieldName = StringSchema("Description, BehaviorDescription, MedicalStatus, or CompatibilityNotes."),
                                suggestedText = StringSchema("Suggested text based only on the input profile."),
                                rationale = NullableStringSchema("Why this suggestion helps.")
                            },
                            required = new[] { "fieldName", "suggestedText", "rationale" }
                        }
                    },
                    suggestedRewrite = new
                    {
                        type = new[] { "object", "null" },
                        additionalProperties = false,
                        properties = new
                        {
                            title = NullableStringSchema("Short title for the suggested rewrite."),
                            description = NullableStringSchema("Suggested public description based only on input facts."),
                            behaviorDescription = NullableStringSchema("Suggested behavior description based only on input facts.")
                        },
                        required = new[] { "title", "description", "behaviorDescription" }
                    },
                    questionsForShelter = ArraySchema("Follow-up questions for staff to answer before saving."),
                    safetyNotes = ArraySchema("Safety reminders about verifying facts.")
                },
                required = new[]
                {
                    "overallScore",
                    "summary",
                    "issues",
                    "strengths",
                    "suggestions",
                    "suggestedRewrite",
                    "questionsForShelter",
                    "safetyNotes"
                }
            }
        };
    }

    private static object StringSchema(string description)
    {
        return new
        {
            type = "string",
            description
        };
    }

    private static object NullableStringSchema(string description)
    {
        return new
        {
            type = new[] { "string", "null" },
            description
        };
    }

    private static object NumberSchema(string description)
    {
        return new
        {
            type = "number",
            description
        };
    }

    private static object ArraySchema(string description)
    {
        return new
        {
            type = "array",
            description,
            items = new
            {
                type = "string"
            }
        };
    }

    private static DogProfileQualityResult ToResult(OpenAiDogProfileQualityPayload payload)
    {
        var issues = payload.Issues
            .Select(ToIssue)
            .Where(issue => issue is not null)
            .Select(issue => issue!)
            .Take(MaxIssues)
            .ToList();

        var suggestions = payload.Suggestions
            .Select(suggestion => new DogProfileQualitySuggestion(
                SafeTrim(suggestion.FieldName, 80) ?? "Description",
                SafeTrim(suggestion.SuggestedText, MaxTextLength) ?? string.Empty,
                SafeTrim(suggestion.Rationale, 240)))
            .Where(suggestion => !string.IsNullOrWhiteSpace(suggestion.SuggestedText))
            .Take(5)
            .ToList();

        return new DogProfileQualityResult(
            Math.Clamp(payload.OverallScore, 0, 100),
            SafeTrim(payload.Summary, 220) ?? "Profile quality check completed.",
            issues,
            NormalizeStrings(payload.Strengths, 8),
            suggestions,
            payload.SuggestedRewrite is null
                ? null
                : new DogProfileRewriteSuggestion(
                    SafeTrim(payload.SuggestedRewrite.Title, 80),
                    SafeTrim(payload.SuggestedRewrite.Description, MaxTextLength),
                    SafeTrim(payload.SuggestedRewrite.BehaviorDescription, MaxTextLength)),
            NormalizeStrings(payload.QuestionsForShelter, MaxQuestions),
            NormalizeStrings(payload.SafetyNotes, 5),
            UsedAi: true,
            FallbackReason: null);
    }

    private static DogProfileQualityIssue? ToIssue(OpenAiDogProfileQualityPayloadIssue issue)
    {
        if (string.IsNullOrWhiteSpace(issue.Message))
        {
            return null;
        }

        if (!Enum.TryParse<DogProfileQualityCategory>(issue.Category, ignoreCase: true, out var category))
        {
            return null;
        }

        if (!Enum.TryParse<DogProfileQualitySeverity>(issue.Severity, ignoreCase: true, out var severity))
        {
            severity = DogProfileQualitySeverity.Info;
        }

        return new DogProfileQualityIssue(
            category,
            severity,
            SafeTrim(issue.Message, 240) ?? string.Empty,
            SafeTrim(issue.FieldName, 80),
            SafeTrim(issue.SuggestedAction, 240));
    }

    private static IReadOnlyList<string> NormalizeStrings(IEnumerable<string?> values, int take)
    {
        return values
            .Select(value => SafeTrim(value, 240))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    private static string? SafeTrim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var clean = value.Trim();
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }

    private static OpenAiDogProfileQualityPayload? DeserializePayload(string? outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return null;
        }

        return JsonSerializer.Deserialize<OpenAiDogProfileQualityPayload>(outputText.Trim(), JsonOptions);
    }

    private static string? ExtractOutputText(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (!document.RootElement.TryGetProperty("output", out var output) ||
            output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }

    private sealed class OpenAiDogProfileQualityPayload
    {
        public int OverallScore { get; set; }

        public string? Summary { get; set; }

        public List<OpenAiDogProfileQualityPayloadIssue> Issues { get; set; } = [];

        public List<string?> Strengths { get; set; } = [];

        public List<OpenAiDogProfileQualityPayloadSuggestion> Suggestions { get; set; } = [];

        public OpenAiDogProfileQualityPayloadRewrite? SuggestedRewrite { get; set; }

        public List<string?> QuestionsForShelter { get; set; } = [];

        public List<string?> SafetyNotes { get; set; } = [];
    }

    private sealed class OpenAiDogProfileQualityPayloadIssue
    {
        public string? Category { get; set; }

        public string? Severity { get; set; }

        public string? Message { get; set; }

        public string? FieldName { get; set; }

        public string? SuggestedAction { get; set; }
    }

    private sealed class OpenAiDogProfileQualityPayloadSuggestion
    {
        public string? FieldName { get; set; }

        public string? SuggestedText { get; set; }

        public string? Rationale { get; set; }
    }

    private sealed class OpenAiDogProfileQualityPayloadRewrite
    {
        public string? Title { get; set; }

        public string? Description { get; set; }

        public string? BehaviorDescription { get; set; }
    }
}
