# Adoption Copilot Thesis Subsection Brief

This document is intended to be given to ChatGPT or another writing assistant so it can write a bachelor thesis subsection about the PawConnect Adoption Copilot.

The goal is to help the writing assistant produce an accurate academic explanation without inventing functionality that is not implemented.

## Suggested Prompt To Give ChatGPT

Use the technical notes below to write a bachelor thesis subsection about the "Adoption Copilot" feature in PawConnect.

Requirements for the subsection:

- Write in clear academic English suitable for a Computer Science bachelor thesis.
- Explain the feature from both user and technical perspectives.
- Mention the actual files, classes, services, and DTOs listed below.
- Emphasize that OpenAI is optional and controlled by backend validation.
- Explain that the AI does not directly access the database.
- Explain fallback behavior when OpenAI or embeddings are unavailable.
- Explain how hallucinated dogs are prevented.
- Avoid marketing language.
- Do not claim the Copilot guarantees adoption compatibility.
- Do not invent endpoints, controllers, database tables, or features that are not listed here.

Suggested subsection title:

`Adoption Copilot: Natural-Language, Evidence-Based Dog Search`

## Short Feature Summary

The Adoption Copilot is an adopter-facing feature in PawConnect that allows users to describe the kind of dog they are looking for using natural language. Instead of selecting only fixed filters, the user can write queries such as:

- "Find me a small friendly dog."
- "I have a cat at home."
- "I live in an apartment but enjoy longer walks."
- "Show me black and tan dogs."
- "I have a sick dog recovering at home."

The system interprets the query, retrieves real public-safe dogs from the database, extracts evidence from each dog profile, scores the candidates, and displays dog cards with match labels, reasons, display tags, and caution tags.

The feature is not a free-form chatbot. It is a controlled backend workflow with optional OpenAI assistance.

## User-Facing Location

The Copilot page is:

- `Components/Pages/Adopter/AdoptionCopilot.razor`

Route:

- `/adopter/copilot`

Authorization:

- `[Authorize(Roles = "Adopter")]`

The UI allows the adopter to:

- enter a natural-language query
- submit it to the Copilot
- view interpreted criteria chips
- view suggested dog cards
- see match labels and match percentages when appropriate
- see display tags and caution tags
- save dogs to favorites
- open dog details

The page calls:

- `AdoptionCopilotService.AskAsync(_currentUserId, _query)`

It also uses:

- `ICopilotStateService` to preserve the Copilot query/results state during navigation
- `IDogService.GetDogDetailsAsync` to reload real dog entities for display
- `IFavoriteDogService` to save/remove favorite dogs

## Main Backend Files

| File | Role |
| --- | --- |
| `Services/AdoptionCopilotService.cs` | Main orchestration service. Parses deterministic constraints, calls safe backend tools, optionally calls OpenAI, validates final AI output, and builds the response. |
| `Services/AdoptionCopilotToolService.cs` | Retrieves public-safe dog candidates, applies hard filters, runs semantic search support, extracts evidence, scores candidates, and creates safe result DTOs. |
| `Services/OpenAiAdoptionCopilotClient.cs` | Optional OpenAI client. Uses tool/function calling through the OpenAI Responses API and requires the model to select from backend-provided candidates. |
| `Services/AdoptionCopilotModels.cs` | Response/result models used by the Copilot UI and OpenAI orchestration. |
| `Services/AdoptionCopilotToolModels.cs` | Search arguments, intent, evidence, candidate, and sanitized tool DTO models. |
| `Services/AdoptionCopilotConstraintNormalizer.cs` | Cleans and deduplicates displayed criteria chips. |
| `Services/SemanticDogSearchService.cs` | Performs semantic search using embeddings when available, with deterministic fallback. |
| `Services/DogSearchDocumentService.cs` | Builds public-safe dog search documents. |
| `Services/DogSearchEmbeddingService.cs` | Creates, refreshes, rebuilds, and removes dog search embeddings. |
| `Services/OpenAiEmbeddingService.cs` | Optional OpenAI embedding client. |
| `Entities/DogSearchEmbedding.cs` | Stores dog search document text, content hash, embedding JSON, model name, and update time. |
| `Services/OpenAiSettings.cs` | Stores OpenAI configuration such as enabled flag, API key, chat model, and embedding model. |

## Main DTOs and Models

### `AdoptionCopilotResponse`

File:

- `Services/AdoptionCopilotModels.cs`

