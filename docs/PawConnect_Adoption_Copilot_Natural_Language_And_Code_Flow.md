# PawConnect Adoption Copilot - Natural Language Explanation and Code Flow

This document explains the PawConnect Adoption Copilot in plain language, with enough technical detail to answer questions during the thesis presentation.

The most important idea is this:

> The Copilot lets an adopter write a natural-language request, but the final dog suggestions still come from PawConnect's own database, filters, services, and validation rules.

It is not a chatbot that freely invents answers. It is an assisted search and ranking feature.

---

## 1. What the Adoption Copilot Does

The Adoption Copilot is available on:

| Page | File |
|---|---|
| `/adopter/copilot` | `Components/Pages/Adopter/AdoptionCopilot.razor` |

Only users with the `Adopter` role can access it:

```csharp
@page "/adopter/copilot"
@attribute [Authorize(Roles = "Adopter")]
```

The adopter writes a prompt such as:

```text
I have a sick dog recovering at home and need a calm dog that will not overwhelm him.
```

or:

```text
I live in an apartment but enjoy longer walks.
```

The Copilot then returns real PawConnect dogs with:

- a short summary message
- interpreted criteria chips
- dog cards
- match percentage, when it is a suitability/recommendation query
- qualitative labels such as `Strong match`, `Good match`, or `Possible match`
- visible evidence tags, such as `Longer walks`, `Medium size`, `Calm dog company`
- caution tags, such as `Reserved - availability may change`, `Needs more space`, or `Ask shelter about apartment fit`
- a suggested next action, usually asking the adopter to review the profile or confirm details with the shelter

---

## 2. Simple Explanation

In simple terms, the Copilot works like this:

1. The adopter writes what kind of dog they want.
2. PawConnect reads the prompt and extracts obvious filters, such as size, color, location, activity level, home type, or compatibility needs.
3. PawConnect searches only real dogs from the database.
4. The system excludes dogs that should not be public adoption candidates, such as `Adopted` or `InTreatment` dogs.
5. The system scores each dog based on how well the dog's public profile matches the request.
6. If OpenAI is configured, the AI helps interpret the prompt and explain/rerank the backend-provided candidates.
7. Even when OpenAI is used, the backend validates the returned dog IDs.
8. The UI displays only real dogs that PawConnect approved.

So, the AI helps with language understanding and explanation, but PawConnect remains the source of truth.

---

## 3. Important Files

| File | Purpose |
|---|---|
| `Components/Pages/Adopter/AdoptionCopilot.razor` | Blazor UI for typing prompts and showing results |
| `Services/IAdoptionCopilotService.cs` | Main Copilot service interface |
| `Services/AdoptionCopilotService.cs` | Main orchestration flow: prompt in, final response out |
| `Services/IAdoptionCopilotToolService.cs` | Interface for safe Copilot tools |
| `Services/AdoptionCopilotToolService.cs` | Searches, filters, extracts evidence, scores dogs |
| `Services/IOpenAiAdoptionCopilotClient.cs` | Interface for OpenAI Copilot client |
| `Services/OpenAiAdoptionCopilotClient.cs` | Calls OpenAI Responses API with tool/function calling |
| `Services/SemanticDogSearchService.cs` | Semantic/keyword search used as supporting search logic |
| `Services/DogSearchDocumentService.cs` | Builds public-safe dog search text |
| `Services/DogSearchEmbeddingService.cs` | Manages stored dog search embeddings |
| `Services/OpenAiEmbeddingService.cs` | Calls OpenAI embeddings API when enabled |
| `Services/AdoptionCopilotModels.cs` | Response models shown by the UI |
| `Services/AdoptionCopilotToolModels.cs` | Tool arguments, candidate DTOs, intent/evidence models |
| `Services/CopilotStateService.cs` | Stores last Copilot result in session-like state |

---

## 4. Main Models Used

### `AdoptionCopilotResponse`

File:

```text
Services/AdoptionCopilotModels.cs
```

This is the final object returned to the UI.

Important fields:

```csharp
public sealed record AdoptionCopilotResponse(
    string AssistantMessage,
    IReadOnlyList<AdoptionCopilotDogResult> Results,
    bool UsedAiEnhancement,
    bool UsedSemanticSearch,
    bool UsedToolCalling = false,
    string? FallbackReason = null,
    IReadOnlyList<AdoptionCopilotConstraint>? AppliedConstraints = null);
```

This tells the UI:

- what summary to show
- which dog cards to show
- whether OpenAI helped
- whether semantic search was used
- whether tool calling was used
- what interpreted constraints should appear as chips

### `AdoptionCopilotDogResult`

This represents one dog card in the Copilot results:

```csharp
public sealed record AdoptionCopilotDogResult(
    int DogId,
    Dog Dog,
    int ScorePercent,
    string MatchLabel,
    IReadOnlyList<string> Reasons,
    string SuggestedNextAction,
    double? DistanceKm = null,
    bool UsedAiEnhancement = false,
    IReadOnlyList<AdoptionCopilotConstraint>? MatchedCriteria = null,
    IReadOnlyList<string>? DisplayTags = null,
    IReadOnlyList<string>? CautionTags = null);
```

The important point: the result contains a real `Dog` entity loaded from the PawConnect database.

### `AdoptionCopilotSearchDogsArgs`

File:

```text
Services/AdoptionCopilotToolModels.cs
```

This is the structured search request used by the Copilot tools.

It can contain:

- `Sizes`
- `Breeds`
- `CoatColors`
- `City`
- `Neighborhood`
- `MaxAgeYears`
- `MinAgeYears`
- `Statuses`
- `BehaviorTerms`
- `EnergyLevel`
- `ActivityLevel`
- `HomeType`
- `CompatibilityTarget`
- `ExperienceLevel`
- `MustHave`
- `NiceToHave`
- `Avoid`
- `NearLocationText`
- `RadiusKm`
- `Sort`

This is how natural language becomes structured search data.

### `CopilotIntent`

This is the interpreted intent:

```csharp
public sealed record CopilotIntent(
    string PrimaryIntent,
    string CompatibilityTarget,
    string HomeType,
    string ActivityLevel,
    string RealLifeNeed,
    IReadOnlyList<string> MustHaveEvidence,
    IReadOnlyList<string> NiceToHaveEvidence,
    IReadOnlyList<string> NegativeEvidence,
    IReadOnlyList<string> SecondarySignals,
    IReadOnlyList<string> Chips,
    IReadOnlyList<string> Statuses,
    string? City,
    string? Neighborhood,
    IReadOnlyList<string> Sizes,
    int Limit);
```

Example:

Prompt:

```text
I have a sick dog recovering at home.
```

Intent:

- `PrimaryIntent`: `Compatibility`
- `CompatibilityTarget`: `SensitiveDog`
- `HomeType`: maybe `Any`
- `ActivityLevel`: often calm/low
- evidence needed: calm dog company, respectful around dogs, gentle introductions

---

## 5. Complete Code Flow: From Prompt to Final Dogs

This is the full flow after the user writes a prompt and clicks **Ask Copilot**.

---

### Step 1: User enters a prompt in the UI

File:

```text
Components/Pages/Adopter/AdoptionCopilot.razor
```

The text field stores the prompt in `_query`:

```razor
<MudTextField @bind-Value="_query"
              Label="What kind of dog are you looking for?"
              Placeholder="Example: calm medium dog for apartment living near Cluj"
              Lines="3" />
```

The Ask button calls:

```csharp
AskCopilotAsync()
```

---

### Step 2: The UI validates the prompt and current user

File:

```text
Components/Pages/Adopter/AdoptionCopilot.razor
```

Method:

```csharp
private async Task AskCopilotAsync()
```

It checks:

- is there a logged-in adopter user?
- is the prompt empty?
- should the button show loading state?

Then it calls the service:

```csharp
_response = await AdoptionCopilotService.AskAsync(_currentUserId, _query);
```

