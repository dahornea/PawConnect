using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PawConnect.Services;

public class OpenAiReportSummaryClient(
    HttpClient httpClient,
    IOptions<OpenAiSettings> options,
    ILogger<OpenAiReportSummaryClient> logger) : IOpenAiReportSummaryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OpenAiReportSummaryResponse> GenerateSummaryAsync(
        AiReportSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.ReportSummariesEnabled || !settings.HasApiKey)
        {
            return OpenAiReportSummaryResponse.Failed("OpenAI report summaries are disabled or not configured.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());
            httpRequest.Content = JsonContent.Create(new
            {
                model = settings.GetSafeReportSummaryModel(),
                input = BuildInput(request),
                text = new
                {
                    format = BuildResponseFormat()
                }
            }, options: JsonOptions);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenAI report summary request failed with status {StatusCode}.", response.StatusCode);
                return OpenAiReportSummaryResponse.Failed("OpenAI report summary request failed.");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var outputText = ExtractOutputText(responseJson);
            var payload = DeserializePayload(outputText);
            if (payload is null)
            {
                return OpenAiReportSummaryResponse.Failed("OpenAI report summary response was not valid JSON.");
            }

            return OpenAiReportSummaryResponse.Successful(ToResult(payload));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "OpenAI report summary generation failed.");
            return OpenAiReportSummaryResponse.Failed("OpenAI report summary generation failed.");
        }
    }

    private static object[] BuildInput(AiReportSummaryRequest request)
    {
        return
        [
            new
            {
                role = "system",
                content = """
                You summarize PawConnect report metrics for shelter staff.
                PawConnect already calculated every number in the input.
                Do not invent numbers, trends, causes, or hidden data.
                Do not infer data that is not present.
                If a metric is missing, do not mention it.
                Keep the tone professional, concise, and practical.
                Return valid JSON only.
                """
            },
            new
            {
                role = "user",
                content = JsonSerializer.Serialize(new
                {
                    request.ReportType,
                    metrics = request.ShelterMetrics
                }, JsonOptions)
            }
        ];
    }

    private static object BuildResponseFormat()
    {
        return new
        {
            type = "json_schema",
            name = "pawconnect_report_summary",
            strict = true,
            schema = new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    title = StringSchema("Short report summary title."),
                    executiveSummary = StringSchema("One concise paragraph based only on the provided metrics."),
                    keyHighlights = ArraySchema("Important metric-backed highlights."),
                    warnings = ArraySchema("Metric-backed warnings, such as low stock."),
                    suggestedActions = ArraySchema("Practical next steps supported by the metrics."),
                    limitations = ArraySchema("Short caveats about the summary being based only on report metrics.")
                },
                required = new[]
                {
                    "title",
                    "executiveSummary",
                    "keyHighlights",
                    "warnings",
                    "suggestedActions",
                    "limitations"
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

    private static AiReportSummaryResult ToResult(OpenAiReportSummaryPayload payload)
    {
        return new AiReportSummaryResult(
            payload.Title ?? "Report Summary",
            payload.ExecutiveSummary ?? string.Empty,
            payload.KeyHighlights,
            payload.Warnings,
            payload.SuggestedActions,
            payload.Limitations,
            UsedAi: true,
            FallbackReason: null);
    }

    private static OpenAiReportSummaryPayload? DeserializePayload(string? outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return null;
        }

        return JsonSerializer.Deserialize<OpenAiReportSummaryPayload>(outputText.Trim(), JsonOptions);
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

    private sealed class OpenAiReportSummaryPayload
    {
        public string? Title { get; set; }

        public string? ExecutiveSummary { get; set; }

        public List<string> KeyHighlights { get; set; } = [];

        public List<string> Warnings { get; set; } = [];

        public List<string> SuggestedActions { get; set; } = [];

        public List<string> Limitations { get; set; } = [];
    }
}
