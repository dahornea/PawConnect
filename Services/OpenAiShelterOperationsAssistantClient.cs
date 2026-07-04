using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PawConnect.Services;

public class OpenAiShelterOperationsAssistantClient(
    HttpClient httpClient,
    IOptions<OpenAiSettings> options,
    ILogger<OpenAiShelterOperationsAssistantClient> logger) : IOpenAiShelterOperationsAssistantClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OpenAiShelterOperationsAssistantResponse> GenerateBriefAsync(
        ShelterOperationsBriefInputDto input,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.ShelterOperationsAssistantEnabled || !settings.HasApiKey)
        {
            return OpenAiShelterOperationsAssistantResponse.Failed("OpenAI shelter operations assistant is disabled or not configured.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());
            httpRequest.Content = JsonContent.Create(new
            {
                model = settings.GetSafeShelterOperationsAssistantModel(),
                input = BuildInput(input),
                text = new
                {
                    format = BuildResponseFormat()
                }
            }, options: JsonOptions);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenAI shelter operations assistant request failed with status {StatusCode}.", response.StatusCode);
                return OpenAiShelterOperationsAssistantResponse.Failed("OpenAI shelter operations assistant request failed.");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var outputText = ExtractOutputText(responseJson);
            var payload = DeserializePayload(outputText);
            if (payload is null)
            {
                return OpenAiShelterOperationsAssistantResponse.Failed("OpenAI shelter operations assistant response was not valid JSON.");
            }

            return OpenAiShelterOperationsAssistantResponse.Successful(ToBrief(payload));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "OpenAI shelter operations assistant generation failed.");
            return OpenAiShelterOperationsAssistantResponse.Failed("OpenAI shelter operations assistant generation failed.");
        }
    }

    private static object[] BuildInput(ShelterOperationsBriefInputDto input)
    {
        return
        [
            new
            {
                role = "system",
                content = """
                You write a concise daily operations brief for PawConnect shelter staff.
                PawConnect already calculated every count, priority, category, and link.
                Do not invent facts, numbers, entity IDs, adoption decisions, or hidden data.
                Do not tell the shelter to automatically accept, reject, confirm, cancel, send, or modify anything.
                Suggest only manual review actions based on the provided priority items.
                Do not mention adopter contact details, private profiles, secrets, SQL, tokens, credentials, or raw logs.
                Return valid JSON only.
                """
            },
            new
            {
                role = "user",
                content = JsonSerializer.Serialize(input, JsonOptions)
            }
        ];
    }

    private static object BuildResponseFormat()
    {
        return new
        {
            type = "json_schema",
            name = "pawconnect_shelter_operations_brief",
            strict = true,
            schema = new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    executiveSummary = StringSchema("One concise paragraph based only on provided operations data."),
                    priorityItems = new
                    {
                        type = "array",
                        description = "Optional rewritten text for backend-provided priority items. Keep the same title, priority, and category.",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                priority = StringSchema("Critical, High, Medium, Low, or Info."),
                                category = StringSchema("One provided category."),
                                title = StringSchema("Exact backend-provided priority item title."),
                                description = StringSchema("A concise description based only on provided facts."),
                                suggestedAction = StringSchema("Manual action suggestion only.")
                            },
                            required = new[] { "priority", "category", "title", "description", "suggestedAction" }
                        }
                    },
                    suggestedActions = ArraySchema("Manual next steps supported by backend data."),
                    warnings = ArraySchema("Short caveats about advisory nature, low stock, reports, or missing data."),
                    limitations = ArraySchema("Short limitations such as this being based only on PawConnect data.")
                },
                required = new[] { "executiveSummary", "priorityItems", "suggestedActions", "warnings", "limitations" }
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

    private static ShelterOperationsAiBriefDto ToBrief(OpenAiShelterOperationsPayload payload)
    {
        return new ShelterOperationsAiBriefDto(
            payload.ExecutiveSummary ?? string.Empty,
            payload.PriorityItems
                .Select(item => new ShelterOperationsAiPriorityItemDto(
                    item.Priority ?? string.Empty,
                    item.Category ?? string.Empty,
                    item.Title ?? string.Empty,
                    item.Description ?? string.Empty,
                    item.SuggestedAction ?? string.Empty))
                .ToList(),
            payload.SuggestedActions,
            payload.Warnings,
            payload.Limitations);
    }

    private static OpenAiShelterOperationsPayload? DeserializePayload(string? outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return null;
        }

        return JsonSerializer.Deserialize<OpenAiShelterOperationsPayload>(outputText.Trim(), JsonOptions);
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

    private sealed class OpenAiShelterOperationsPayload
    {
        public string? ExecutiveSummary { get; set; }

        public List<OpenAiShelterOperationsPriorityPayload> PriorityItems { get; set; } = [];

        public List<string> SuggestedActions { get; set; } = [];

        public List<string> Warnings { get; set; } = [];

        public List<string> Limitations { get; set; } = [];
    }

    private sealed class OpenAiShelterOperationsPriorityPayload
    {
        public string? Priority { get; set; }

        public string? Category { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public string? SuggestedAction { get; set; }
    }
}
