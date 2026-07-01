# PawConnect Adoption Copilot - 20 Step Code Flow Demo Guide

This file is a practical guide for explaining the Adoption Copilot during the thesis defense.

Use it when the committee asks:

- how the Copilot works from UI to backend
- where OpenAI is used
- how PawConnect prevents hallucinated dogs
- how dog results are filtered, scored, and displayed
- how fallback behavior works

The shortest explanation is:

> The UI sends the adopter prompt to `AdoptionCopilotService.AskAsync`. PawConnect first detects obvious constraints, searches real public-safe dogs, scores them using backend evidence, optionally asks OpenAI to help interpret and explain the results, validates all returned dog IDs, and finally sends safe dog cards back to the UI.

---

## Files To Open During The Demo

| Step | File | Why open it |
|---|---|---|
| 1 | `Components/Pages/Adopter/AdoptionCopilot.razor` | Shows the UI and where the prompt is submitted |
| 2 | `Services/AdoptionCopilotService.cs` | Main backend orchestrator |
| 3 | `Services/AdoptionCopilotToolService.cs` | Real dog search, hard filters, evidence, scoring |
| 4 | `Services/OpenAiAdoptionCopilotClient.cs` | OpenAI Responses API call, tools, strict JSON |
| 5 | `Services/SemanticDogSearchService.cs` | Optional semantic search support |

---

## Complete Flow In 20 Steps

## 1. The User Clicks Ask Copilot

File:

`Components/Pages/Adopter/AdoptionCopilot.razor`

Start around:

```csharp
private async Task AskCopilotAsync()
{
    if (string.IsNullOrWhiteSpace(_currentUserId))
    {
        _error = "Current adopter account could not be found.";
        return;
    }

    if (string.IsNullOrWhiteSpace(_query))
    {
        _error = "Describe the kind of dog you are looking for.";
        return;
    }
```

What this does:

- checks that the current user is known
- checks that the prompt is not empty
- prevents sending invalid requests to the backend

The important call:

```csharp
_response = await AdoptionCopilotService.AskAsync(_currentUserId, _query);
```

What to say:

> The Blazor component does not calculate matches itself. When the adopter clicks Ask Copilot, the page sends the current adopter ID and the natural-language query to the backend service.

Live script:

> The Copilot starts on this page, where the adopter writes what kind of dog they are looking for. When I press Ask Copilot, the page sends two things to the backend: the user's message and the current adopter account.

Next method:

`AdoptionCopilotService.AskAsync`

---

## 2. The Main Backend Method Starts

File:

`Services/AdoptionCopilotService.cs`

Method:

```csharp
public async Task<AdoptionCopilotResponse> AskAsync(
    string adopterUserId,
    string userMessage,
    CancellationToken cancellationToken = default)
{
    var query = userMessage.Trim();
```

What this does:

- this is the main Copilot orchestrator
- it receives the adopter ID and prompt
- it normalizes the text by trimming it
- it decides whether to return fallback results or call OpenAI

The next important call:

```csharp
var deterministicArgs = await BuildDeterministicSearchArgsAsync(query, cancellationToken);
```

What to say:

> The main backend method starts by converting the free-text prompt into structured search arguments. This happens before OpenAI is used.

Live script:

> From there, the main service is `AdoptionCopilotService`. This is the central method for the Copilot. It receives the user's message, cleans it up, and starts deciding how the search should be handled.

Next method:

`BuildDeterministicSearchArgsAsync`

---

## 3. PawConnect Detects Obvious Constraints Without AI

File:

`Services/AdoptionCopilotService.cs`

Method:

```csharp
private async Task<AdoptionCopilotSearchDogsArgs> BuildDeterministicSearchArgsAsync(
    string query,
    CancellationToken cancellationToken)
{
    var sizes = DetectSizes(query);
    var coatColors = DogCoatColorOptions.DetectInText(query);
    var statuses = DetectStatuses(query);
    var ageConstraint = DetectAgeConstraint(query);
    var neighborhood = await DetectExplicitNeighborhoodAsync(query, cancellationToken);
    var behaviorTerms = DetectBehaviorTerms(query);
    var temperamentTags = DetectTemperamentTags(query);
    var homeType = DetectHomeType(query);
    var activityLevel = DetectEnergyLevel(query) ?? DetectHouseholdDogActivityLevel(query);
    var compatibility = DetectCompatibility(query);
```