After the response returns, it saves the state:

```csharp
CopilotStateService.SaveState(_currentUserId, _query, _response);
```

This allows the page to restore the last Copilot result if the user navigates away and comes back.

---

### Step 3: The main Copilot service receives the prompt

File:

```text
Services/AdoptionCopilotService.cs
```

Interface:

```text
Services/IAdoptionCopilotService.cs
```

Entry method:

```csharp
public async Task<AdoptionCopilotResponse> AskAsync(
    string adopterUserId,
    string userMessage,
    CancellationToken cancellationToken = default)
```

This method is the main orchestrator.

It does not directly build the final list alone. Instead, it coordinates:

- deterministic parsing
- fallback search
- OpenAI tool calling, if enabled
- backend validation
- final response construction

---

### Step 4: PawConnect parses obvious constraints first

File:

```text
Services/AdoptionCopilotService.cs
```

Method:

```csharp
BuildDeterministicSearchArgsAsync(...)
```

This step extracts obvious constraints from the user's prompt before OpenAI is used.

Examples:

| User phrase | Detected meaning |
|---|---|
| `black dog` | `CoatColors = Black` |
| `black and tan dogs` | `CoatColors = Black and tan` |
| `small dog` | `Sizes = Small` |
| `medium dog` | `Sizes = Medium` |
| `in Zorilor` | `Neighborhood = Zorilor` |
| `apartment` | `HomeType = Apartment` |
| `longer walks` | `ActivityLevel = Medium` / longer-walk preference |
| `cat at home` | `CompatibilityTarget = Cats` |
| `older dog at home` | `CompatibilityTarget = SeniorDog` |
| `sick dog recovering` | `CompatibilityTarget = SensitiveDog` |

Why this matters:

- hard filters should not depend only on the AI model
- exact filters should still work if OpenAI is disabled
- OpenAI is not allowed to weaken or ignore important user constraints

---

### Step 5: PawConnect prepares a safe fallback search

File:

```text
Services/AdoptionCopilotService.cs
```

The main service calls:

```csharp
var fallbackSearch = await toolService.SearchDogsAsync(
    adopterUserId,
    deterministicArgs,
    cancellationToken);
```

Implementation:

```text
Services/AdoptionCopilotToolService.cs
```

Method:

```csharp
public async Task<AdoptionCopilotToolSearchResult> SearchDogsAsync(...)
```

This fallback search is important because:

- it works without OpenAI
- it gives the app a real list of candidate dogs
- it gives OpenAI a safe candidate set if OpenAI is used
- it prevents the UI from depending completely on the AI response

---

### Step 6: The tool service loads public-safe dogs

File:

```text
Services/AdoptionCopilotToolService.cs
```

The query loads dogs with related public data:

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

This means:

- `Available` dogs can appear
- `Reserved` dogs can appear, usually with a warning chip
- `Adopted` dogs are excluded
- `InTreatment` dogs are excluded

This is one of the most important safety rules.

---

### Step 7: Hard filters are applied

File:

```text
Services/AdoptionCopilotToolService.cs
```

Method:

```csharp
private bool MatchesHardFilters(...)
```

Hard filters are not just score bonuses. They exclude dogs that do not match explicit constraints.

Examples:

- if the user asks for `black and tan dogs`, dogs with other coat colors are removed
- if the user asks for `small dogs`, medium and large dogs are removed
- if the user asks for `Zorilor`, dogs from other neighborhoods are removed
- if the user asks for dogs near a location, the distance filter is applied
- if the user asks for an age range, age checks are applied

This makes deterministic filter queries behave like proper search filters.

---

### Step 8: Semantic search may support candidate ranking

File:

```text
Services/AdoptionCopilotToolService.cs
```

Method:

```csharp
GetSemanticRankingsAsync(...)
```

This calls:

```text
Services/SemanticDogSearchService.cs
```

Method:

```csharp
SearchDogsAsync(...)
```

If OpenAI embeddings are configured, semantic search can compare meaning, not only exact words.

Example:

User says:

```text
quiet apartment dog
```

A dog description says:

```text
She settles indoors after short walks and enjoys a predictable routine.
```

Semantic search can help connect those meanings.

If embeddings are unavailable or fail, the app falls back to keyword/rule-based search:

```csharp
return await KeywordFallbackSearchAsync(...);
```

Important:

Semantic search is supporting evidence. It does not bypass public-safe filters.

---

### Step 9: Each dog candidate is scored

File:

```text
Services/AdoptionCopilotToolService.cs
```

Method:

```csharp
private static AdoptionCopilotToolDogCandidate BuildCandidate(...)
```

This method creates one internal candidate result.

The score starts conservatively:

```csharp
var score = 34;
```

Then it adds points for things that match the prompt:

- size match
- coat color match
- city/neighborhood match
- distance match
- apartment fit
- yard/activity fit
- calm/low activity evidence
- longer-walk evidence
- compatibility evidence
- semantic match support

It also subtracts points for cautions:

- reserved status, lightly
- missing behavior details
- needs more space
- higher activity needs
- patient adopter needed
- not suitable with cats
- not ideal for young children
- missing compatibility evidence

---

### Step 10: Evidence is extracted from dog descriptions

File:

```text
Services/AdoptionCopilotToolService.cs
```

Method:

```csharp
private static CopilotDogEvidence ExtractDogEvidence(...)
```

This is one of the key parts of the Copilot.

It reads public dog data:

- `Dog.Description`
- `Dog.BehaviorDescription`
- `Dog.Size`
- `Dog.Status`
- `Dog.CoatColor`
- shelter city/neighborhood
- public image data

It converts normal shelter text into evidence tags.

Examples:

| Dog text | Possible evidence tag |
|---|---|
| `short daily walks` | `Short walks` |
| `settles quickly` | `Settles quickly` |
| `quiet routine` | `Quiet routine` |
| `longer walks` | `Longer walks` |
| `walks politely beside calm dogs` | `Calm dog company`, `Respectful around dogs` |
| `slow introductions` | `Needs slow dog introductions` |
| `notices cats but can be redirected` | `Redirectable around cats` |
| `fast-moving small animals are too exciting` | `Not suitable with cats` |
| `very young children may be too intense` | `Not ideal for young children` |
| `patient adopter` | `Patient adopter needed` |

The evidence is grouped into:

- direct evidence
- indirect evidence
- generic evidence
- caution evidence
- negative evidence
- missing evidence

This is why the Copilot can explain matches instead of only showing a number.

---

### Step 11: Scores are capped and calibrated

File:

```text
Services/AdoptionCopilotToolService.cs
```

Important methods:

```csharp
CalculateIntentEvidenceScore(...)
ApplyCompatibilityEvidenceCaps(...)
ApplyHomeActivityEvidenceCaps(...)
CalibrateRecommendationScore(...)
ApplyIntentConfidenceCaps(...)
ApplyFinalVisibleDifferentiation(...)
```

The goal is to avoid unrealistic results like every dog getting 95%.

The score is calibrated so:

- strong but realistic matches are usually around the upper 70s or low 80s
- partial matches are around 45-65
- dogs with only weak evidence do not look like perfect matches
- dogs with `Ask shelter...` uncertainty are kept conservative
- exact filter-only requests can show `Exact match` instead of a misleading low percentage

Example:

For:

```text
black and tan dogs
```

The Copilot should treat this as a deterministic filter. A dog with coat color `Black and tan` should show something like:

```text
Exact match
```

not:

```text
58% Possible match
```

For:

```text
I live in an apartment but enjoy longer walks
```

The Copilot should separate:

- `Home: Apartment`
- `Activity: Longer walks`
- `Lifestyle: Moderate activity`

It should not treat `short walks` as a longer-walk match.

---

### Step 12: OpenAI may be used if configured

File:

```text
Services/AdoptionCopilotService.cs
```

After fallback candidates are prepared, the service checks:

```csharp
if (!settings.Enabled || !settings.HasApiKey)
{
    return fallback;
}
```

So:

- if OpenAI is enabled and has an API key, the OpenAI tool-calling flow is used
- if OpenAI is disabled or not configured, PawConnect returns fallback rule-based results

Important clarification:

The fallback is not the full AI experience. It does not provide the same OpenAI-generated interpretation. But it still uses PawConnect parsing, filtering, scoring, and dog data to return suggestions where possible.

---

### Step 13: OpenAI receives controlled instructions and tool schemas

File:

```text
Services/OpenAiAdoptionCopilotClient.cs
```

Method:

```csharp
AskWithToolsAsync(...)
```

The OpenAI client sends:

- a system prompt
- the user's message
- deterministic constraints already detected by PawConnect
- tool definitions
- strict JSON response format

The model is instructed to:

- use PawConnect tools
- only recommend returned dog IDs
- not invent dogs
- not invent tags
- not expose private information
- keep confidence conservative

---

### Step 14: OpenAI can call PawConnect tools

File:

```text
Services/OpenAiAdoptionCopilotClient.cs
```

Method:

```csharp
BuildTools()
```

The available tools are:

| Tool | Purpose |
|---|---|
| `search_dogs` | Search public-safe dogs using structured filters |
| `get_adopter_profile_summary` | Get sanitized adopter profile summary |
| `get_favorite_and_recent_preferences` | Get aggregate favorites/recently viewed preferences |
| `get_dog_details_public` | Fetch public-safe details for one dog ID |

The AI does not call SQL.

It does not receive a database connection.

It can only request these safe operations.

---

### Step 15: PawConnect executes the tool call

File:

```text
Services/AdoptionCopilotService.cs
```

Method:

```csharp
ExecuteToolCallAsync(...)
```

Example for `search_dogs`:

```csharp
var args = DeserializeArgs<AdoptionCopilotSearchDogsArgs>(toolCall.ArgumentsJson)
    ?? new AdoptionCopilotSearchDogsArgs();

MergeDeterministicConstraints(args, deterministicArgs);

var result = await toolService.SearchDogsAsync(adopterUserId, args, cancellationToken);
```

The important part is:

```csharp
MergeDeterministicConstraints(args, deterministicArgs);
```

This means if PawConnect already detected `black and tan`, `small`, or `Zorilor`, those constraints remain enforced even if the AI forgets them.

---

### Step 16: Tool output is sanitized

File:

```text
Services/AdoptionCopilotService.cs
```

Method:

```csharp
ToDogDto(...)
```

The OpenAI tool output uses `AdoptionCopilotDogToolDto`, not raw EF entities.

The DTO includes public-safe dog information such as:

- dog ID
- name
- breed
- coat color
- age text
- size
- status
- public description
- behavior description
- shelter name
- shelter city/neighborhood
- distance
- main image URL
- evidence tags
- score
- match label

It does not include private adopter data, passwords, SMTP credentials, audit logs, or internal admin-only data.

---

### Step 17: OpenAI returns structured JSON

File:

```text
Services/OpenAiAdoptionCopilotClient.cs
```

Method:

```csharp
BuildResponseFormat()
```

OpenAI is asked to return strict JSON:

```json
{
  "assistantMessage": "...",
  "results": [
    {
      "dogId": 1,
      "rank": 1,
      "matchLabel": "Good match",
      "scorePercent": 76,
      "displayTags": ["Short walks", "Indoor rest"],
      "cautionTags": [],
      "shortSelectionRationale": "...",
      "reasons": ["Short walks", "Indoor rest"],
      "suggestedNextAction": "..."
    }
  ]
}
```

This makes the response easier to validate than free-form text.

---

### Step 18: Backend validates OpenAI results

File:

```text
Services/AdoptionCopilotService.cs
```

After OpenAI returns results, PawConnect filters them:

```csharp
var aiResults = openAiResponse.Results
    .Where(result => allowedCandidateMap.ContainsKey(result.DogId))
    .Select(result => BuildAiResult(result, allowedCandidateMap[result.DogId], appliedConstraints))
    .ToList();
```