Purpose:

- Returned to the UI after a Copilot query.

Important fields:

- `AssistantMessage`
- `Results`
- `UsedAiEnhancement`
- `UsedSemanticSearch`
- `UsedToolCalling`
- `FallbackReason`
- `AppliedConstraints`

### `AdoptionCopilotDogResult`

File:

- `Services/AdoptionCopilotModels.cs`

Purpose:

- Represents one final dog result displayed in the UI.

Important fields:

- `DogId`
- `Dog`
- `ScorePercent`
- `MatchLabel`
- `Reasons`
- `SuggestedNextAction`
- `MatchedCriteria`
- `DisplayTags`
- `CautionTags`

### `AdoptionCopilotSearchDogsArgs`

File:

- `Services/AdoptionCopilotToolModels.cs`

Purpose:

- Represents structured search parameters extracted from the user's natural-language query.

Important fields include:

- `Query`
- `PrimaryIntent`
- `Sizes`
- `Breeds`
- `CoatColors`
- `City`
- `Neighborhood`
- `ShelterName`
- `MaxAgeYears`
- `MinAgeYears`
- `Statuses`
- `Temperaments`
- `ActivityLevel`
- `HomeType`
- `Compatibility`
- `CompatibilityTarget`
- `ExperienceLevel`
- `MustHave`
- `NiceToHave`
- `Avoid`
- `EvidenceToLookFor`
- `DisplayChipIntent`
- `Limit`
- `Count`

### `CopilotIntent`

File:

- `Services/AdoptionCopilotToolModels.cs`

Purpose:

- Represents the interpreted real-life adoption need.

Important fields:

- `PrimaryIntent`
- `CompatibilityTarget`
- `HomeType`
- `ActivityLevel`
- `RealLifeNeed`
- `MustHaveEvidence`
- `NiceToHaveEvidence`
- `NegativeEvidence`
- `SecondarySignals`
- `Chips`
- `Statuses`
- `City`
- `Neighborhood`
- `Sizes`
- `Limit`

### `CopilotDogEvidence`

File:

- `Services/AdoptionCopilotToolModels.cs`

Purpose:

- Stores evidence extracted from one dog profile.

Evidence categories:

- `DirectEvidence`
- `IndirectEvidence`
- `GenericEvidence`
- `PositiveEvidence`
- `CautionEvidence`
- `NegativeEvidence`
- `MissingEvidence`

It also includes:

- evidence items with source field and matched text
- supported display tags
- evidence summary

### `AdoptionCopilotDogToolDto`

File:

- `Services/AdoptionCopilotToolModels.cs`

Purpose:

- Sanitized public-safe dog DTO sent to OpenAI tools/results.

It includes only controlled public information such as:

- dog ID
- name
- breed
- coat color
- age text
- size
- public status
- public description
- behavior description
- shelter public location
- main image URL
- safe reasons
- display tags
- caution tags
- evidence summary
- score
- match label

## Step-by-Step Technical Process

### Step 1: The adopter submits a query

File:

- `Components/Pages/Adopter/AdoptionCopilot.razor`

The adopter enters a query in natural language. The page calls:

`AdoptionCopilotService.AskAsync(_currentUserId, _query)`

The Copilot is adopter-only, so the page is protected with:

`[Authorize(Roles = "Adopter")]`

### Step 2: The backend performs deterministic query interpretation

File:

- `Services/AdoptionCopilotService.cs`

Before OpenAI is used, the backend extracts deterministic constraints from the user message. This is important because the application does not rely only on AI interpretation.

Examples:

| User wording | Interpreted meaning |
| --- | --- |
| "black and tan dogs" | Coat color hard filter: `Black and tan` |
| "small dog" | Size hard filter: `Small` |
| "in Zorilor" | Neighborhood hard filter: `Zorilor` |
| "apartment" | Home suitability criterion |
| "longer walks" | Activity preference, not temperament |
| "I have a cat" | Compatibility target: cats |
| "sick dog recovering at home" | Compatibility target: sensitive dog |

The deterministic interpretation is stored in `AdoptionCopilotSearchDogsArgs` and later in `CopilotIntent`.

### Step 3: PawConnect retrieves candidate dogs through safe backend tools

File:

- `Services/AdoptionCopilotToolService.cs`

Main method:

`SearchDogsAsync(AdoptionCopilotSearchDogsArgs args, string? adopterUserId, CancellationToken cancellationToken)`