What this does:

- detects size, for example small, medium, large
- detects coat color, for example black, white, black and tan
- detects status, for example available or reserved
- detects age constraints
- detects neighborhood
- detects home type, for example apartment or yard
- detects activity level
- detects compatibility needs, for example cats, children, senior dog, sensitive dog

Then it builds one structured argument object:

```csharp
return new AdoptionCopilotSearchDogsArgs
{
    Query = query,
    PrimaryIntent = primaryIntent,
    Sizes = sizes.Count > 0 ? sizes : null,
    CoatColors = coatColors.Count > 0 ? coatColors.ToList() : null,
    Statuses = statuses.Count > 0 ? statuses : [DogStatus.Available.ToString(), DogStatus.Reserved.ToString()],
    City = city,
    Neighborhood = neighborhood,
    Compatibility = compatibility.Count > 0 ? compatibility : null,
    CompatibilityTarget = compatibilityTarget,
    EnergyLevel = activityLevel,
    ActivityLevel = activityLevel,
    HomeType = homeType,
    Count = 16,
    Limit = 16
};
```

What to say:

> This is the deterministic parser. It handles clear constraints directly in C#, so exact filters do not depend only on the AI model. For example, "black and tan dogs" becomes a coat color filter, and "apartment but longer walks" becomes home type Apartment plus an activity preference.

Live script:

> The first thing the backend does is try to understand simple things by itself. For example, if I write "black dog", it detects the coat color. If I write "small dog", it detects the size. If I write "apartment", it detects the home type. So the app does not depend only on AI for basic filters.

Next method:

Back to `AskAsync`.

---

## 4. PawConnect Builds A Safe Fallback Search

File:

`Services/AdoptionCopilotService.cs`

Back inside `AskAsync`, PawConnect calls:

```csharp
var fallbackSearch = await toolService.SearchDogsAsync(adopterUserId, deterministicArgs, cancellationToken);
var fallback = BuildFallbackResponse(query, fallbackSearch, "AI assistance is unavailable right now, so PawConnect used safe rule-based search.");
```

What this does:

- before calling OpenAI, PawConnect already searches real dogs itself
- this gives the app a safe fallback
- it also creates a candidate list that OpenAI is allowed to talk about later

What to say:

> PawConnect always prepares backend results first. This means the Copilot can still return results if OpenAI is disabled or unavailable.

Live script:

> After that, PawConnect searches its own database for real dogs. This is important because the Copilot does not start from imaginary AI results. It first creates a safe backend result list.

Next method:

`AdoptionCopilotToolService.SearchDogsAsync`

---

## 5. The Dog Search Tool Starts

File:

`Services/AdoptionCopilotToolService.cs`

Method:

```csharp
public async Task<AdoptionCopilotToolSearchResult> SearchDogsAsync(
    string adopterUserId,
    AdoptionCopilotSearchDogsArgs args,
    CancellationToken cancellationToken = default)
{
    NormalizeOptionalArguments(args);
    var intent = AnalyzeIntent(args);
    var requestedCount = intent.Limit > 0 ? intent.Limit : args.Count;
    var count = Math.Clamp(requestedCount <= 0 ? 16 : Math.Max(requestedCount, 12), 1, 20);
    var appliedConstraints = BuildAppliedConstraints(args, intent);
```

What this code does:

```csharp
NormalizeOptionalArguments(args);
```

Cleans the structured arguments so empty strings, casing, and optional fields are easier to work with.

```csharp
var intent = AnalyzeIntent(args);
```

Turns the arguments into a higher-level intent. For example:

- Compatibility + Cats
- Compatibility + SensitiveDog
- HomeSuitability + Apartment
- ActivityLevel + Longer walks

```csharp
var appliedConstraints = BuildAppliedConstraints(args, intent);
```

Builds the summary chips shown in the UI, such as:

- `Status: Available, Reserved`
- `Home: Apartment`
- `Activity: Longer walks`
- `Compatibility: Cats`

What to say:

> This method is the real backend search tool. It cleans the request, understands the main intent, and prepares the criteria chips before it even queries dogs.

Live script:

> This is the actual search tool used by the Copilot. At this point, the prompt has already been turned into structured search data, and this method prepares the intent and the chips that explain what the app understood.

