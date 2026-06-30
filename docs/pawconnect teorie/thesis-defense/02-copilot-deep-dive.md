# Adoption Copilot Deep Dive

## What the Copilot Does

The Adoption Copilot lets an adopter type natural language such as:

- "Find me a small friendly dog."
- "I have a cat at home."
- "I live in an apartment but enjoy longer walks."
- "Show me black and tan dogs."
- "I have a sick dog recovering at home."

The system turns this message into structured search criteria, retrieves real dogs from PawConnect, scores the candidates, and displays cards with match labels, reasons, display tags, and caution tags.

User-facing file:

- `Components/Pages/Adopter/AdoptionCopilot.razor`

Main backend files:

- `Services/AdoptionCopilotService.cs`
- `Services/AdoptionCopilotToolService.cs`
- `Services/OpenAiAdoptionCopilotClient.cs`
- `Services/AdoptionCopilotModels.cs`
- `Services/AdoptionCopilotToolModels.cs`
- `Services/AdoptionCopilotConstraintNormalizer.cs`
- `Services/SemanticDogSearchService.cs`
- `Services/DogSearchDocumentService.cs`
- `Services/DogSearchEmbeddingService.cs`
- `Services/OpenAiEmbeddingService.cs`

## Why It Is Useful

Normal filters require the adopter to know exact fields like size, breed, status, or location. The Copilot allows the user to describe a real-life situation:

- "I have a senior dog at home."
- "I live in an apartment but enjoy longer walks."
- "I have young children."

The app then interprets the underlying adoption need. For example, "I have a sick dog recovering at home" is treated as a need for a dog that will not overwhelm a sensitive dog, not simply as a generic "other dogs" query.

## Complete Flow From User Message to Results

### Step 1: UI Collects the Query

File: `Components/Pages/Adopter/AdoptionCopilot.razor`

Important behavior:

- Page route: `/adopter/copilot`
- Protected with `[Authorize(Roles = "Adopter")]`
- The user enters text in the Copilot input.
- `AskCopilotAsync` calls `AdoptionCopilotService.AskAsync(_currentUserId, _query)`.
- Results are stored in component state and also saved through `ICopilotStateService`.
- Dog details for result cards are reloaded from `IDogService.GetDogDetailsAsync`.

### Step 2: Service Creates Deterministic Search Arguments

File: `Services/AdoptionCopilotService.cs`

The service first builds deterministic search arguments before asking OpenAI. This is important because the app does not rely entirely on AI interpretation.

Relevant model:

- `AdoptionCopilotSearchDogsArgs` in `Services/AdoptionCopilotToolModels.cs`

This model can represent:

- query text
- primary intent
- compatibility target
- sizes
- breeds
- coat colors
- city/neighborhood/shelter
- age range
- statuses
- activity level
- home type
- compatibility signals
- must-have and avoid evidence
- limit/count

Examples of deterministic parsing:

- "black and tan dogs" -> coat color filter.
- "small dog" -> size filter.
- "in Zorilor" -> neighborhood filter.
- "apartment" -> home suitability.
- "longer walks" -> activity preference, not temperament.

### Step 3: Candidate Retrieval Uses Safe Application Tools

File: `Services/AdoptionCopilotToolService.cs`

The main tool is:

- `SearchDogsAsync(AdoptionCopilotSearchDogsArgs args, string? adopterUserId, CancellationToken cancellationToken)`

This tool retrieves public-safe candidates and scores them. It does not expose private database access to OpenAI.

Hard public filters include:

- Available and Reserved dogs by default.
- Adopted and InTreatment dogs are excluded from public Copilot results by default.
- Explicit size, coat color, breed, city, neighborhood, shelter, age, and status filters are applied.
- Reserved dogs may appear, but they get an availability caution chip.

### Step 4: Semantic Search Can Support Candidate Ranking

Files:

- `Services/SemanticDogSearchService.cs`
- `Services/DogSearchDocumentService.cs`
- `Services/DogSearchEmbeddingService.cs`
- `Services/OpenAiEmbeddingService.cs`
- `Entities/DogSearchEmbedding.cs`