This method retrieves dog candidates from PawConnect data and applies hard filters.

Public-safe default:

- `Available` dogs can appear.
- `Reserved` dogs can appear with caution.
- `Adopted` and `InTreatment` dogs are excluded from public Copilot discovery by default.

Hard filters are applied before soft scoring. Examples:

- explicit size
- explicit breed
- explicit coat color
- explicit city/neighborhood
- explicit shelter
- explicit age range
- explicit status

This means a filter-only query such as "black and tan dogs" should return dogs that satisfy the coat color filter, not dogs that merely seem semantically related.

### Step 4: Semantic search may support candidate ranking

Files:

- `Services/SemanticDogSearchService.cs`
- `Services/DogSearchDocumentService.cs`
- `Services/DogSearchEmbeddingService.cs`
- `Services/OpenAiEmbeddingService.cs`
- `Entities/DogSearchEmbedding.cs`

PawConnect can use embeddings for semantic search if OpenAI embeddings are configured. A dog search document is built from public-safe dog fields, transformed into an embedding vector, and stored in `DogSearchEmbedding`.

Important stored fields:

- `DogId`
- `Content`
- `ContentHash`
- `EmbeddingJson`
- `EmbeddingModel`
- `UpdatedAt`

Semantic search helps match meaning rather than only exact words. For example, a query about a "quiet apartment dog" may match a dog profile that says the dog "settles indoors after short walks".

Fallback behavior:

- If OpenAI is disabled,
- or no API key is configured,
- or query embedding generation fails,
- or no stored embeddings exist,

then `SemanticDogSearchService` falls back to deterministic keyword/rule-based search.

### Step 5: Evidence is extracted from each candidate dog

File:

- `Services/AdoptionCopilotToolService.cs`

For each dog, PawConnect extracts evidence from public-safe fields only:

- dog size
- dog status
- dog breed display value
- coat color
- public description
- behavior description
- location
- shelter city
- shelter neighborhood

Evidence is grouped by strength:

| Evidence type | Meaning |
| --- | --- |
| Direct evidence | Strong evidence for the user's main request. Example: "calm near cats" for a cat query. |
| Indirect evidence | Helpful but weaker evidence. Example: "settles indoors" for a compatibility query. |
| Generic evidence | Positive but vague wording. Example: "friendly" without specific compatibility context. |
| Caution evidence | Important warning. Example: "Reserved", "Needs slow introductions", "Needs more space". |
| Negative evidence | Evidence that conflicts with the request. Example: high energy for a low-activity apartment query. |
| Missing evidence | The profile does not contain enough information for the requested criterion. |

This evidence is used for both scoring and visible card tags.

### Step 6: Candidates are scored and labeled

File:

- `Services/AdoptionCopilotToolService.cs`

The Copilot scoring logic compares:

- the interpreted user intent
- hard filters
- soft lifestyle preferences
- dog evidence
- caution signals
- missing information

The system separates deterministic filter matches from recommendation-style suitability matches.

For filter-only queries:

- Example: "black and tan dogs"
- The UI should prefer labels such as "Exact match" or "Matches request"
- It should not show low percentages like "58% Possible match" for dogs that satisfy the exact hard filter

For suitability queries:

- Example: "Find me a calm medium-sized dog in Cluj-Napoca that can live in an apartment"
- The score is a heuristic compatibility indicator
- Positive evidence increases the score
- Caution and missing evidence reduce the score
- Labels communicate approximate fit, not certainty

The exact score should not be interpreted as a medical or behavioral guarantee. It is an application-level ranking aid.

### Step 7: Optional OpenAI tool/function calling

File:

- `Services/OpenAiAdoptionCopilotClient.cs`

If OpenAI is enabled and an API key is configured, the Copilot can use OpenAI through tool/function calling.

Configuration:

- `Services/OpenAiSettings.cs`
- `appsettings.json`

Relevant settings:

- `OpenAI:Enabled`
- `OpenAI:ApiKey`
- chat model
- embedding model

The OpenAI client uses the Responses API endpoint:

`v1/responses`

Configured tools:

- `search_dogs`
- `get_adopter_profile_summary`
- `get_favorite_and_recent_preferences`
- `get_dog_details_public`

Important point:

The AI does not query SQL directly. Instead, the AI may request a tool call such as `search_dogs`, and PawConnect executes that tool using its own services. The tool output is sanitized before being returned to the model.

### Step 8: Backend validation protects final results

File:

- `Services/AdoptionCopilotService.cs`

Even when OpenAI is used, PawConnect validates the final response.

Safety behavior:

- OpenAI may only select dog IDs returned by backend candidate search.
- Unknown dog IDs are ignored.
- Dog IDs outside the latest candidate set are ignored.
- Unsupported reasons and tags are filtered.
- AI scores are capped relative to backend scores.
- Reserved dogs remain clearly marked with caution.
- If OpenAI fails or returns no usable result, PawConnect falls back to rule-based results.

This is the key hallucination-control mechanism.

## OpenAI Safety Explanation

The AI is not the source of truth. The backend is the source of truth.

OpenAI can help:

- interpret wording
- decide among backend-provided candidates
- produce concise explanations

OpenAI cannot:

- query the database directly
- run SQL
- see all private application data
- invent valid dogs
- bypass public-safe filtering
- override backend validation

## Data Sent to OpenAI

The Copilot sends sanitized public-safe data only.

Potentially included:

- dog ID
- name
- breed
- coat color
- age text
- size
- status
- public description
- behavior description
- shelter name/city/neighborhood
- evidence tags and caution tags

Not sent:

- passwords
- authentication tokens
- database connection strings
- raw SQL
- private shelter internal notes
- audit logs
- SMTP credentials
- arbitrary user data
- unavailable dogs outside backend candidates

## Fallback Behavior

The Copilot is designed to still work without OpenAI.

Fallback cases:

- OpenAI is disabled
- OpenAI API key is missing
- OpenAI request fails
- OpenAI returns invalid output
- embeddings are unavailable
- query embedding generation fails

Fallback result:

- deterministic parsing
- public-safe dog filtering
- keyword/rule-based matching
- evidence-based scoring
- safe UI response

This is important for reliability and for the thesis demonstration.

## Example Flow 1: Small Friendly Dog

User input:

`Find me a small friendly dog.`

Interpretation:

- hard filter/preference: small size
- temperament preference: friendly
- status default: Available and Reserved

Files involved:

- `Components/Pages/Adopter/AdoptionCopilot.razor`
- `Services/AdoptionCopilotService.cs`
- `Services/AdoptionCopilotToolService.cs`
- `Services/DogService.cs`

Data queried:

- `Dogs`
- `Shelters`
- `DogBreeds`
- `DogImages`

Expected output:

- small dogs should rank higher
- dogs with friendly/gentle/social public behavior evidence should rank higher
- generic or missing behavior details should lower confidence

Thesis explanation:

The Copilot first detects explicit constraints such as size, then uses softer evidence from public dog descriptions to rank candidates.

## Example Flow 2: Dog in a Specific Neighborhood

User input:

`Show me family-friendly dogs in Zorilor.`

Interpretation:

- hard location filter: shelter neighborhood `Zorilor`
- compatibility/temperament preference: children/family
- status default: Available and Reserved

Files involved:

- `Services/AdoptionCopilotService.cs`
- `Services/AdoptionCopilotToolService.cs`
- `Services/DogService.cs`

Data queried:

- `Dogs`
- `Shelters.Neighborhood`
- public dog behavior fields

Expected output:

- dogs outside Zorilor should be excluded if the neighborhood is treated as an explicit hard constraint
- remaining dogs are ranked by family/children evidence

Thesis explanation:

The Copilot combines hard filters such as neighborhood with softer suitability scoring such as family compatibility.

## Example Flow 3: Cat Compatibility

User input:

`I have a cat at home.`

Interpretation:

- primary intent: compatibility
- compatibility target: cats
- direct evidence: calm near cats, redirectable around cats, slow cat introductions
- negative evidence: chase behavior, strong interest in fast-moving small animals

Files involved:

- `Services/AdoptionCopilotToolModels.cs`
- `Services/AdoptionCopilotToolService.cs`
- `Services/AdoptionCopilotService.cs`

Expected output:

- dog cards should show cat-related tags only when supported
- examples: `Calm near cats`, `Needs slow cat introductions`, `Ask shelter about cats`
- unrelated tags such as `Short walks` or `Indoor rest` should not be primary cat-query tags

Thesis explanation:

The Copilot uses evidence-backed display tags. A generally friendly dog is not automatically a strong cat match unless the public profile contains cat-relevant evidence.

## Example Flow 4: Apartment but Longer Walks

User input:

`I live in an apartment but enjoy longer walks.`

Interpretation:

- home criterion: apartment
- activity criterion: longer walks or moderate activity
- explicit longer-walk preference overrides the usual apartment -> low activity assumption

Expected behavior:

- dogs with longer-walk or moderate-activity evidence should rank higher
- dogs that only mention short walks should not receive a longer-walk match chip
- apartment cautions such as `Needs more space` should reduce score

Thesis explanation:

The Copilot distinguishes explicit preferences from inferred assumptions. It should not treat "short walks" and "longer walks" as the same simply because both contain the word "walks".

## Important Tests Related to Copilot

Main test file:

- `PawConnect.Tests/Tests/SemanticDogSearchServiceTests.cs`

Related tests also exist in:

- `PawConnect.Tests/Tests/CopilotStateServiceTests.cs`
- `PawConnect.Tests/Tests/DogRecommendationServiceTests.cs`

The tests cover:

- OpenAI disabled fallback
- OpenAI failure fallback
- semantic search fallback when embeddings are unavailable
- public-safe filtering
- embedding refresh/index behavior
- unknown OpenAI dog IDs being ignored
- deterministic coat color interpretation
- short walks vs longer walks behavior
- Copilot chip cleanup and category normalization
- compatibility queries such as cats, children, senior dogs, and sensitive dogs

## Strengths To Mention in the Thesis

- Natural-language interface for adopters.
- Combines deterministic filters, semantic search, evidence extraction, and optional AI.
- AI does not directly access the database.
- Backend validation prevents hallucinated dogs from being displayed.
- Public-safe filtering protects unavailable dogs.
- Fallback behavior keeps the feature usable without OpenAI.
- Evidence and caution tags make recommendations more explainable.

## Limitations To Mention Honestly

- The Copilot depends on the quality of dog descriptions and behavior descriptions.
- Compatibility is inferred from public text, not from fully structured medical/behavioral assessments.
- OpenAI and embeddings require external API availability when enabled.
- Embeddings must be refreshed when dog profile content changes.
- The Copilot is advisory and cannot guarantee real-world compatibility.
- Shelter confirmation remains necessary before adoption decisions.

## Suggested Thesis Subsection Outline

1. Purpose of the Adoption Copilot
2. User interaction flow
3. Backend architecture
4. Query interpretation and intent extraction
5. Candidate retrieval and public-safe filtering
6. Semantic search and embeddings
7. Evidence extraction and scoring
8. Optional OpenAI tool/function calling
9. Backend validation and hallucination prevention
10. Fallback behavior
11. Limitations and future improvements

## Short Thesis-Style Explanation

The Adoption Copilot in PawConnect is implemented as a controlled AI-assisted search workflow rather than as an unrestricted chatbot. The adopter enters a natural-language request in the Blazor Server page `Components/Pages/Adopter/AdoptionCopilot.razor`, which calls `AdoptionCopilotService.AskAsync`. The backend first extracts deterministic constraints such as size, breed, coat color, location, activity level, home type, and compatibility target. Candidate dogs are retrieved through `AdoptionCopilotToolService`, which applies public-safe filters and excludes non-adoptable statuses from normal discovery. For each candidate, the system extracts evidence from public dog fields such as description, behavior description, size, status, coat color, and shelter location. The candidates are then scored and displayed with match labels, reasons, display tags, and caution tags.

When OpenAI is enabled, PawConnect uses tool/function calling through `OpenAiAdoptionCopilotClient`. The model can request predefined tools such as `search_dogs`, but PawConnect executes those tools using its own services and returns sanitized results. The model is not allowed to access the database directly. Final OpenAI output is validated by the backend: unknown dog IDs are ignored, unsupported tags are filtered, and fallback rule-based results are used if OpenAI fails. This design allows the Copilot to benefit from natural-language interpretation while keeping the application database, privacy rules, and final dog selection under backend control.

## Things ChatGPT Must Not Claim

Do not claim:

- that the Copilot guarantees adoption compatibility
- that OpenAI directly queries SQL Server
- that OpenAI has unrestricted access to the database
- that all adopter private data is sent to OpenAI
- that the system has a separate REST controller for Copilot, unless directly verified in code
- that semantic embeddings are always used
- that OpenAI is required for the feature to work
- that the score is a scientific probability

Correct wording:

- "The score is a heuristic match indicator."
- "The Copilot is advisory."
- "Shelter confirmation remains necessary."
- "OpenAI is optional and validated by the backend."
- "The backend remains the source of truth."