Next section:

Database query inside the same method.

---

## 6. The Tool Loads Only Public-Safe Dogs

File:

`Services/AdoptionCopilotToolService.cs`

Inside `SearchDogsAsync`:

```csharp
var dogs = await context.Dogs
    .Include(dog => dog.Shelter)
    .Include(dog => dog.DogBreed)
    .Include(dog => dog.SecondaryBreed)
    .Include(dog => dog.Images)
    .AsNoTracking()
    .Where(dog => dog.Status == DogStatus.Available || dog.Status == DogStatus.Reserved)
    .ToListAsync(cancellationToken);
```

What this does:

- loads dogs from the database
- includes related public data:
  - shelter
  - breed
  - secondary breed
  - images
- uses `AsNoTracking()` because this is read-only
- only keeps dogs with status `Available` or `Reserved`

What to say:

> This is the first important safety rule. The Copilot does not start from all dogs. It only loads public-safe dogs: Available and Reserved. Adopted and InTreatment dogs are excluded before any AI ranking or scoring happens.

Live script:

> Here the app loads dogs from the database, but only dogs that are allowed to be shown publicly. So Available and Reserved dogs can appear, while Adopted dogs and dogs in treatment are excluded before any AI ranking happens.

Next section:

Hard filters inside the same method.

---

## 7. Hard Filters Remove Dogs That Do Not Match Explicit Criteria

File:

`Services/AdoptionCopilotToolService.cs`

Inside `SearchDogsAsync`:

```csharp
var hardFiltered = new List<(Dog Dog, double? DistanceKm)>();
foreach (var dog in dogs)
{
    if (!MatchesHardFilters(dog, args, sizes, statuses, coatColors, origin, out var distanceKm))
    {
        continue;
    }

    hardFiltered.Add((dog, distanceKm));
}
```

What this does:

- checks every public-safe dog
- removes dogs that fail exact user constraints
- keeps dogs that pass the hard filters

The called method:

```csharp
MatchesHardFilters(...)
```

Important code inside `MatchesHardFilters`:

```csharp
if (dog.Status is not (DogStatus.Available or DogStatus.Reserved))
{
    return false;
}
```

```csharp
if (sizes.Count > 0 && !sizes.Contains(dog.Size))
{
    return false;
}
```

```csharp
if (coatColors.Count > 0)
{
    var dogCoatColor = DogCoatColorOptions.Normalize(dog.CoatColor);
    if (string.IsNullOrWhiteSpace(dogCoatColor) || !coatColors.Contains(dogCoatColor))
    {
        return false;
    }
}
```

```csharp
if (!string.IsNullOrWhiteSpace(args.Neighborhood) &&
    !string.Equals(dog.Shelter?.Neighborhood, args.Neighborhood.Trim(), StringComparison.OrdinalIgnoreCase))
{
    return false;
}
```

What to say:

> Hard filters are strict. If the user explicitly asks for a medium dog, a black and tan dog, or a dog in Zorilor, dogs that do not satisfy that condition are removed before scoring.

Live script:

> Then the app applies hard filters. If the user asks for a medium dog, large dogs are removed. If the user asks for black and tan dogs, dogs with other coat colors are removed. These filters happen before ranking, so the AI cannot ignore them later.

Next method:

`GetSemanticRankingsAsync`

---

## 8. Semantic Search Can Add Meaning-Based Ranking

File:

`Services/AdoptionCopilotToolService.cs`

Inside `SearchDogsAsync`:

```csharp
var semanticById = await GetSemanticRankingsAsync(adopterUserId, args, origin, count, cancellationToken);
```

Called method:

```csharp
private async Task<Dictionary<int, SemanticDogSearchResult>> GetSemanticRankingsAsync(...)
{
    var options = new SemanticDogSearchOptions
    {
        Size = singleSize,
        Status = singleStatus,
        Neighborhood = EmptyToNull(args.Neighborhood),
        Location = EmptyToNull(args.City),
        CoatColors = args.CoatColors,
        OriginLatitude = origin?.Latitude,
        OriginLongitude = origin?.Longitude,
        RadiusKm = args.RadiusKm
    };

    var query = string.IsNullOrWhiteSpace(args.Query)
        ? string.Join(' ', (args.BehaviorTerms ?? []).Concat(args.TemperamentTags ?? []))
        : args.Query!;

    var results = await semanticDogSearchService.SearchDogsAsync(query, adopterUserId, Math.Max(count, 12), options, cancellationToken);
    return results.ToDictionary(result => result.DogId);
}
```