If OpenAI embeddings are configured, PawConnect can generate query embeddings and compare them with stored dog search embeddings. This helps match meaning, not just exact words.

Example:

- Dog text: "settles indoors after short walks"
- Query: "calm apartment dog"
- Semantic search can connect these ideas even if the exact word "apartment" is not in the dog text.

Fallback:

- If OpenAI is disabled, the API key is missing, embedding generation fails, or embeddings are missing, `SemanticDogSearchService` falls back to deterministic keyword/rule-based search.

### Step 5: Evidence Extraction and Scoring

File: `Services/AdoptionCopilotToolService.cs`

The tool service extracts dog evidence from public-safe fields only:

- `Dog.Size`
- `Dog.Status`
- `Dog.Description`
- `Dog.BehaviorDescription`
- `Dog.Location`
- `Shelter.City`
- `Shelter.Neighborhood`
- `Dog.CoatColor`
- formatted breed data

Relevant models:

- `CopilotIntent`
- `EvidenceItem`
- `CopilotDogEvidence`
- `AdoptionCopilotToolDogCandidate`

Evidence is grouped into:

- direct evidence
- indirect evidence
- generic evidence
- caution evidence
- negative evidence
- missing evidence

Example:

For "I have a cat at home":

- Direct evidence: "calm near cats", "redirectable around cats", "slow cat introductions".
- Caution/negative evidence: "strong interest in fast-moving small animals", "not suitable with cats".
- Unrelated lifestyle tags such as "short walks" should not be shown as cat evidence.

### Step 6: Optional OpenAI Tool Calling

File: `Services/OpenAiAdoptionCopilotClient.cs`

If `OpenAI:Enabled` is true and `OpenAI:ApiKey` exists, the Copilot can use OpenAI with tool/function calling.

Important method:

- `AskWithToolsAsync(...)`

Configured tools:

- `search_dogs`
- `get_adopter_profile_summary`
- `get_favorite_and_recent_preferences`
- `get_dog_details_public`

The AI does not call SQL directly. It requests a tool call, and PawConnect executes that tool through application services.

The OpenAI client uses the OpenAI Responses API endpoint:

- `v1/responses`

The prompt instructs the model to:

- identify the real-life adoption constraint
- select only dogs returned by tools
- not invent dogs
- not invent unsupported tags
- use display/caution tags from the backend
- keep explanations concise
- return JSON

### Step 7: Backend Validation of OpenAI Output

File: `Services/AdoptionCopilotService.cs`

The backend validates OpenAI output before showing it.

Important safety behavior:

- Unknown dog IDs are ignored.
- Dog IDs outside the latest backend candidate search are ignored.
- Unsupported tags/reasons are not trusted.
- AI scores are capped relative to backend scores.
- Reserved dogs remain capped/cautioned.
- If OpenAI fails or gives no usable result, PawConnect returns fallback rule-based results.

This is the most important answer if the committee asks: "Can the AI invent dogs?"

Answer: No. The AI can propose dog IDs, but the backend only accepts IDs from real candidates returned by PawConnect services.

## What Data Copilot Can Access

Through sanitized tool DTOs, Copilot can access public dog information:

File: `Services/AdoptionCopilotToolModels.cs`

Model:

- `AdoptionCopilotDogToolDto`

Included data:

- dog ID
- name
- formatted breed
- coat color
- age
- size
- public status
- public description
- public behavior description
- shelter name/city/neighborhood
- public image URL
- score/reasons/tags/evidence produced by PawConnect

## What Data Copilot Cannot Access

The Copilot should not receive:

- passwords or tokens
- raw SQL access
- arbitrary user IDs
- private adopter full contact details in candidate data
- shelter internal notes
- audit logs
- SMTP credentials
- database connection strings
- private medical details beyond public dog profile fields

Important files for privacy:

- `Services/AdoptionCopilotToolModels.cs`
- `Services/AdoptionCopilotToolService.cs`
- `Services/OpenAiAdoptionCopilotClient.cs`

## How Suggestions Are Displayed

File: `Components/Pages/Adopter/AdoptionCopilot.razor`

The UI displays:

- Copilot summary message
- source chips such as "AI-assisted explanation", "Semantic search", "Used PawConnect data"
- interpreted constraint chips
- dog result cards
- match percentage/label for recommendation-style queries
- "Exact match" / "Matches request" style labels for deterministic filter queries
- display tags
- caution tags
- View Details and Save actions

## Copilot State Management

File: `Services/CopilotStateService.cs`

Purpose:

- Stores the last Copilot query and results during the session.
- Allows the `/adopter/copilot` page to restore results when the user navigates back.

Test file:

- `PawConnect.Tests/Tests/CopilotStateServiceTests.cs`

## Deterministic Filters vs Suitability Queries

The Copilot separates simple filters from suitability questions.

| Query type | Example | Expected behavior |
| --- | --- | --- |
| Filter-only | "black and tan dogs" | Hard filter by `CoatColor`; show "Exact match" or "Matches request". |
| Suitability | "calm dog for an apartment" | Score dogs based on apartment/lifestyle evidence. |
| Mixed | "medium black dog for apartment" | Apply hard filters first, then score remaining dogs for apartment fit. |

This logic is mainly in:

- `Services/AdoptionCopilotService.cs`
- `Services/AdoptionCopilotToolService.cs`
- `Services/AdoptionCopilotConstraintNormalizer.cs`

## Difference Between Browse, Recommendations, and Copilot

| Feature | Input | Main logic | Output |
| --- | --- | --- | --- |
| Browse dogs | Explicit UI filters such as size, breed, status, neighborhood, coat color. | `DogService.SearchDogsAsync` applies EF Core filters over public-safe dog records. | Public dog list. |
| Recommended Dogs | Adopter profile, favorites, recently viewed dogs. | `DogRecommendationService` scores dogs by home fit, location fit, behavior fit, experience fit, and preferences, with optional OpenAI enhancement. | Personalized recommendation cards. |
| Adoption Copilot | Natural-language query. | `AdoptionCopilotService` interprets intent, uses `AdoptionCopilotToolService` for candidates/evidence/scoring, optional OpenAI tool calling, and backend validation. | Query-specific dog suggestions with chips, tags, cautions, and explanations. |

For recommendations, the sanitized OpenAI request is defined in `Services/RecommendationOpenAiRequest.cs`. It includes limited adopter profile fields such as city, housing type, yard, pets, children, and experience, plus backend-provided candidate dogs. It does not let OpenAI add dogs outside those candidates.

## Example Flow 1: Small Friendly Dog

### User input

"Find me a small friendly dog."

### Interpretation

- Size constraint: Small
- Temperament preference: Friendly
- Public statuses: Available, Reserved by default

### Code involved

- UI: `Components/Pages/Adopter/AdoptionCopilot.razor`
- Orchestration: `Services/AdoptionCopilotService.cs`
- Search/scoring: `Services/AdoptionCopilotToolService.cs`
- Public dog data: `Services/DogService.cs`, `Entities/Dog.cs`

### Data queried

- `Dogs`
- `Shelters`
- `DogImages`
- `DogBreeds`

### Output

The result cards should prioritize small dogs whose public text supports friendly/gentle/social behavior. If a dog is small but has no behavior evidence, it should not be treated as a strongest recommendation.

### What to say to the committee

"The Copilot first detects explicit filters like size, then evaluates softer behavior signals from the public description and behavior fields. This keeps exact constraints and suitability scoring separate."

## Example Flow 2: Specific Neighborhood

### User input

"Show me family-friendly dogs in Zorilor."

### Interpretation

- Neighborhood hard filter: Zorilor
- Compatibility/temperament preference: family/children
- Status default: Available, Reserved

### Code involved

- `AdoptionCopilotService.BuildDeterministicSearchArgsAsync`
- `AdoptionCopilotToolService.SearchDogsAsync`
- `DogService.SearchDogsAsync` also supports neighborhood filtering for normal browse.

### Data queried

- `Dogs`
- `Shelters.Neighborhood`

### Output

Only dogs from shelters in the requested neighborhood should remain after hard filtering. Then the Copilot ranks based on child/family evidence.

### What to say to the committee

"Location and neighborhood are hard constraints. The AI cannot decide to include dogs from another neighborhood if the backend filter excludes them."

