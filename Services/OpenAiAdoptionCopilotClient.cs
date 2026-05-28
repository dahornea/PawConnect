using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PawConnect.Services;

public class OpenAiAdoptionCopilotClient(
    HttpClient httpClient,
    IOptions<OpenAiSettings> options,
    ILogger<OpenAiAdoptionCopilotClient> logger) : IOpenAiAdoptionCopilotClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OpenAiAdoptionCopilotResponse> AskWithToolsAsync(
        AdoptionCopilotToolOpenAiRequest request,
        OpenAiCopilotToolExecutor executeToolAsync,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.HasApiKey)
        {
            return OpenAiAdoptionCopilotResponse.Failed("OpenAI copilot is disabled or not configured.");
        }

        var input = new List<object>
        {
            new
            {
                role = "system",
                content = """
                You are PawConnect's adoption copilot.
                Use PawConnect tools to retrieve real dog data before recommending dogs.
                Your role is to first identify the adopter's primary real-life adoption constraint, then express it as structured tool arguments.
                Follow this flow: intent analysis, then compare each dog against evidence provided by PawConnect, then choose display tags that are directly supported by that evidence.
                PawConnect application code performs the actual filtering, scoring, and validation.
                Never invent dogs, dog IDs, statuses, shelters, or private facts.
                Only recommend dog IDs returned by PawConnect tools.
                Respect exact user constraints such as size, coat color, neighborhood, city, status, age, and radius.
                Use primaryIntent, homeType, activityLevel, compatibilityTarget, compatibility, desiredTraits, avoidTraits, evidenceToLookFor, mustHave, niceToHave, and avoid to express user intent.
                Use coatColors for visible coat-color requests such as black dog, white small dog, brown Labrador, golden dog, tricolor dog, or black and tan dog.
                Keep home/lifestyle/compatibility separate: apartment is Home, low activity is Lifestyle, cats/children/other dogs are Compatibility.
                For "I have a cat at home", set primaryIntent=Compatibility, compatibilityTarget=Cats, desiredTraits around calm/redirectable cats, and avoid chase behavior.
                For "I have kids", set primaryIntent=Compatibility and compatibilityTarget=Children or OlderChildren.
                If the adopter says they already have an older dog at home, represent that as Compatibility with compatibilityTarget=SeniorDog.
                If the adopter says they already have a sick, recovering, anxious, shy, fragile, or sensitive dog at home, represent that as Compatibility with compatibilityTarget=SensitiveDog.
                If the adopter says they already have a young or playful dog at home, represent that as Compatibility with compatibilityTarget=YoungDog.
                For older/sick/anxious household dogs, prefer calm dog company, respectful introductions, gentle play style, and slow introductions; avoid very energetic or pushy candidates.
                For young/playful household dogs, prefer candidates with playful dog friends, active owner fit, and respectful introductions.
                If no exact matches are returned, say so and suggest broadening the request.
                Do not make adoption decisions or create adoption requests.
                Do not mention raw tool calls, JSON, databases, or internal implementation details.
                Write natural, concise, adopter-friendly explanations.
                Keep assistantMessage to one short sentence plus one brief review reminder if useful; do not repeat every interpreted filter because the UI shows chips.
                Choose dogs based on evidence matching the user's main intent, not generic positive traits.
                Candidate dogs include positiveEvidence, cautionEvidence, negativeEvidence, and missingEvidence. Use each item's strength, sourceField, and matchedText when deciding confidence.
                Use only displayTags and cautionTags returned by PawConnect tools. Do not invent tags.
                Do not use generic positive traits as enough evidence for compatibility requests.
                Do not invent evidence. If compatibility evidence is missing, use an "Ask shelter about..." tag and lower confidence.
                Use match labels conservatively: Strong match, Good match, Possible match, or Low match.
                For simple filter-only requests, such as black and tan dogs or medium dogs in Zorilor, prefer Exact match or another PawConnect filter label and do not overstate lifestyle compatibility.
                For SeniorDog or SensitiveDog requests, Strong match requires direct dog-to-dog evidence such as calm dog company or respectful around dogs.
                If a SeniorDog or SensitiveDog candidate only has indirect calm evidence, missing dog compatibility evidence, or an "Ask shelter about..." tag, use Good match or Possible match.
                Reserved dogs and dogs needing slow introductions should be described cautiously, not as certain fits.
                Do not assign apartment/low-activity tags unless the dog evidence includes short walks, indoor rest, quiet routine, small/medium size, or settles quickly.
                Do not assign cats/children/other-dogs tags unless the dog evidence supports them.
                If evidence for a compatibility target is missing, prefer the PawConnect "Ask shelter about..." tag and keep confidence conservative.
                If evidence is weak, use "Possible match" or "Low match", or do not select the dog.
                Reserved dogs may be selected but must include the Reserved caution tag when it is available.
                Return valid JSON only with this shape:
                {"assistantMessage":"Nala looks like the strongest fit. Review each profile before sending a request.","results":[{"dogId":1,"rank":1,"matchLabel":"Good match","scorePercent":76,"displayTags":["Short walks","Indoor rest"],"cautionTags":[],"shortSelectionRationale":"Evidence points to short walks and indoor rest.","reasons":["Short walks","Indoor rest"],"suggestedNextAction":"View the profile to confirm the energy level and shelter details."}]}
                """
            },
            new
            {
                role = "user",
                content = JsonSerializer.Serialize(new
                {
                    request.UserMessage,
                    request.DeterministicConstraints
                }, JsonOptions)
            }
        };

        try
        {
            var toolNames = new List<string>();
            for (var step = 0; step < 4; step++)
            {
                var responseJson = await SendResponsesRequestAsync(
                    settings,
                    input,
                    toolChoice: step == 0 ? "required" : "auto",
                    cancellationToken);

                var toolCalls = ExtractToolCalls(responseJson);
                if (toolCalls.Count == 0)
                {
                    var payload = DeserializePayload(ExtractOutputText(responseJson));
                    if (payload is null)
                    {
                        return OpenAiAdoptionCopilotResponse.Failed("OpenAI response did not include copilot JSON.");
                    }

                    var items = payload.Results
                        .Where(item => item.DogId > 0)
                        .Select(item => new OpenAiAdoptionCopilotItem(
                            item.DogId,
                            item.Rank <= 0 ? int.MaxValue : item.Rank,
                            NormalizeMatchLabel(item.MatchLabel),
                            Math.Clamp(item.ScorePercent, 25, 92),
                            NormalizeReasons(item.Reasons),
                            SafeTrim(item.SuggestedNextAction, 160) ?? "View the dog profile and check the shelter details.",
                            NormalizeReasons(item.DisplayTags),
                            NormalizeReasons(item.CautionTags),
                            SafeTrim(item.ShortSelectionRationale, 180)))
                        .ToList();

                    logger.LogInformation(
                        "OpenAI adoption copilot completed with tools {ToolNames} and {ResultCount} returned dog IDs.",
                        string.Join(", ", toolNames.Distinct(StringComparer.OrdinalIgnoreCase)),
                        items.Count);

                    return new OpenAiAdoptionCopilotResponse(
                        items.Count > 0,
                        SafeTrim(payload.AssistantMessage, 260),
                        items,
                        items.Count > 0 ? null : "OpenAI response did not include usable results.");
                }

                foreach (var toolCall in toolCalls)
                {
                    toolNames.Add(toolCall.Name);
                    input.Add(new
                    {
                        type = "function_call",
                        call_id = toolCall.CallId,
                        name = toolCall.Name,
                        arguments = toolCall.ArgumentsJson
                    });

                    var output = await executeToolAsync(toolCall, cancellationToken);
                    input.Add(new
                    {
                        type = "function_call_output",
                        call_id = toolCall.CallId,
                        output = output.OutputJson
                    });
                }
            }

            return OpenAiAdoptionCopilotResponse.Failed("OpenAI tool-calling loop did not finish.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "OpenAI adoption copilot tool-calling failed.");
            return OpenAiAdoptionCopilotResponse.Failed("OpenAI copilot failed.");
        }
    }

    private async Task<string> SendResponsesRequestAsync(
        OpenAiSettings settings,
        IReadOnlyList<object> input,
        string toolChoice,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());
        httpRequest.Content = JsonContent.Create(new
        {
            model = settings.GetSafeChatModel(),
            input,
            tools = BuildTools(),
            tool_choice = toolChoice,
            parallel_tool_calls = false,
            text = new
            {
                format = BuildResponseFormat()
            }
        }, options: JsonOptions);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OpenAI adoption copilot request failed with status {StatusCode}.", response.StatusCode);
            throw new HttpRequestException("OpenAI request failed.");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static object BuildResponseFormat()
    {
        return new
        {
            type = "json_schema",
            name = "adoption_copilot_response",
            strict = true,
            schema = new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    assistantMessage = StringSchema("One concise adopter-friendly summary sentence."),
                    results = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                dogId = NumberSchema("A dog ID from the candidate list."),
                                rank = NumberSchema("1-based rank among selected candidates."),
                                matchLabel = StringSchema("Strong match, Good match, Possible match, Low match, or a PawConnect filter label such as Exact match or Matches request."),
                                scorePercent = NumberSchema("Conservative evidence-based score from 25 to 86."),
                                displayTags = ArraySchema("Supported display tags copied from the candidate only."),
                                cautionTags = ArraySchema("Supported caution tags copied from the candidate only."),
                                shortSelectionRationale = StringSchema("Short internal rationale based only on provided evidence."),
                                reasons = ArraySchema("Short reasons copied from supported tags or evidence."),
                                suggestedNextAction = StringSchema("Short user-facing next action.")
                            },
                            required = new[]
                            {
                                "dogId",
                                "rank",
                                "matchLabel",
                                "scorePercent",
                                "displayTags",
                                "cautionTags",
                                "shortSelectionRationale",
                                "reasons",
                                "suggestedNextAction"
                            }
                        }
                    }
                },
                required = new[] { "assistantMessage", "results" }
            }
        };
    }

    private static object[] BuildTools()
    {
        return
        [
            new
            {
                type = "function",
                name = "search_dogs",
                description = "Search public-safe PawConnect dogs using structured filters. Returns only real Available or Reserved dogs.",
                parameters = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        query = StringSchema("Natural language search query."),
                        primaryIntent = StringSchema("Primary user intent: HomeSuitability, ActivityLevel, Temperament, Compatibility, Location, Size, or ExperienceLevel."),
                        sizes = ArraySchema("Dog sizes such as Small, Medium, or Large."),
                        breeds = ArraySchema("Breed names or breed fragments."),
                        coatColors = ArraySchema("Coat colors such as Black, White, Brown, Golden, Tricolor, Black and tan, or Brown and white. Use for requests like black dog, white small dog, brown Labrador, or tricolor dog."),
                        city = StringSchema("Shelter city or dog location city."),
                        neighborhood = StringSchema("Shelter neighborhood/cartier such as Zorilor or Manastur."),
                        shelterName = StringSchema("Shelter name."),
                        maxAgeYears = NumberSchema("Maximum age in years. Omit this field unless the user explicitly asks for an upper age limit; never use 0 as a placeholder."),
                        minAgeYears = NumberSchema("Minimum age in years. Use for senior/older/at least/over age requests only."),
                        ageComparison = StringSchema("Use Under for strict phrases like 'under 2' or 'younger than 2'; Max for inclusive phrases like 'max 2' or '2 years or younger'; AtLeast for older/senior minimum-age requests."),
                        statuses = ArraySchema("Dog statuses. Public-safe values are Available and Reserved. If the user asks specifically for reserved or available dogs, pass only that requested status."),
                        behaviorTerms = ArraySchema("Temperament/behavior terms such as calm, gentle, friendly, social, patient, shy, or anxious. Do not put apartment, flat, house, yard, garden, cats, children, dogs, short walks, daily walks, or longer walks here."),
                        temperamentTags = ArraySchema("Normalized temperament tags such as Calm, Gentle, Friendly, Playful, Shy, or Anxious. Do not put walk/activity preferences here; use activityLevel/energyLevel and mustHave for short walks, daily walks, longer walks, or low/moderate/high activity."),
                        temperaments = ArraySchema("Structured temperament values: Calm, Gentle, Friendly, Playful, Shy, or Anxious. Do not put apartment, cats, children, yard, short walks, daily walks, longer walks, or activity level here."),
                        energyLevel = StringSchema("Lifestyle energy level inferred from the request: Low for low-activity/short-walk requests, Medium, or High for active/outdoor requests."),
                        activityLevel = StringSchema("Structured activity level: Low, Medium, High, or Any."),
                        homeType = StringSchema("Home setting inferred from the request, such as Apartment or House with yard. Do not also put this value in behaviorTerms."),
                        housingPreference = StringSchema("Housing preference such as Apartment or House with yard."),
                        apartmentFriendly = BooleanSchema("Whether the user asks for an apartment-friendly dog."),
                        yardFriendly = BooleanSchema("Whether the request favors a dog suited to a house, garden, or yard."),
                        yardRequired = BooleanSchema("Whether the user explicitly requires a yard."),
                        needsYard = BooleanSchema("Whether the request implies a yard is needed or preferred."),
                        goodWithChildren = BooleanSchema("Whether the request asks for family/children suitability."),
                        goodWithPets = BooleanSchema("Whether the request asks for other-pet suitability."),
                        compatibility = ArraySchema("Compatibility values: Children, OlderChildren, Cats, OtherDogs, SeniorDog, YoungDog, SickDog, AnxiousDog, or SmallAnimals. Use SeniorDog/YoungDog/SickDog/AnxiousDog only when the adopter says they already have that kind of dog at home."),
                        compatibilityTarget = StringSchema("Main compatibility target: Children, OlderChildren, Cats, OtherDogs, SeniorDog, SensitiveDog, YoungDog, SmallAnimals, or null."),
                        experienceLevel = StringSchema("Adopter experience level: Beginner, Experienced, or Any."),
                        desiredTraits = ArraySchema("Intent-level desired traits such as calm near cats, respectful introductions, gentle play style, short walks, or outdoor play."),
                        mustHave = ArraySchema("Specific evidence the user appears to require, such as short walks, indoor rest, calm around cats, older children, or outdoor play."),
                        niceToHave = ArraySchema("Helpful evidence signals, such as settles quickly, gentle handling, slow introductions, or training games."),
                        avoidTraits = ArraySchema("Intent-level traits to avoid such as chase behavior, pushy dogs, very energetic, rough play, or noisy children."),
                        avoid = ArraySchema("Evidence to avoid, such as very energetic, chase behavior, not suitable for young/noisy children, or too interested in fast-moving small animals."),
                        evidenceToLookFor = ArraySchema("Public dog evidence to look for: shelter cats, slow introductions, calm canine company, supervised visits with children, short walks, indoor rest, outdoor play, etc."),
                        displayChipIntent = ArraySchema("Short chip concepts for the UI, matching interpreted user intent rather than dog evidence."),
                        nearLocationText = StringSchema("City or address for nearby search. Use only when the user asks near/around a location."),
                        radiusKm = NumberSchema("Nearby search radius in kilometers."),
                        sort = StringSchema("Sort order such as nearest or best_match."),
                        limit = NumberSchema("Maximum number of dogs to return. Prefer 6 unless the user asks for a different count."),
                        count = NumberSchema("Maximum number of dogs to return.")
                    }
                }
            },
            new
            {
                type = "function",
                name = "get_adopter_profile_summary",
                description = "Get a sanitized summary for the current adopter only. Does not accept a user id.",
                parameters = EmptyObjectSchema()
            },
            new
            {
                type = "function",
                name = "get_favorite_and_recent_preferences",
                description = "Get aggregate favorite and recently viewed dog preferences for the current adopter only.",
                parameters = EmptyObjectSchema()
            },
            new
            {
                type = "function",
                name = "get_dog_details_public",
                description = "Fetch public-safe details for one dog ID that was already returned by PawConnect tools.",
                parameters = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        dogId = NumberSchema("The PawConnect dog ID.")
                    },
                    required = new[] { "dogId" }
                }
            }
        ];
    }

    private static object EmptyObjectSchema()
    {
        return new
        {
            type = "object",
            additionalProperties = false,
            properties = new { }
        };
    }

    private static object StringSchema(string description)
    {
        return new { type = "string", description };
    }

    private static object NumberSchema(string description)
    {
        return new { type = "number", description };
    }

    private static object BooleanSchema(string description)
    {
        return new { type = "boolean", description };
    }

    private static object ArraySchema(string description)
    {
        return new
        {
            type = "array",
            description,
            items = new { type = "string" }
        };
    }

    private static IReadOnlyList<OpenAiCopilotToolCall> ExtractToolCalls(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (!document.RootElement.TryGetProperty("output", out var outputElement) ||
            outputElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var calls = new List<OpenAiCopilotToolCall>();
        foreach (var item in outputElement.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var typeElement) ||
                typeElement.GetString() != "function_call")
            {
                continue;
            }

            var callId = item.TryGetProperty("call_id", out var callIdElement)
                ? callIdElement.GetString()
                : null;
            var name = item.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;
            var arguments = item.TryGetProperty("arguments", out var argumentsElement)
                ? argumentsElement.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(callId) && !string.IsNullOrWhiteSpace(name))
            {
                calls.Add(new OpenAiCopilotToolCall(callId!, name!, arguments ?? "{}"));
            }
        }

        return calls;
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

        if (!root.TryGetProperty("output", out var outputElement) ||
            outputElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

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

        return null;
    }

    private static OpenAiAdoptionCopilotPayload? DeserializePayload(string? outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return null;
        }

        var trimmed = outputText.Trim();
        var jsonStart = trimmed.IndexOf('{');
        var jsonEnd = trimmed.LastIndexOf('}');
        if (jsonStart > 0 || jsonEnd < trimmed.Length - 1)
        {
            trimmed = jsonStart >= 0 && jsonEnd >= jsonStart
                ? trimmed[jsonStart..(jsonEnd + 1)]
                : trimmed;
        }

        return JsonSerializer.Deserialize<OpenAiAdoptionCopilotPayload>(trimmed, JsonOptions);
    }

    private static string NormalizeMatchLabel(string? label)
    {
        return label?.Trim() switch
        {
            "Excellent match" => "Strong match",
            "Strong match" => "Strong match",
            "Good match" => "Good match",
            "Possible match" => "Possible match",
            "Potential match" => "Possible match",
            "Low match" => "Low match",
            "Weak match" => "Low match",
            "Exact match" => "Exact match",
            "Matches request" => "Matches request",
            "Exact filter match" => "Exact filter match",
            "Matching result" => "Matching result",
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

    private static string? SafeTrim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private sealed class OpenAiAdoptionCopilotPayload
    {
        public string? AssistantMessage { get; set; }

        public List<OpenAiAdoptionCopilotPayloadItem> Results { get; set; } = [];
    }

    private sealed class OpenAiAdoptionCopilotPayloadItem
    {
        public int DogId { get; set; }

        public int Rank { get; set; }

        public string? MatchLabel { get; set; }

        public int ScorePercent { get; set; }

        public List<string>? Reasons { get; set; }

        public List<string>? DisplayTags { get; set; }

        public List<string>? CautionTags { get; set; }

        public string? ShortSelectionRationale { get; set; }

        public string? SuggestedNextAction { get; set; }
    }
}