What this does:

- builds semantic search options from the same constraints
- sends the natural-language query to semantic search
- returns semantic results indexed by dog ID
- if semantic search fails, the method catches the error and returns an empty dictionary

What to say:

> Semantic search is used as an extra ranking signal. It helps match meaning, not only exact words. But it does not replace hard filters.

Live script:

> Semantic search can also help a little. It means the app can compare the meaning of the user's prompt with the dog descriptions, not just exact words. But it only gives a small bonus. The main score still comes from the dog data and the filters.

Next method:

`BuildCandidate`

---

## 9. Each Dog Is Converted Into A Candidate

File:

`Services/AdoptionCopilotToolService.cs`

Inside `SearchDogsAsync`:

```csharp
var candidates = OrderCopilotCandidates(
    hardFiltered
    .Select(item => BuildCandidate(item.Dog, item.DistanceKm, args, intent, queryTerms, semanticById))
    .ToList(),
    intent,
    count)
    .ToList();
```

What this does:

- takes every hard-filtered dog
- calls `BuildCandidate` for each one
- orders the candidates by score and ranking signals
- limits the list

What to say:

> After the hard filters, each remaining dog becomes a Copilot candidate. This is where the dog gets a score, reasons, display tags, caution tags, and next action text.

Live script:

> After the list is filtered, each remaining dog becomes a candidate. This means the app prepares the score, the short reasons, the tags, the cautions, and the next action text for that dog.

Next method:

`BuildCandidate`

---

## 10. BuildCandidate Calculates The Match Score

File:

`Services/AdoptionCopilotToolService.cs`

Method:

```csharp
private static AdoptionCopilotToolDogCandidate BuildCandidate(
    Dog dog,
    double? distanceKm,
    AdoptionCopilotSearchDogsArgs args,
    CopilotIntent intent,
    IReadOnlyList<string> queryTerms,
    IReadOnlyDictionary<int, SemanticDogSearchResult> semanticById)
{
    var score = 34;
    var reasons = new List<string>();
```

What this does:

- starts with a conservative base score
- adds points only for things that can be explained

Examples:

Semantic ranking:

```csharp
if (semanticById.TryGetValue(dog.Id, out var semanticResult))
{
    score += Math.Clamp((semanticResult.ScorePercent - 55) / 7, 0, 5);
}
```

Size match:

```csharp
if (parsedSizes.Contains(dog.Size))
{
    AddReason(reasons, "Size matches your search");
    score += 11;
}
```

Coat color match:

```csharp
if (!string.IsNullOrWhiteSpace(dogCoatColor) && parsedCoatColors.Contains(dogCoatColor))
{
    AddReason(reasons, $"Coat color: {dogCoatColor}");
    score += 11;
}
```

Location match:

```csharp
if (!string.IsNullOrWhiteSpace(args.City) &&
    (Contains(dog.Shelter?.City, args.City) || Contains(dog.Location, args.City)))
{
    AddReason(reasons, $"In {args.City.Trim()}");
    score += 8;
}
```

What to say:

> The score is not a prediction. It is a heuristic score based on visible evidence: size, coat color, location, activity fit, behavior text, and compatibility signals.

Live script:

> Each dog is then scored. The score is not meant to be a perfect prediction. It is just a helpful match indicator. PawConnect adds points for things like matching size, location, coat color, apartment fit, activity level, and behavior evidence from the dog's description.

Next method:

`ExtractDogEvidence`

---

## 11. Dog Evidence Is Extracted From Public Text

File:

`Services/AdoptionCopilotToolService.cs`

Inside `BuildCandidate`:

```csharp
var evidence = ExtractDogEvidence(dog, args, intent, searchableText, safeReasons);
score += CalculateIntentEvidenceScore(intent, evidence, dog.Status);
score = ApplyCompatibilityEvidenceCaps(intent, evidence, score);
score = ApplyHomeActivityEvidenceCaps(intent, evidence, score);
```

Called method:

```csharp
private static CopilotDogEvidence ExtractDogEvidence(
    Dog dog,
    AdoptionCopilotSearchDogsArgs args,
    CopilotIntent intent,
    string searchableText,
    IReadOnlyList<string> safeReasons)
```

Important code:

```csharp
var apartmentRequested = IsApartmentRequest(args);
var calmRequested = IsCalmRequest(args);
var strictLongerWalksRequested = HasExplicitLongerWalksRequest(args);
var seniorOrSensitiveDogAtHome = HasSeniorOrSensitiveHouseholdDogRequest(args);
```

Example display tag:

```csharp
if ((inferLowActivityFromApartment || calmRequested || seniorOrSensitiveDogAtHome) &&
    ContainsAny(searchableText, ["short daily walks", "short walks", "slow walks", "leash walks"]))
{
    AddDisplayTag(displayTags, "Short walks");
}
```

Another example:

```csharp
if ((inferLowActivityFromApartment || calmRequested || seniorOrSensitiveDogAtHome || activityWalksRequested) &&
    ContainsAny(searchableText, ["settles down quickly", "settles quickly", "settles", "settle"]))
{
    AddDisplayTag(displayTags, "Settles quickly");
}
```

What this does:

- reads public dog description and behavior text
- creates tags only when the dog data supports them
- separates positive tags from caution tags
- avoids showing unrelated chips

What to say:

> This is how the Copilot becomes explainable. It does not just return a score. It extracts evidence from the dog profile and uses that evidence for visible chips and scoring confidence.

Live script:

> The app also looks at the text written for each dog. If the description says the dog enjoys short walks, settles indoors, walks calmly near other dogs, or needs slow introductions, PawConnect turns that into visible tags on the card.

Next part:

Back to `BuildCandidate`.

---

## 12. Candidate Score Is Calibrated And Returned

File:

`Services/AdoptionCopilotToolService.cs`

Back in `BuildCandidate`:

```csharp
var safeScore = filterOnlyRequest
    ? GetFilterOnlyScore(dog.Status, reservedOnlyRequest)
    : CalibrateRecommendationScore(score, intent, evidence);
```

Then:

```csharp
if (!filterOnlyRequest)
{
    safeScore = ApplyFinalVisibleDifferentiation(dog, safeScore, intent, evidence);
}
```

Then:

```csharp
var matchLabel = filterOnlyRequest ? "Exact match" : GetMatchLabel(safeScore);
```

Finally:

```csharp
return new AdoptionCopilotToolDogCandidate(
    dog.Id,
    dog,
    safeScore,
    matchLabel,
    safeReasons.Take(3).ToList(),
    BuildSuggestedNextAction(dog, intent, evidence),
    distanceKm,
    evidence.SupportedDisplayTags,
    evidence.CautionEvidence,
    evidence.MissingEvidence);
```

What this does:

- filter-only queries get labels like `Exact match`
- recommendation-style queries get percentage labels
- final differentiation reduces ties when visible evidence differs
- the returned candidate contains real dog data, score, tags, cautions, and reasons

What to say:

> The backend candidate already contains everything needed to display a result, even without OpenAI.

Live script:

> At this point, the backend already has usable results. Even without OpenAI, each candidate has a score, a label, reasons, tags, cautions, and real dog data.

Next part:

Back to `AdoptionCopilotService.AskAsync`.

---

## 13. If OpenAI Is Disabled, The Fallback Is Returned

File:

`Services/AdoptionCopilotService.cs`

Back in `AskAsync`:

```csharp
var settings = openAiOptions.Value;
if (!settings.Enabled || !settings.HasApiKey)
{
    return fallback;
}
```

What this does:

- checks if OpenAI is enabled and has an API key
- if not, returns the backend results

What to say:

> OpenAI is not the source of truth. If it is not configured, PawConnect can still return backend-scored suggestions.

Live script:

> If OpenAI is not enabled or something fails, PawConnect can still return these backend results. So there is a safe fallback path.

Next part:

If OpenAI is enabled, call `AskWithToolsAsync`.

---

## 14. PawConnect Calls The OpenAI Client

File:

`Services/AdoptionCopilotService.cs`

Inside `AskAsync`:

```csharp
var openAiResponse = await openAiCopilotClient.AskWithToolsAsync(
    new AdoptionCopilotToolOpenAiRequest(query, appliedConstraints),
    async (toolCall, token) =>
    {
        toolNamesCalled.Add(toolCall.Name);
        var output = await ExecuteToolCallAsync(adopterUserId, deterministicArgs, toolCall, token);
        ...
        return new OpenAiCopilotToolOutput(toolCall.CallId, toolCall.Name, output.OutputJson);
    },
    cancellationToken);
```

What this does:

- sends the prompt and interpreted constraints to OpenAI
- passes a callback that can execute PawConnect tools
- if OpenAI asks for `search_dogs`, the callback runs the real backend search

What to say:

> OpenAI does not call the database. It can request a tool, and PawConnect decides how that tool is executed.

Live script:

> When OpenAI is enabled, PawConnect sends the prompt and the detected criteria to the OpenAI API. But the AI does not access the database directly. Instead, PawConnect gives it a small list of allowed tools.

Next method:

`OpenAiAdoptionCopilotClient.AskWithToolsAsync`

---

## 15. The OpenAI Client Builds The Conversation

File:

`Services/OpenAiAdoptionCopilotClient.cs`

Method:

```csharp
public async Task<OpenAiAdoptionCopilotResponse> AskWithToolsAsync(
    AdoptionCopilotToolOpenAiRequest request,
    OpenAiCopilotToolExecutor executeToolAsync,
    CancellationToken cancellationToken = default)
```

Important system prompt:

```csharp
content = """
You are PawConnect's adoption copilot.
Use PawConnect tools to retrieve real dog data before recommending dogs.
...
Never invent dogs, dog IDs, statuses, shelters, or private facts.
Only recommend dog IDs returned by PawConnect tools.
...
Use only displayTags and cautionTags returned by PawConnect tools. Do not invent tags.
...
"""
```

User content:

```csharp
content = JsonSerializer.Serialize(new
{
    request.UserMessage,
    request.DeterministicConstraints
}, JsonOptions)
```

What this does:

- instructs the model to behave as the PawConnect Copilot
- tells it not to invent data
- gives it the user's prompt and backend-detected constraints

What to say:

> The prompt tells the model its role and limits. It must use PawConnect tools and only recommend real dog IDs returned by those tools.

Live script:

> This part tells the model how it must behave. It says that the model should act as PawConnect's adoption helper, use PawConnect tools, and never invent dogs, dog IDs, shelters, or private facts.

Next method:

`SendResponsesRequestAsync`

---

## 16. The OpenAI Request Includes Tools And Strict JSON

File:

`Services/OpenAiAdoptionCopilotClient.cs`

Inside `AskWithToolsAsync`:

```csharp
var responseJson = await SendResponsesRequestAsync(
    settings,
    input,
    toolChoice: step == 0 ? "required" : "auto",
    cancellationToken);
```

Called method:

```csharp
private async Task<string> SendResponsesRequestAsync(...)
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
```

What this does:

- sends a request to the OpenAI Responses API
- includes available tools
- requires a tool call on the first step
- asks for strict JSON output

What to say:

> The API call includes both the conversation and the tool definitions. `tool_choice` is required first, so the model must retrieve PawConnect data before producing final suggestions.

Live script:

> The OpenAI request includes the prompt, the tool definitions, and a strict response format. The first step requires a tool call, so the model has to ask PawConnect for dog data before giving final suggestions.

Next methods:

`BuildTools` and `BuildResponseFormat`.

---

## 17. BuildTools Defines What The Model Can Ask For

File:

`Services/OpenAiAdoptionCopilotClient.cs`

Method:

```csharp
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
                    coatColors = ArraySchema("Coat colors such as Black, White, Brown, Golden, Tricolor, Black and tan, or Brown and white."),
                    city = StringSchema("Shelter city or dog location city."),
                    neighborhood = StringSchema("Shelter neighborhood/cartier such as Zorilor or Manastur."),
                    statuses = ArraySchema("Dog statuses. Public-safe values are Available and Reserved."),
                    activityLevel = StringSchema("Structured activity level: Low, Medium, High, or Any."),
                    homeType = StringSchema("Home setting inferred from the request, such as Apartment or House with yard."),
                    compatibilityTarget = StringSchema("Main compatibility target: Children, OlderChildren, Cats, OtherDogs, SeniorDog, SensitiveDog, YoungDog, SmallAnimals, or null.")
                }
            }
        },
        ...
    ];
}
```

