using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PawConnect.Entities;

namespace PawConnect.Services;

public class OpenAiNaturalLanguageSearchClient(
    HttpClient httpClient,
    IOptions<OpenAiSettings> options,
    ILogger<OpenAiNaturalLanguageSearchClient> logger) : IOpenAiNaturalLanguageSearchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OpenAiNaturalLanguageSearchResponse> InterpretAsync(
        NaturalLanguageSearchAiRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.HasApiKey)
        {
            return OpenAiNaturalLanguageSearchResponse.Failed("OpenAI natural-language search is disabled or not configured.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());
            httpRequest.Content = JsonContent.Create(new
            {
                model = settings.GetSafeChatModel(),
                input = BuildInput(request),
                text = new
                {
                    format = BuildResponseFormat()
                }
            }, options: JsonOptions);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenAI natural-language search request failed with status {StatusCode}.", response.StatusCode);
                return OpenAiNaturalLanguageSearchResponse.Failed("OpenAI request failed.");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = DeserializePayload(ExtractOutputText(responseJson));
            if (payload is null)
            {
                return OpenAiNaturalLanguageSearchResponse.Failed("OpenAI response was not valid search JSON.");
            }

            return OpenAiNaturalLanguageSearchResponse.Successful(payload.ToInterpretation());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "OpenAI natural-language search interpretation failed.");
            return OpenAiNaturalLanguageSearchResponse.Failed("OpenAI interpretation failed.");
        }
    }

    private static object[] BuildInput(NaturalLanguageSearchAiRequest request)
    {
        return
        [
            new
            {
                role = "system",
                content = """
                You interpret PawConnect operational search queries for the role provided in the request.
                Return only structured JSON that uses the allowed scopes, intents, statuses, and sort fields.
                Do not generate SQL, LINQ, code, table names, or arbitrary database expressions.
                Do not request or expose private adopter contact data.
                For Shelter role, never broaden the search to other shelters; PawConnect scopes the final query to the current shelter.
                If the query is unsupported, return Unknown intent and ask for clarification.
                PawConnect validates and executes the query with predefined backend handlers.
                """
            },
            new
            {
                role = "user",
                content = JsonSerializer.Serialize(request, JsonOptions)
            }
        ];
    }

    private static object BuildResponseFormat()
    {
        return new
        {
            type = "json_schema",
            name = "pawconnect_natural_language_search",
            strict = true,
            schema = new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    intent = StringSchema("One allowed NaturalLanguageSearchIntent value."),
                    scope = StringSchema("One allowed NaturalLanguageSearchScope value."),
                    confidence = NumberSchema("Confidence from 0 to 1."),
                    requestStatus = NullableStringSchema("Optional AdoptionRequestStatus."),
                    visitStatus = NullableStringSchema("Optional AdoptionVisitStatus."),
                    dogStatus = NullableStringSchema("Optional DogStatus."),
                    shelterApplicationStatus = NullableStringSchema("Optional ShelterRegistrationRequestStatus."),
                    dogName = NullableStringSchema("Optional dog name filter."),
                    shelterName = NullableStringSchema("Optional shelter name filter."),
                    city = NullableStringSchema("Optional city filter."),
                    resourceCategory = NullableStringSchema("Optional resource category filter."),
                    lowStockOnly = BooleanSchema("Whether only low stock resources are requested."),
                    noRequestsOnly = BooleanSchema("Whether only dogs with no adoption requests are requested."),
                    olderThanDays = NullableNumberSchema("Optional age threshold in days."),
                    dateFrom = NullableStringSchema("Optional ISO date/time start."),
                    dateTo = NullableStringSchema("Optional ISO date/time end."),
                    dateLabel = NullableStringSchema("Short date range label."),
                    sortField = NullableStringSchema("Optional allowed sort field."),
                    sortDirection = StringSchema("Ascending or Descending."),
                    limit = NumberSchema("Requested result limit."),
                    needsClarification = BooleanSchema("Whether the user should clarify."),
                    clarificationQuestion = NullableStringSchema("Clarification question if needed."),
                    explanation = StringSchema("Short explanation of the interpreted criteria.")
                },
                required = new[]
                {
                    "intent",
                    "scope",
                    "confidence",
                    "requestStatus",
                    "visitStatus",
                    "dogStatus",
                    "shelterApplicationStatus",
                    "dogName",
                    "shelterName",
                    "city",
                    "resourceCategory",
                    "lowStockOnly",
                    "noRequestsOnly",
                    "olderThanDays",
                    "dateFrom",
                    "dateTo",
                    "dateLabel",
                    "sortField",
                    "sortDirection",
                    "limit",
                    "needsClarification",
                    "clarificationQuestion",
                    "explanation"
                }
            }
        };
    }

    private static object StringSchema(string description)
    {
        return new { type = "string", description };
    }

    private static object NullableStringSchema(string description)
    {
        return new { type = new[] { "string", "null" }, description };
    }

    private static object NumberSchema(string description)
    {
        return new { type = "number", description };
    }

    private static object NullableNumberSchema(string description)
    {
        return new { type = new[] { "number", "null" }, description };
    }

    private static object BooleanSchema(string description)
    {
        return new { type = "boolean", description };
    }

    private static NaturalLanguageSearchPayload? DeserializePayload(string? outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return null;
        }

        return JsonSerializer.Deserialize<NaturalLanguageSearchPayload>(outputText.Trim(), JsonOptions);
    }

    private static string? ExtractOutputText(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (document.RootElement.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString();
        }

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

    private sealed class NaturalLanguageSearchPayload
    {
        public string? Intent { get; set; }

        public string? Scope { get; set; }

        public double Confidence { get; set; }

        public string? RequestStatus { get; set; }

        public string? VisitStatus { get; set; }

        public string? DogStatus { get; set; }

        public string? ShelterApplicationStatus { get; set; }

        public string? DogName { get; set; }

        public string? ShelterName { get; set; }

        public string? City { get; set; }

        public string? ResourceCategory { get; set; }

        public bool LowStockOnly { get; set; }

        public bool NoRequestsOnly { get; set; }

        public double? OlderThanDays { get; set; }

        public string? DateFrom { get; set; }

        public string? DateTo { get; set; }

        public string? DateLabel { get; set; }

        public string? SortField { get; set; }

        public string? SortDirection { get; set; }

        public int Limit { get; set; }

        public bool NeedsClarification { get; set; }

        public string? ClarificationQuestion { get; set; }

        public string? Explanation { get; set; }

        public NaturalLanguageSearchInterpretation ToInterpretation()
        {
            var interpretation = new NaturalLanguageSearchInterpretation
            {
                Confidence = Math.Clamp(Confidence, 0, 1),
                DogName = DogName,
                ShelterName = ShelterName,
                City = City,
                ResourceCategory = ResourceCategory,
                LowStockOnly = LowStockOnly,
                NoRequestsOnly = NoRequestsOnly,
                OlderThanDays = OlderThanDays.HasValue ? Math.Max(0, (int)Math.Round(OlderThanDays.Value)) : null,
                SortField = SortField,
                SortDirection = string.Equals(SortDirection, "Ascending", StringComparison.OrdinalIgnoreCase)
                    ? NaturalLanguageSearchSortDirection.Ascending
                    : NaturalLanguageSearchSortDirection.Descending,
                Limit = Limit,
                NeedsClarification = NeedsClarification,
                ClarificationQuestion = ClarificationQuestion,
                Explanation = Explanation ?? "OpenAI interpreted the query as a supported PawConnect search.",
                UsedAi = true
            };

            if (Enum.TryParse<NaturalLanguageSearchIntent>(Intent, true, out var intent))
            {
                interpretation.Intent = intent;
            }

            if (Enum.TryParse<NaturalLanguageSearchScope>(Scope, true, out var scope))
            {
                interpretation.Scope = scope;
            }

            if (Enum.TryParse<AdoptionRequestStatus>(RequestStatus, true, out var requestStatus))
            {
                interpretation.RequestStatus = requestStatus;
            }

            if (Enum.TryParse<AdoptionVisitStatus>(VisitStatus, true, out var visitStatus))
            {
                interpretation.VisitStatus = visitStatus;
            }

            if (Enum.TryParse<DogStatus>(DogStatus, true, out var dogStatus))
            {
                interpretation.DogStatus = dogStatus;
            }

            if (Enum.TryParse<ShelterRegistrationRequestStatus>(ShelterApplicationStatus, true, out var shelterStatus))
            {
                interpretation.ShelterApplicationStatus = shelterStatus;
            }

            interpretation.DateRange = new NaturalLanguageSearchDateRange(
                ParseDate(DateFrom),
                ParseDate(DateTo),
                DateLabel);

            return interpretation;
        }

        private static DateTime? ParseDate(string? value)
        {
            return DateTime.TryParse(value, out var date) ? date.ToUniversalTime() : null;
        }
    }
}