This is very important.

It means:

- OpenAI cannot invent a dog ID
- OpenAI cannot return an adopted dog that was not in the candidate list
- OpenAI cannot return a dog that failed hard filters
- OpenAI cannot decide public visibility

If OpenAI returns no valid dog IDs, PawConnect falls back to backend candidates.

---

### Step 19: Final result is sorted and returned

File:

```text
Services/AdoptionCopilotService.cs
```

Results are sorted by score:

```csharp
aiResults = aiResults
    .OrderByDescending(result => result.ScorePercent)
    .ThenBy(result => result.Dog.Name)
    .ToList();
```

Then the final response is returned:

```csharp
return new AdoptionCopilotResponse(
    NormalizeAssistantMessage(...),
    aiResults.Take(6).ToList(),
    true,
    usedSemanticSearch,
    true,
    null,
    AdoptionCopilotConstraintNormalizer.Normalize(appliedConstraints));
```

The UI receives:

- final message
- top dog cards
- AI-used flag
- semantic-search-used flag
- tool-calling-used flag
- applied constraints

---

### Step 20: UI displays dog cards

File:

```text
Components/Pages/Adopter/AdoptionCopilot.razor
```

The UI displays:

- assistant message
- explanation chips
- match score/label
- dog image
- dog name, breed, age
- shelter
- status
- reasons
- display tags
- caution tags
- `View Details`
- favorite/save action

If the user refreshes or returns to the page, the UI reloads dog details from the database instead of trusting stale session data:

```csharp
var dog = await DogService.GetDogDetailsAsync(savedResult.DogId);
if (dog?.Status is not (DogStatus.Available or DogStatus.Reserved))
{
    continue;
}
```

This prevents unavailable dogs from remaining visible after their status changes.

---

## 6. What the AI Model Actually Does

The AI model helps with:

- understanding natural language
- deciding which structured tool arguments to request
- interpreting more complex situations, such as:
  - sick dog at home
  - older dog at home
  - apartment but longer walks
  - cat compatibility
  - young children
- choosing a concise explanation
- helping select/rerank dogs from the backend candidate list

The AI model does not:

- query SQL directly
- create dogs
- update dogs
- submit adoption requests
- decide final adoption
- bypass authorization
- show private data
- invent dogs that are not in PawConnect

Good sentence for presentation:

> The OpenAI model is used for language interpretation and explanation, while PawConnect keeps control over data access, filtering, scoring, and final validation.

---

## 7. What Happens Without OpenAI

If OpenAI is disabled or no API key exists:

```csharp
if (!settings.Enabled || !settings.HasApiKey)
{
    return fallback;
}
```

The Copilot returns fallback results built by PawConnect.

Fallback still includes:

- deterministic parsing
- public-safe dog filtering
- hard constraints
- rule-based scoring
- evidence tags
- match labels

Fallback does not include:

- OpenAI-generated explanation
- OpenAI tool-calling reasoning
- full AI interpretation quality

If embeddings are also unavailable, semantic search falls back to keyword/rule-based search.

---

## 8. Examples

### Example 1: Apartment and low activity

Prompt:

```text
I live in an apartment and want a dog that does not need too much activity.
```

Expected interpretation:

- `Home: Apartment`
- `Lifestyle: Low activity`
- look for:
  - short walks
  - indoor rest
  - settles quickly
  - quiet routine
  - small/medium size

Good dog evidence:

- "short walks"
- "settles indoors"
- "quiet routine"
- "does not demand constant activity"

Cautions:

- "needs more space"
- "higher activity needs"
- "large dog - confirm apartment fit"

What to say:

> This is a suitability query, so the Copilot does not only filter. It ranks dogs by how much their descriptions support apartment living and lower activity.

---

### Example 2: Coat color filter

Prompt:

```text
black and tan dogs
```

Expected interpretation:

- hard filter: `CoatColor = Black and tan`

Expected UI:

- label like `Exact match`
- chip like `Coat color: Black and tan`
- no low `Possible match` label

What to say:

> This is a deterministic filter query. The system should not pretend it is predicting compatibility. It simply returns dogs that satisfy the explicit coat color filter.

---

### Example 3: Sensitive or sick dog at home

Prompt:

```text
I have a sick dog recovering at home and need a calm dog that will not overwhelm him.
```

Expected interpretation:

- `PrimaryIntent = Compatibility`
- `CompatibilityTarget = SensitiveDog`
- look for:
  - calm dog company
  - respectful around dogs
  - gentle play style
  - slow introductions
  - not pushy

Good evidence:

- "walks politely near calm dogs"
- "not pushy with other dogs"
- "prefers gentle interactions"
- "slow introductions"

Cautions:

- "Ask shelter about sensitive dog fit"
- "No dog compatibility history found"
- "May overwhelm sensitive dogs"

What to say:

> For compatibility queries, the Copilot is stricter. Generic words like "friendly" are not enough for a high score. It looks for direct evidence about the requested compatibility target.

---

## 9. How Scores and Labels Should Be Explained

The score is not a scientific probability.

It is a heuristic match indicator.

Good explanation:

> The percentage is a ranking aid. It combines explicit filters, public dog profile evidence, caution tags, and match strength. It does not guarantee real-world compatibility.

Labels:

| Score range | Label |
|---|---|
| 80-100 | Strong match |
| 65-79 | Good match |
| 45-64 | Possible match |
| below 45 | Low match |

For simple filter-only queries, the UI can show:

- `Exact match`
- `Matches request`

instead of treating it like a compatibility score.

---

## 10. Privacy and Safety

The Copilot only sends public-safe data to OpenAI.

It can include:

- dog name
- breed
- coat color
- age
- size
- public status
- public description
- behavior description
- shelter name/city/neighborhood
- safe evidence tags

It should not include:

- adopter full private profile details
- passwords
- tokens
- SMTP credentials
- audit logs
- private admin notes
- private shelter-only information
- arbitrary user IDs

The AI also cannot choose unknown dogs because the backend validates returned dog IDs.

---

## 11. Committee Questions and Short Answers

### Does the AI access the database directly?

No. The AI can only request predefined PawConnect tools. PawConnect executes those tools through its own services and returns sanitized results.

### Can the AI invent dogs?

The model might output an invalid dog ID, but the backend ignores it. Final dog IDs must exist in the candidate map returned by PawConnect tools.

### What happens if OpenAI is unavailable?

The service returns fallback results using deterministic parsing, public-safe filters, rule-based scoring, and keyword/semantic fallback logic where available.

### Why use AI here?

Because adopters often describe real-life situations instead of using exact filters. For example, "I have a sick dog recovering at home" is not a normal dropdown filter, but the Copilot can interpret it as a sensitive-dog compatibility need.

### Are the scores guaranteed?

No. They are heuristic ranking scores. The app still tells users to review profiles and confirm compatibility with the shelter.

### Why not let OpenAI decide everything?

Because adoption data needs safety and consistency. PawConnect keeps control over visibility, filters, scoring caps, dog IDs, and private data.

---

## 12. One-Minute Explanation to Memorize

The Adoption Copilot is a natural-language search feature for adopters. Instead of only choosing filters, the user can describe their situation, such as needing a calm dog for an apartment or a dog that will not overwhelm an older dog at home. The Blazor page sends the prompt to `AdoptionCopilotService`, which first extracts deterministic constraints like size, coat color, location, activity level, or compatibility target. PawConnect then searches only public-safe dogs through `AdoptionCopilotToolService`, excluding adopted and in-treatment dogs. Each dog is scored using public profile data, behavior descriptions, evidence tags, cautions, and optional semantic search. If OpenAI is configured, it helps interpret the prompt and explain or rerank the backend-provided candidates through tool calling, but it never accesses the database directly. The backend validates all returned dog IDs before the UI displays them, so the final cards always represent real PawConnect dogs.