What this does:

- exposes a controlled list of functions to the model
- tells the model what arguments each tool accepts
- prevents arbitrary database access

What to say:

> `BuildTools` is like a menu. The model can ask for `search_dogs`, but it cannot run SQL or access private data.

Live script:

> `BuildTools` is like giving the model a menu. It can ask for `search_dogs`, but it cannot run SQL, read random tables, or access private data.

Also mention:

```csharp
StringSchema(...)
NumberSchema(...)
BooleanSchema(...)
ArraySchema(...)
```

These are small helpers that describe the type of each tool argument.

---

## 18. BuildResponseFormat Forces Structured JSON

File:

`Services/OpenAiAdoptionCopilotClient.cs`

Method:

```csharp
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
                            reasons = ArraySchema("Short reasons copied from supported tags or evidence."),
                            suggestedNextAction = StringSchema("Short user-facing next action.")
                        }
                    }
                }
            }
        }
    };
}
```

What this does:

- prevents the response from being unstructured text
- forces fields like dog ID, rank, score, label, tags, and reasons

What to say:

> The final AI output must be JSON. This makes it possible for PawConnect to parse and validate the response before showing anything in the UI.

Live script:

> The final AI answer is not just free text. It has to come back as structured JSON with fields like dog ID, score, label, tags, reasons, and next action. That makes it easier for PawConnect to check before displaying it.

Next part:

The tool-calling loop.

---

## 19. OpenAI Tool Calls Are Executed By PawConnect

File:

`Services/OpenAiAdoptionCopilotClient.cs`

Inside `AskWithToolsAsync`:

```csharp
var toolCalls = ExtractToolCalls(responseJson);
```

If tool calls exist:

```csharp
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
```

What this does:

- reads the model's requested tool
- calls the callback from `AdoptionCopilotService`
- adds PawConnect's tool result back into the model conversation

The callback goes to:

`AdoptionCopilotService.ExecuteToolCallAsync`

Important code:

```csharp
switch (toolCall.Name)
{
    case "search_dogs":
    {
        var args = DeserializeArgs<AdoptionCopilotSearchDogsArgs>(toolCall.ArgumentsJson)
            ?? new AdoptionCopilotSearchDogsArgs();

        MergeDeterministicConstraints(args, deterministicArgs);
        var result = await toolService.SearchDogsAsync(adopterUserId, args, cancellationToken);
        var json = JsonSerializer.Serialize(new AdoptionCopilotToolJsonResult(
            result.Dogs.Count > 0,
            result.EmptyReason,
            result.Dogs.Select(ToDogDto).ToList(),
            result.AppliedConstraints), JsonOptions);
        return new CopilotToolExecutionResult(json, result, null);
    }
```

What this does:

- checks which tool was requested
- deserializes tool arguments
- merges deterministic backend constraints
- runs the real backend search
- serializes a sanitized tool result

What to say:

> Even when OpenAI asks for a search, PawConnect still executes the search itself. The deterministic constraints are merged back in, so the model cannot accidentally ignore exact filters detected by the backend.

Live script:

> When the AI asks for a tool, PawConnect runs the tool itself. It searches the real database, applies the same safety rules, and sends back only safe dog information. The AI receives candidate dogs, but not private data.

Next part:

OpenAI returns final JSON.

---

## 20. Final AI Results Are Validated Before Display

File:

`Services/AdoptionCopilotService.cs`

Back in `AskAsync` after OpenAI responds:

```csharp
var allowedCandidateMap = latestSearchCandidateMap ?? candidateMap;
var aiResults = openAiResponse.Results
    .Where(result => allowedCandidateMap.ContainsKey(result.DogId))
    .OrderBy(result => result.Rank)
    .Select(result => BuildAiResult(result, allowedCandidateMap[result.DogId], appliedConstraints))
    .OrderByDescending(result => result.ScorePercent)
    .ThenBy(result => result.Dog.Name)
    .ToList();
```

What this does:

- builds a map of dogs PawConnect actually returned
- keeps only OpenAI results whose dog ID exists in that map
- ignores invented or unknown dog IDs
- converts valid AI items into final UI results

The conversion method:

