using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PawConnect.Services;

public class OpenAiRecommendationClient(
    HttpClient httpClient,
    IOptions<OpenAiSettings> options,
    ILogger<OpenAiRecommendationClient> logger) : IOpenAiRecommendationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OpenAiRecommendationResponse> GetEnhancedRecommendationsAsync(
        RecommendationOpenAiRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.HasApiKey)
        {
            return OpenAiRecommendationResponse.Failed("OpenAI recommendations are disabled or not configured.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());
            httpRequest.Content = JsonContent.Create(new
            {
                model = settings.GetSafeModel(),
                input = new object[]
                {
                    new
                    {
                        role = "system",
                        content = """
                        You improve dog adoption recommendations for PawConnect.
                        Recommend only from the provided candidate dog IDs.
                        Do not invent dogs, hidden data, private details, or unavailable dogs.
                        Do not mention AI.
                        Write natural, concise, adopter-friendly explanations in warm but professional language.
                        Keep shortSummary to one short sentence.
                        Avoid robotic phrases such as "may fit you because", "suitable for", and repeated sentence structures.
                        Do not exaggerate certainty; use phrases such as "could be a good match", "may suit", or "aligns with".
                        Return reasons as short readable phrases, not sentence fragments that sound technical.
                        Return valid JSON only with this shape:
                        {"recommendations":[{"dogId":1,"rank":1,"matchLabel":"Excellent match","shortSummary":"This dog could be a good match because their profile aligns with the adopter's home.","reasons":["Medium size may suit apartment living","Social and friendly behavior profile"],"categories":[{"category":"Home fit","text":"Medium size may suit apartment living"}]}]}
                        """
                    },
                    new
                    {
                        role = "user",
                        content = JsonSerializer.Serialize(request, JsonOptions)
                    }
                },
                text = new
                {
                    format = new
                    {
                        type = "json_object"
                    }
                }
            }, options: JsonOptions);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenAI recommendation request failed with status {StatusCode}.", response.StatusCode);
                return OpenAiRecommendationResponse.Failed("OpenAI request failed.");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var outputText = ExtractOutputText(responseJson);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                return OpenAiRecommendationResponse.Failed("OpenAI response did not include recommendation JSON.");
            }

            var payload = DeserializePayload(outputText);
            if (payload?.Recommendations is null || payload.Recommendations.Count == 0)
            {
                return OpenAiRecommendationResponse.Failed("OpenAI response did not include usable recommendations.");
            }

            var items = payload.Recommendations
                .Where(item => item.DogId > 0)
                .Select(item => new OpenAiRecommendationItem(
                    item.DogId,
                    item.Rank <= 0 ? int.MaxValue : item.Rank,
                    NormalizeMatchLabel(item.MatchLabel),
                    NormalizeReasons(item.Reasons),
                    SafeTrim(item.ShortSummary, 220),
                    NormalizeCategories(item.Categories)))
                .ToList();

            return items.Count == 0
                ? OpenAiRecommendationResponse.Failed("OpenAI response did not include usable recommendations.")
                : new OpenAiRecommendationResponse(true, items);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "OpenAI recommendation enhancement failed. Falling back to rule-based recommendations.");
            return OpenAiRecommendationResponse.Failed("OpenAI enhancement failed.");
        }
    }

    private static string? ExtractOutputText(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;

        if (root.TryGetProperty("output_text", out var outputTextElement) &&
            outputTextElement.ValueKind == JsonValueKind.String)
        {
            return outputTextElement.GetString();
        }

        if (root.TryGetProperty("output", out var outputElement) &&
            outputElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var outputItem in outputElement.EnumerateArray())
            {
                if (!outputItem.TryGetProperty("content", out var contentElement) ||
                    contentElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in contentElement.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var textElement) &&
                        textElement.ValueKind == JsonValueKind.String)
                    {
                        return textElement.GetString();
                    }
                }
            }
        }

        return null;
    }

    private static OpenAiRecommendationPayload? DeserializePayload(string outputText)
    {
        var trimmed = outputText.Trim();
        var jsonStart = trimmed.IndexOf('{');
        var jsonEnd = trimmed.LastIndexOf('}');
        if (jsonStart > 0 || jsonEnd < trimmed.Length - 1)
        {
            trimmed = jsonStart >= 0 && jsonEnd >= jsonStart
                ? trimmed[jsonStart..(jsonEnd + 1)]
                : trimmed;
        }

        return JsonSerializer.Deserialize<OpenAiRecommendationPayload>(trimmed, JsonOptions);
    }

    private static string NormalizeMatchLabel(string? label)
    {
        return label?.Trim() switch
        {
            "Excellent match" => "Excellent match",
            "Good match" => "Good match",
            "Possible match" => "Possible match",
            _ => "Good match"
        };
    }

    private static IReadOnlyList<string> NormalizeReasons(IReadOnlyList<string>? reasons)
    {
        return reasons?
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Select(reason => reason.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList() ?? [];
    }

    private static IReadOnlyList<OpenAiRecommendationReason> NormalizeCategories(IReadOnlyList<OpenAiRecommendationPayloadReason>? categories)
    {
        return categories?
            .Where(category => !string.IsNullOrWhiteSpace(category.Category) && !string.IsNullOrWhiteSpace(category.Text))
            .Select(category => new OpenAiRecommendationReason(
                NormalizeCategory(category.Category),
                category.Text!.Trim()))
            .Distinct()
            .Take(4)
            .ToList() ?? [];
    }

    private static string NormalizeCategory(string? category)
    {
        return category?.Trim() switch
        {
            "Home fit" => "Home fit",
            "Experience fit" => "Experience fit",
            "Location fit" => "Location fit",
            "Behavior fit" => "Behavior fit",
            "Preferences fit" => "Preferences fit",
            _ => "Match fit"
        };
    }

    private static string? SafeTrim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private sealed class OpenAiRecommendationPayload
    {
        public List<OpenAiRecommendationPayloadItem> Recommendations { get; set; } = [];
    }

    private sealed class OpenAiRecommendationPayloadItem
    {
        public int DogId { get; set; }

        public int Rank { get; set; }

        public string? MatchLabel { get; set; }

        public List<string>? Reasons { get; set; }

        public string? ShortSummary { get; set; }

        public List<OpenAiRecommendationPayloadReason>? Categories { get; set; }
    }

    private sealed class OpenAiRecommendationPayloadReason
    {
        public string? Category { get; set; }

        public string? Text { get; set; }
    }
}