## Example Flow 3: Age, Breed, Health, or Compatibility

### User input

"I have a cat at home and want a small dog."

### Interpretation

- Compatibility target: Cats
- Size: Small
- Evidence to look for: calm near cats, redirectable around cats, slow cat introductions, low chase interest.
- Negative evidence: chase behavior, strong interest in small animals.

### Code involved

- `AdoptionCopilotSearchDogsArgs` for size and compatibility fields.
- `CopilotIntent` for primary intent/compatibility target.
- `CopilotDogEvidence` for direct/caution/missing evidence.
- `AdoptionCopilotToolService.ExtractDogEvidence` and scoring helpers.

### Data queried

- `Dog.Description`
- `Dog.BehaviorDescription`
- `Dog.Size`
- `Dog.Status`
- shelter public location

### Output

The cards should show cat-relevant tags:

- "Calm near cats"
- "Needs slow cat introductions"
- "Ask shelter about cats"

They should not show unrelated apartment tags like "Short walks" unless the user also asked about apartment/lifestyle.

### What to say to the committee

"The Copilot uses evidence-backed tags. A generic friendly dog is not automatically a strong cat match because cat compatibility requires specific evidence in the public dog profile."

## How Hallucination Is Reduced

PawConnect reduces hallucination through backend control:

1. OpenAI only receives sanitized candidate data.
2. The AI uses predefined tools, not direct database access.
3. The backend executes the tools.
4. Final dog IDs are validated against real candidates.
5. Unsupported dog IDs are ignored.
6. Unsupported tags and reasons are filtered.
7. Fallback results are returned if OpenAI fails.

Important tests:

- `PawConnect.Tests/Tests/SemanticDogSearchServiceTests.cs`
- `PawConnect.Tests/Tests/DogRecommendationServiceTests.cs`

Examples tested include:

- OpenAI cannot inject unknown dog IDs.
- OpenAI disabled/failing still returns fallback results.
- Public-safe filtering excludes unavailable dogs from semantic index/search.
- Coat color and walk preference interpretation are deterministic.

## Limitations

Current limitations:

- The Copilot depends heavily on the quality of dog descriptions and behavior descriptions.
- Compatibility evidence is extracted from text, not from fully structured compatibility database fields.
- OpenAI is optional and depends on API key/network availability.
- Embeddings must be refreshed after dog content changes.
- The Copilot is advisory; shelters must confirm real-world compatibility.
- UI tests for Copilot behavior are limited compared with service tests.

## Future Improvements

Good future work:

- Add structured compatibility fields for cats, children, other dogs, senior dogs, and activity level.
- Add an admin/shelter tool to audit Copilot evidence extracted per dog.
- Add end-to-end browser tests for Copilot UI.
- Add AI evaluation datasets with expected ranking behavior.
- Add a cached/vector-store search layer for larger datasets.
- Add user feedback buttons for Copilot suggestions.

## Possible Committee Questions About Copilot

| Question | Suggested answer |
| --- | --- |
| Does the AI access the database directly? | No. The AI can request predefined tools, but PawConnect executes those tools through services. |
| Can the AI invent a dog? | It can output an unknown ID, but the backend ignores IDs that were not returned by PawConnect candidate search. |
| What happens if OpenAI is disabled? | The Copilot still works with deterministic parsing, semantic/keyword fallback, and rule-based scoring. |
| What private data is sent to OpenAI? | Candidate data is sanitized. It includes public dog details, not passwords, tokens, private notes, audit logs, or raw SQL access. |
| Why use embeddings? | Embeddings compare meaning, so queries can match descriptions even without exact keyword overlap. |
| What is the main limitation? | The quality of results depends on the quality and specificity of public dog descriptions. |

## Best Way to Present Copilot as an Original Contribution

Say this:

"The Copilot is not just a chatbot. It is a controlled AI workflow. The user's natural language is interpreted into structured adoption criteria. PawConnect retrieves only real public-safe dogs, extracts evidence from their public profiles, scores them, and optionally asks OpenAI to help explain or rerank only those candidates. The backend validates everything before display, so the AI is helpful but not the source of truth."