```csharp
private static AdoptionCopilotDogResult BuildAiResult(
    OpenAiAdoptionCopilotItem aiResult,
    AdoptionCopilotToolDogCandidate searchResult,
    IReadOnlyList<AdoptionCopilotConstraint> appliedConstraints)
{
    var matchedCriteria = BuildMatchedCriteria(searchResult.Dog, searchResult.DistanceKm, appliedConstraints);
    var reasons = ChooseTrustedReasons(aiResult.Reasons, searchResult.SafeReasons, searchResult.Dog);
    var displayTags = ChooseTrustedTags(aiResult.DisplayTags, searchResult.DisplayTags);
    var cautionTags = ChooseTrustedTags(aiResult.CautionTags, searchResult.CautionTags);
```

Important score safety:

```csharp
var safeScore = Math.Clamp(Math.Min(Math.Clamp(aiResult.ScorePercent, 25, 92), searchResult.ScorePercent + 3), 25, 92);
```

Uncertainty cap:

```csharp
if (HasUncertainPrimaryEvidence(searchResult))
{
    safeScore = Math.Min(safeScore, 80);
}
```

Final response:

```csharp
return new AdoptionCopilotResponse(
    NormalizeAssistantMessage(openAiResponse.AssistantMessage, query, aiResults.FirstOrDefault()?.Dog.Name, appliedConstraints),
    aiResults.Take(6).ToList(),
    true,
    usedSemanticSearch,
    true,
    null,
    AdoptionCopilotConstraintNormalizer.Normalize(appliedConstraints));
```

What to say:

> This is the final safety step. OpenAI can suggest a rank and explanation, but PawConnect only accepts dog IDs from the backend candidate list. The score and tags are also checked against PawConnect evidence.

Live script:

> After OpenAI returns its answer, the backend checks everything again. If the AI returns a dog ID that was not in the PawConnect candidate list, that dog is ignored. The backend also checks the score and tags so the AI cannot make the result too confident or invent unsupported evidence.

---

# What You Should Say In The Demo

Use this version if you need to explain the whole flow in about one minute:

> The Copilot starts in the Blazor page, where the adopter enters a prompt. The UI calls `AdoptionCopilotService.AskAsync` with the adopter ID and query. The backend first extracts deterministic constraints, such as size, coat color, neighborhood, activity level, home type, or compatibility needs. Then it calls `AdoptionCopilotToolService.SearchDogsAsync`, which loads only public-safe dogs from the database, meaning Available and Reserved dogs.

> After that, hard filters are applied. For example, if the user asks for black and tan dogs, dogs with another coat color are removed before scoring. Each remaining dog is passed through `BuildCandidate`, where PawConnect calculates a conservative score using real public dog data: size, location, coat color, semantic match, activity fit, and behavior evidence. `ExtractDogEvidence` reads the dog's description and behavior text and creates supported tags such as `Short walks`, `Settles quickly`, `Calm dog company`, or caution tags like `Ask shelter about cats`.

> If OpenAI is disabled, PawConnect returns these backend-scored results directly. If OpenAI is enabled, the service calls `OpenAiAdoptionCopilotClient.AskWithToolsAsync`. The OpenAI request contains a system prompt, strict JSON response format, and tool definitions. The model can request tools such as `search_dogs`, but PawConnect executes the tool itself. OpenAI never queries SQL directly and never receives private internal data.

> When OpenAI returns final suggestions, PawConnect validates every dog ID against the backend candidate list. Unknown or invented dog IDs are ignored. The backend also clamps scores and only keeps trusted tags supported by PawConnect evidence. Finally, the UI displays the assistant message, criteria chips, match labels, dog cards, tags, cautions, favorites, and View Details links.

Final key sentence:

> OpenAI helps interpret and explain the natural-language request, but PawConnect remains the source of truth for dog data, public-safe filtering, scoring limits, and final validation.

---

# Very Short Version

If the committee asks for a quick explanation:

> The UI sends the prompt to `AdoptionCopilotService.AskAsync`. The backend detects obvious constraints, searches real Available and Reserved dogs, applies hard filters, scores each dog from public evidence, and builds safe candidate results. If OpenAI is enabled, it can call controlled PawConnect tools and help rank or explain those candidates. The final dog IDs are validated against backend candidates before display, so the AI cannot invent dogs or bypass visibility rules.
