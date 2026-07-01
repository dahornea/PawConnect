# PawConnect Adoption Copilot - Method-by-Method Guide

This document explains the Adoption Copilot code in a technical but readable way. It is meant as source material for another ChatGPT or for thesis preparation, so it focuses on:

- what each Copilot-related file does
- what the important methods do
- how control moves from method to method
- where OpenAI, semantic search, deterministic parsing, scoring, and UI display fit together

The key idea:

> The Copilot accepts natural language, converts it into structured search intent, searches real PawConnect dog data, optionally asks OpenAI to help interpret/rerank/explain, validates everything, and returns real dog cards to the Blazor UI.

---

## 1. Main Copilot Files

| File | Role in Copilot |
|---|---|
| `Components/Pages/Adopter/AdoptionCopilot.razor` | UI page at `/adopter/copilot`; collects prompt and displays results |
| `Services/IAdoptionCopilotService.cs` | Main Copilot service contract |
| `Services/AdoptionCopilotService.cs` | Main orchestration service; connects UI, deterministic parsing, tools, OpenAI, fallback, and final result |
| `Services/IAdoptionCopilotToolService.cs` | Contract for safe Copilot tools |
| `Services/AdoptionCopilotToolService.cs` | Searches dogs, applies filters, extracts evidence, scores candidates |
| `Services/IOpenAiAdoptionCopilotClient.cs` | Contract for OpenAI Copilot client |
| `Services/OpenAiAdoptionCopilotClient.cs` | Calls OpenAI Responses API with tool/function calling and strict JSON output |
| `Services/AdoptionCopilotModels.cs` | Main response models used by UI and service |
| `Services/AdoptionCopilotToolModels.cs` | Tool argument, intent, candidate, evidence, and DTO models |
| `Services/AdoptionCopilotConstraintNormalizer.cs` | Cleans, deduplicates, and orders summary/criteria chips |
| `Services/CopilotStateService.cs` | Keeps the last Copilot result for the current user while navigating |
| `Services/SemanticDogSearchService.cs` | Optional semantic/keyword search used as ranking support |
| `Services/SemanticDogSearchModels.cs` | Semantic search options/result models |
| `Services/DogSearchDocumentService.cs` | Builds public-safe text documents for dog semantic/keyword search |
| `Services/DogSearchEmbeddingService.cs` | Creates and refreshes dog search embeddings |
| `Services/OpenAiEmbeddingService.cs` | Calls OpenAI embeddings API and calculates cosine similarity |

---

## 2. Complete Flow from User Prompt to Final Dogs

### Short version

1. User types a prompt in `AdoptionCopilot.razor`.
2. `AskCopilotAsync()` sends the prompt to `AdoptionCopilotService.AskAsync()`.
3. `AdoptionCopilotService` builds deterministic search arguments from obvious prompt text.
4. It calls `AdoptionCopilotToolService.SearchDogsAsync()` to create a safe fallback candidate list.
5. If OpenAI is disabled, it returns fallback results.
6. If OpenAI is enabled, `OpenAiAdoptionCopilotClient.AskWithToolsAsync()` starts a tool-calling loop.
7. OpenAI can request safe tools such as `search_dogs`.
8. PawConnect executes tool calls through `AdoptionCopilotService.ExecuteToolCallAsync()`.
9. `AdoptionCopilotToolService` applies public-safe filters, hard filters, semantic support, evidence extraction, scoring, caps, and sorting.
10. OpenAI returns structured JSON with dog IDs, tags, labels, and explanations.
11. `AdoptionCopilotService` validates dog IDs against the PawConnect candidate map.
12. Final `AdoptionCopilotResponse` goes back to the UI.
13. `AdoptionCopilot.razor` displays dog cards, chips, scores, tags, warnings, and actions.

### Method call chain

```text
AdoptionCopilot.razor
  AskCopilotAsync()
    -> AdoptionCopilotService.AskAsync(userId, query)
      -> BuildDeterministicSearchArgsAsync(query)
        -> DetectSizes()
        -> DetectStatuses()
        -> DetectAgeConstraint()
        -> DetectBehaviorTerms()
        -> DetectCompatibility()
        -> DetectPrimaryIntent()
        -> DetectCompatibilityTarget()
        -> DetectMustHaveSignals()
        -> DetectNiceToHaveSignals()
        -> DetectAvoidSignals()
        -> DetectEvidenceToLookFor()
        -> BuildDisplayChipIntent()
      -> AdoptionCopilotToolService.SearchDogsAsync(userId, deterministicArgs)
        -> NormalizeOptionalArguments()
        -> AnalyzeIntent()
        -> BuildAppliedConstraints()
        -> MatchesHardFilters()
        -> GetSemanticRankingsAsync()
          -> SemanticDogSearchService.SearchDogsAsync()
        -> BuildCandidate()
          -> AddLifestyleScores()
          -> AddHouseholdDogContextScores()
          -> AddPetEvidenceScores()
          -> ExtractDogEvidence()
          -> CalculateIntentEvidenceScore()
          -> ApplyCompatibilityEvidenceCaps()
          -> ApplyHomeActivityEvidenceCaps()
          -> CalibrateRecommendationScore()
          -> ApplySatisfiedHardConstraintFloor()
          -> ApplyFinalVisibleDifferentiation()
        -> OrderCopilotCandidates()
      -> if OpenAI disabled: BuildFallbackResponse()
      -> if OpenAI enabled:
        -> OpenAiAdoptionCopilotClient.AskWithToolsAsync()
          -> SendResponsesRequestAsync()
          -> ExtractToolCalls()
          -> AdoptionCopilotService.ExecuteToolCallAsync()
            -> SearchDogsAsync() / GetAdopterProfileSummaryAsync() / GetFavoriteAndRecentPreferencesAsync() / GetDogDetailsPublicAsync()
          -> ExtractOutputText()
          -> DeserializePayload()
      -> BuildAiResult() or BuildFallbackDogResult()
      -> NormalizeAssistantMessage()
      -> AdoptionCopilotConstraintNormalizer.Normalize()
      -> return AdoptionCopilotResponse
```

---

## 3. `Components/Pages/Adopter/AdoptionCopilot.razor`

This is the Blazor page the adopter uses. It is routed at:

```csharp
@page "/adopter/copilot"
@attribute [Authorize(Roles = "Adopter")]
```

### Injected services

| Service | Why it is used |
|---|---|
| `AuthenticationStateProvider` | Reads current logged-in user |
| `UserManager<ApplicationUser>` | Loads current `ApplicationUser` |
| `IAdoptionCopilotService` | Main service that handles the Copilot prompt |
| `ICopilotStateService` | Saves/restores last Copilot result |
| `IDogService` | Reloads dog details from database when restoring state |
| `IFavoriteDogService` | Shows and toggles favorite state |
| `NavigationManager` | Navigates to login/details pages |
| `ISnackbar` | Shows user messages |

### Methods

| Method | What it does | Called by |
|---|---|---|
| `OnInitializedAsync()` | Loads current user, favorite state, and previous Copilot state when the page opens. | Blazor lifecycle |
| `AskCopilotAsync()` | Validates current user and prompt, calls `AdoptionCopilotService.AskAsync()`, saves response in `CopilotStateService`, reloads favorite state. | Ask Copilot button |
| `UsePromptAsync(string prompt)` | Sets `_query` to a suggested prompt and immediately asks the Copilot. | Suggested prompt buttons |
| `ClearConversation()` | Clears the query, error, response, and saved Copilot state. | Clear button |
| `LoadCurrentUserAsync()` | Uses authentication state and `UserManager` to set `_currentUserId`. | `OnInitializedAsync()` |
| `LoadFavoriteStateAsync()` | Loads favorite dog IDs for the current adopter. | Initialization and after Copilot response |
| `RestoreCopilotStateAsync()` | Gets saved state from `CopilotStateService` and rebuilds the visible response. | `OnInitializedAsync()` |
| `BuildResponseFromStateAsync(CopilotSessionState state)` | Reloads each dog by ID and rebuilds `AdoptionCopilotResponse`; skips dogs that are no longer `Available` or `Reserved`. | `RestoreCopilotStateAsync()` |
| `ToggleFavoriteAsync(Dog dog)` | Saves/removes a dog from favorites; redirects to login if needed. | Favorite button |
| `GetDogImageUrl(Dog dog)` | Returns the selected real dog image or fallback. | Dog cards |
| `GetShelterLine(Dog dog)` | Formats shelter name, neighborhood, and city for card display. | Dog cards |
| `GetStatusColor(DogStatus status)` | Maps dog status to MudBlazor chip color. | Status chip |
| `GetMatchColor(string matchLevel)` | Maps match label to chip color. | Match chip |
| `IsFilterMatchLabel(string? matchLevel)` | Detects labels like `Exact match` or `Matches request`, where a percentage is less important. | Score/label display |
| `GetReasonDisplays(...)` | Cleans normal reason text before showing chips. | Dog cards |
| `GetEvidenceDisplays(...)` | Chooses display tags/evidence chips to show on the result card. | Dog cards |
| `GetCautionDisplays(...)` | Chooses caution chips such as reserved status or ask-shelter warnings. | Dog cards |
| `FormatConstraintChip(AdoptionCopilotConstraint constraint)` | Formats summary chips like `Home: Apartment` or `Activity: Longer walks`. | Summary section |
| `IsDuplicateMatchedReason(...)` | Avoids repeating the same reason in multiple chip areas. | Reason display |
| `ShortenReason(string reason)` | Makes long reasons more readable on cards. | Reason display |
| `IsShortReasonChip(string reason)` | Decides whether a reason is short enough to display as a chip. | Reason display |
| `GetFavoriteIcon(int dogId)` | Returns filled/outlined favorite icon. | Favorite UI |
| `GetFavoriteColor(int dogId)` | Returns favorite icon color. | Favorite UI |
| `GetFavoriteText(int dogId)` | Returns favorite action text. | Favorite UI |
| `GetDogDetailsUrl(int dogId)` | Builds dog details URL with return URL back to Copilot. | View Details link |

### Important point

The UI does not decide dog matching. It only:

- sends prompt to the service
- displays the service response
- reloads dog records when restoring state
- handles favorite and navigation actions

---

## 4. `Services/IAdoptionCopilotService.cs`

### Methods

| Method | What it does |
|---|---|
| `AskAsync(string adopterUserId, string userMessage, CancellationToken cancellationToken = default)` | Main Copilot entry point. Takes the adopter ID and prompt, returns `AdoptionCopilotResponse`. |

This interface lets the Razor page depend on a contract instead of the concrete service.

---

## 5. `Services/AdoptionCopilotService.cs`

This is the main orchestration service. It does not contain every scoring rule itself. Instead, it coordinates:

- deterministic prompt parsing
- fallback search
- OpenAI tool calling
- tool execution
- validation of OpenAI output
- conversion to final UI models
- assistant message and chip formatting

### Main orchestration methods

| Method | What it does | Calls / Uses |
|---|---|---|
| `AskAsync(...)` | Main flow. Trims prompt, builds deterministic args, checks unresolved neighborhood intent, builds fallback search, optionally calls OpenAI, validates results, returns final response. | `BuildDeterministicSearchArgsAsync`, `toolService.SearchDogsAsync`, `openAiCopilotClient.AskWithToolsAsync`, `BuildFallbackResponse`, `BuildAiResult` |
| `ExecuteToolCallAsync(...)` | Runs a tool requested by OpenAI. Supported tools: `search_dogs`, `get_adopter_profile_summary`, `get_favorite_and_recent_preferences`, `get_dog_details_public`. | `DeserializeArgs`, `MergeDeterministicConstraints`, `toolService` methods |
| `BuildDeterministicSearchArgsAsync(...)` | Parses obvious constraints from the prompt without relying on OpenAI. | many `Detect...` helper methods |
| `MergeDeterministicConstraints(...)` | Ensures hard constraints detected from the original prompt are preserved in OpenAI tool arguments. | Called before `SearchDogsAsync()` in tool execution |
| `MergeListValues(...)` | Combines model-provided lists with deterministic values without duplicates. | `MergeDeterministicConstraints` |

### Fallback and final response methods

| Method | What it does |
|---|---|
| `BuildFallbackResponse(...)` | Builds fallback response from a tool search result when OpenAI is disabled or fails. |
| `BuildFallbackFromCandidates(...)` | Converts backend candidates into final dog results and builds a summary message. |
| `BuildNoResultsMessage(...)` | Creates user-friendly message when no dogs match. |
| `NormalizeAssistantMessage(...)` | Cleans or replaces OpenAI summary text so it matches PawConnect constraints and tone. |
| `BuildAssistantMessage(...)` | Builds fallback assistant message for normal recommendation/suitability queries. |
| `BuildFilterAssistantMessage(...)` | Builds message for filter-only queries, such as coat color, size, status, or location. |
| `GetAppliedConstraintValue(...)` | Reads one value from applied constraint chips. |
| `IsFilterOnlySummary(...)` | Checks whether a summary should be treated as a filter query instead of a compatibility recommendation. |
| `HasExplicitStatusRequest(...)` | Detects whether the user actually asked for a status, so the UI does not say "status filter" when they asked for coat color. |
| `HasAppliedConstraint(...)` | Checks if a particular constraint label/value exists in the interpreted constraints. |

### Dog result construction and validation methods

| Method | What it does |
|---|---|
| `BuildFallbackDogResult(...)` | Converts a backend candidate into `AdoptionCopilotDogResult` without OpenAI enhancement. |
| `BuildAiResult(...)` | Converts an OpenAI result into `AdoptionCopilotDogResult`, but clamps scores and trusts only backend-supported tags/reasons. |
| `HasUncertainPrimaryEvidence(...)` | Detects "Ask shelter..." or missing evidence cases that should cap confidence. |
| `ChooseTrustedTags(...)` | Keeps only tags that were already supported by PawConnect candidate evidence. |
| `BuildMatchedCriteria(...)` | Builds matched criteria chips for a particular dog card. |
| `ChooseTrustedReasons(...)` | Keeps safe, supported reasons from OpenAI or backend candidate data. |
| `IsSupportedReason(...)` | Checks whether a reason is supported by safe reasons or dog data. |
| `AreCompatibleReasonCategories(...)` | Prevents mismatched reason categories from being treated as equivalent. |
| `GetReasonCategory(...)` | Categorizes reasons as size, location, breed, behavior, lifestyle, etc. |
| `PolishReasons(...)` | Cleans a list of reasons before UI display. |
| `PolishReason(...)` | Cleans one reason string. |
| `FormatDogAgeCriterion(...)` | Formats a dog age criterion for matched criteria. |
| `GetMatchedBehaviorValue(...)` | Determines whether requested behavior is supported by the dog. |
| `GetMatchedLifestyleValue(...)` | Determines whether requested lifestyle signal is supported by the dog. |
| `GetMatchedHomeValue(...)` | Determines whether home suitability is supported by dog details. |
| `ToDogDto(...)` | Converts an internal candidate to a sanitized DTO for OpenAI tool output. |

### Deterministic parsing methods

These methods are used mainly by `BuildDeterministicSearchArgsAsync()`.

| Method | What it detects or formats |
|---|---|
| `DetectExplicitNeighborhoodAsync(...)` | Finds exact known neighborhood names from the prompt. |
| `ContainsExplicitNeighborhoodPhrase(...)` | Checks whether the normalized query explicitly contains a neighborhood. |
| `NormalizeForNeighborhoodMatch(...)` | Removes diacritics/casing differences for neighborhood matching. |
| `DetectSizes(...)` | Detects `Small`, `Medium`, `Large`. |
| `DetectStatuses(...)` | Detects `Available`, `Reserved`, etc. |
| `DetectAgeConstraint(...)` | Detects age filters such as under 2, older than 7, at least 5 years. |
| `TryMatchAgeYears(...)` | Helper for age regex matching. |
| `AddSizeIfPresent(...)` | Adds a size if a token appears in the prompt. |
| `DetectBehaviorTerms(...)` | Detects behavior words such as calm, friendly, shy. |
| `DetectTemperamentTags(...)` | Normalizes temperament terms into UI-friendly tags. |
| `DetectCompatibility(...)` | Detects cats, children, other dogs, senior dog, sensitive dog, young dog, etc. |
| `DetectPrimaryIntent(...)` | Decides the main intent category, such as `Compatibility`, `HomeSuitability`, `ActivityLevel`, `Location`, `Size`. |
| `DetectCompatibilityTarget(...)` | Chooses the main compatibility target from detected compatibility values. |
| `HasExistingDogContext(...)` | Detects if the user already has a dog at home. |
| `HasHouseholdDogContext(...)` | Detects household dog context, including older/sick/young dog at home. |
| `DetectExperienceLevel(...)` | Detects beginner/experienced adopter intent. |
| `DetectMustHaveSignals(...)` | Finds explicit must-have evidence, like short walks or calm around cats. |
| `DetectNiceToHaveSignals(...)` | Finds helpful but less strict evidence signals. |
| `DetectAvoidSignals(...)` | Finds negative/avoid signals, such as chase behavior or too energetic. |
| `DetectEvidenceToLookFor(...)` | Builds evidence hints for OpenAI/tools based on query and compatibility target. |
| `BuildDisplayChipIntent(...)` | Builds summary chip intent values, such as `Activity: Longer walks`. |
| `AddTermIfAny(...)` | Helper for adding canonical terms based on variants. |
| `DetectEnergyLevel(...)` | Detects low/medium/high energy. |
| `HasExplicitLongerWalksRequest(...)` | Detects longer-walk wording. |
| `HasExplicitDailyWalksRequest(...)` | Detects daily-walk wording. |
| `HasExplicitModerateActivityRequest(...)` | Detects moderate activity wording. |
| `HasExplicitActivityPreference(...)` | Detects whether the prompt explicitly mentions activity, so apartment does not automatically mean low activity. |
| `DetectHouseholdDogActivityLevel(...)` | Detects activity/energy context for dogs already at home. |
| `DetectHomeType(...)` | Detects apartment, house, yard. |
| `DetectCity(...)` | Detects known city/location text. |
| `DetectHousingPreference(...)` | Builds housing preference text. |
| `DetectApartmentFriendly(...)` | Detects apartment-friendly request. |
| `DetectYardFriendly(...)` | Detects yard/house request. |
| `DetectYardRequired(...)` | Detects if a yard is required. |
| `DetectNeedsYard(...)` | Detects if the user implies a dog needing yard/outdoor space. |
| `DetectChildrenPreference(...)` | Detects children/family request. |
| `DetectPetPreference(...)` | Detects pets/cats/dogs at home request. |
| `ContainsAny(...)` | General helper for phrase detection. |
| `HasUnresolvedNeighborhoodIntent(...)` | Detects when the user asked for a neighborhood but no specific area was identified. |
| `BuildDeterministicConstraintPreview(...)` | Builds chips even before OpenAI/tool flow completes. |

### Normalization and display formatting methods

| Method | What it does |
|---|---|
| `SplitSingle(...)` | Splits comma-separated values. |
| `NormalizeTemperamentValues(...)` | Normalizes temperament values and removes misplaced activity/home values. |
| `NormalizeCompatibilityValues(...)` | Normalizes compatibility names, such as cats/children/senior dog. |
| `FormatLifestyleConstraint(string? energyLevel)` | Formats simple lifestyle value. |
| `FormatLifestyleConstraint(AdoptionCopilotSearchDogsArgs args)` | Formats lifestyle constraints from search args. |
| `FormatActivityConstraints(...)` | Formats activity chips, such as short walks or longer walks. |
| `FormatHomeConstraints(...)` | Formats home chips, such as apartment or yard. |
| `FormatAgeConstraint(...)` | Formats age filter chip. |
| `PluralizeYear(...)` | Formats `year` vs `years`. |
| `NormalizeMatchLabel(...)` | Normalizes label text returned by OpenAI/backend. |
| `GetMatchLabel(...)` | Converts numeric score to label. |
| `IsFilterMatchLabel(...)` | Detects filter-style labels, such as `Exact match`. |
| `EmptyToNull(...)` | Converts blank string to null. |

### How to explain this file simply

`AdoptionCopilotService` is the controller/orchestrator for the Copilot logic. It does not trust OpenAI blindly. It first prepares deterministic constraints and a backend fallback, then optionally lets OpenAI help through tool calling. After OpenAI returns results, this service validates dog IDs, clamps scores, filters tags/reasons, and returns a safe final response to the UI.

---

## 6. `Services/IAdoptionCopilotToolService.cs`

This interface defines the safe operations OpenAI or the fallback path can use.

| Method | What it does |
|---|---|
| `SearchDogsAsync(...)` | Searches public-safe dog candidates using structured arguments. |
| `GetAdopterProfileSummaryAsync(...)` | Returns sanitized adopter profile summary for current user only. |
| `GetFavoriteAndRecentPreferencesAsync(...)` | Returns aggregate favorite/recent dog preferences for current user. |
| `GetDogDetailsPublicAsync(...)` | Returns one public-safe dog candidate by ID. |

Important:

> These are controlled application tools, not raw database access.

---

## 7. `Services/AdoptionCopilotToolService.cs`

This is the largest and most important backend file for actual dog matching. It:

- loads public-safe dogs
- applies hard filters
- optionally uses semantic search
- builds candidates
- extracts evidence from dog descriptions
- calculates and calibrates scores
- creates display tags and caution tags

### Public tool methods

| Method | What it does |
|---|---|
| `SearchDogsAsync(...)` | Main search tool. Normalizes args, analyzes intent, loads public-safe dogs, applies hard filters, calls semantic ranking, builds/scored candidates, returns `AdoptionCopilotToolSearchResult`. |
| `GetAdopterProfileSummaryAsync(...)` | Loads current adopter profile only and returns city, housing type, yard/pets/children, and dog experience. |
| `GetFavoriteAndRecentPreferencesAsync(...)` | Loads favorite/recent dogs for current adopter and returns common sizes, breeds, and shelter cities. |
| `GetDogDetailsPublicAsync(...)` | Loads one dog only if it is `Available` or `Reserved`, then builds a candidate object. |

### Candidate ordering methods

| Method | What it does |
|---|---|
| `OrderCopilotCandidates(...)` | Sorts candidates using custom comparison and limits result count. |
| `CompareCopilotCandidates(...)` | Sorts by score, but if scores are close it uses visible ranking signal first. |
| `CalculateVisibleRankingSignal(...)` | Gives tie-break bonuses/penalties based on visible display and caution tags, especially for apartment plus moderate/longer-walk queries. |

### Filtering and candidate building

| Method | What it does |
|---|---|
| `GetSemanticRankingsAsync(...)` | Calls `SemanticDogSearchService` with size/status/location/coat filters and maps semantic results by dog ID. |
| `MatchesHardFilters(...)` | Applies hard constraints: public status, requested status, size, breed, coat color, city, neighborhood, shelter, age, calm-energy conflicts, distance/radius. |
| `HasConflictingEnergyForCalmRequest(...)` | Excludes dogs that clearly conflict with a calm request. |
| `BuildCandidate(...)` | Builds one scored candidate: starts with base score, adds filter/lifestyle/location/semantic signals, extracts evidence, applies caps/calibration, builds tags and next action. |
| `BuildSuggestedNextAction(...)` | Creates a short user action based on intent and cautions. |

### Score caps and calibration

| Method | What it does |
|---|---|
| `ApplyCompatibilityEvidenceCaps(...)` | Caps compatibility scores when direct evidence is weak or missing. |
| `ApplyHomeActivityEvidenceCaps(...)` | Caps apartment/low-activity or active/yard scores when direct evidence is weak. |
| `CalibrateRecommendationScore(...)` | Converts raw points into UI-friendly percentages. |
| `ApplyIntentConfidenceCaps(...)` | Applies confidence caps based on evidence weight and material cautions. |
| `CalculatePrimaryIntentEvidenceWeight(...)` | Gives different weights to evidence depending on intent. Example: longer walks matters more for longer-walk apartment query. |
| `HasMaterialCaution(...)` | Checks if caution/missing evidence is serious enough to affect confidence. |
| `IsModerateApartmentIntent(...)` | Detects apartment queries with medium/high activity, such as apartment plus longer walks. |
| `ApplySatisfiedHardConstraintFloor(...)` | Gives a reasonable floor when hard constraints are satisfied, without over-inflating poor suitability. |
| `CalculateCautionPenalty(...)` | Assigns penalty points for caution tags, such as `Needs more space` or `Patient adopter needed`. |
| `ApplyFinalVisibleDifferentiation(...)` | Makes close scores line up better with visible card tags and cautions. |
| `CountApartmentSupportSignals(...)` | Counts apartment-supporting tags like medium size, settles quickly, indoor rest. |
| `GetFilterOnlyScore(...)` | Returns conservative score for filter-only requests, though UI can show `Exact match`. |
| `GetMatchLabel(...)` | Converts score to `Strong match`, `Good match`, `Possible match`, or `Low match`. |

### Filter-only and hard-constraint helpers

| Method | What it does |
|---|---|
| `HasStrongPrimaryEvidence(...)` | Checks if a result has strong direct evidence for the main intent. |
| `IsFilterOnlyRequest(...)` | Decides whether query is simple filtering rather than suitability scoring. |
| `IsClearlyFilterOnlyQuery(...)` | Detects simple queries like color/size/status/location only. |
| `HasExplicitHardConstraints(...)` | Checks if hard constraints were requested. |
| `CountExplicitHardConstraints(...)` | Counts hard constraints for scoring floors. |
| `HasSoftSuitabilitySignals(...)` | Checks if query also has lifestyle/compatibility preferences. |
| `HasExplicitStatusConstraint(...)` | Detects whether status was explicitly requested. |

### Compatibility evidence helpers

| Method | What it does |
|---|---|
| `HasUncertainPrimaryEvidence(...)` | Checks for `Ask shelter...` or missing evidence tags. |
| `HasPrimaryCompatibilityCaution(...)` | Checks cautions for current compatibility target. |
| `HasDirectEvidenceForPrimaryCompatibility(...)` | Checks if direct evidence exists for cats, children, senior/sensitive dogs, etc. |
| `HasUsefulCatEvidence(...)` | Checks cat-specific useful evidence. |
| `HasUsefulSeniorDogEvidence(...)` | Checks senior/sensitive dog evidence. |
| `HasDirectChildEvidence(...)` | Checks direct child/family evidence. |
| `HasStrongDirectSeniorDogEvidence(...)` | Checks strong senior/sensitive dog direct evidence. |
| `HasAnySeniorDogDirectEvidence(...)` | Checks any direct dog-to-dog evidence for senior/sensitive context. |
| `HasSeniorDogEvidenceCaution(...)` | Detects caution for senior/sensitive dog requests. |
| `HasMissingPrimaryCompatibilityEvidence(...)` | Detects missing main compatibility evidence. |
| `CalculateIntentEvidenceScore(...)` | Adds/subtracts raw points based on direct/indirect/generic/caution/missing evidence. |

### Evidence extraction and evidence item methods

| Method | What it does |
|---|---|
| `ExtractDogEvidence(...)` | Main evidence extractor. Converts public dog text and metadata into display tags, caution tags, direct/indirect/generic/negative/missing evidence. |
| `BuildEvidenceItems(...)` | Converts tag labels into `EvidenceItem` objects. |
| `CreateEvidenceItem(...)` | Creates one evidence item with label, strength, source field, and matched text. |
| `InferEvidenceSourceField(...)` | Guesses where evidence came from: description, behavior, size, status, shelter, etc. |
| `FindEvidenceSentence(...)` | Finds matching sentence in description/behavior text. |
| `GetEvidenceTerms(...)` | Maps evidence labels to phrases used for source matching. |
| `HasDirectSeniorDogDisplayEvidence(...)` | Checks if visible tags include direct senior/sensitive dog evidence. |
| `HasDirectChildDisplayEvidence(...)` | Checks if visible tags include child evidence. |
| `HasDogToDogGentlePlayEvidence(...)` | Detects gentle dog-to-dog play evidence. |
| `ClassifyDirectEvidence(...)` | Classifies display tags as direct evidence for current intent. |
| `ClassifyIndirectEvidence(...)` | Classifies display tags as indirect evidence. |
| `ClassifyGenericEvidence(...)` | Detects generic positive text such as friendly/sweet when relevant. |
| `BuildMissingEvidence(...)` | Adds missing-evidence labels for unsupported primary compatibility. |

### Display tag methods

| Method | What it does |
|---|---|
| `IsDisplayTagRelevantToIntent(...)` | Filters card tags so cats query does not show apartment tags, children query does not show unrelated tags, etc. |
| `AddDisplayTag(...)` | Adds a tag if not empty and not duplicate. |
| `NormalizeDisplayTag(...)` | Converts raw reasons into known display tags. |
| `IsDisplayTagBackedByDogData(...)` | Prevents showing a tag if the dog's data does not support it. |

### Lifestyle, household, and pet scoring methods

| Method | What it does |
|---|---|
| `AddLifestyleScores(...)` | Adds raw points and reasons for apartment, yard, calm, friendly, activity, size, and routine matches. |
| `AddHouseholdDogContextScores(...)` | Adds points/cautions for users who already have older/sick/young dogs at home. |
| `HasPositiveActiveEvidence(...)` | Detects active-owner fit signals. |
| `HasPositiveYardEvidence(...)` | Detects yard/outdoor-space evidence. |
| `HasBeginnerEvidence(...)` | Detects beginner-friendly routine/guidance evidence. |
| `HasPositiveChildrenEvidence(...)` | Detects children/family evidence. |
| `AddPetEvidenceScores(...)` | Adds cat/other-pet/other-dog evidence scores. |

### General relevance and text helpers

| Method | What it does |
|---|---|
| `IsSemanticReasonRelevantToCopilotArgs(...)` | Keeps semantic-search reasons only when relevant to current query. |
| `HasSparseProfileText(...)` | Penalizes dogs with very little description/behavior text. |
| `AddReason(...)` | Adds non-empty, non-duplicate reasons. |
| `HasIntent(...)` | Checks query args for terms across fields. |
| `NormalizePrimaryIntent(...)` | Cleans primary intent value. |
| `HasCompatibility(...)` | Checks args for compatibility values. |
| `IsCompatibilityTarget(CopilotIntent, ...)` | Checks intent compatibility target. |
| `IsCompatibilityTarget(string?, ...)` | Checks raw compatibility target string. |
| `HasCatRequest(...)` | Detects cat compatibility request. |
| `HasChildrenRequest(...)` | Detects child compatibility request. |
| `HasYoungChildrenRequest(...)` | Detects stricter young-child request. |
| `HasExplicitLowActivityRequest(...)` | Detects low activity request. |
| `HasExplicitLongerWalksRequest(...)` | Detects longer-walk request. |
| `HasExplicitDailyWalksRequest(...)` | Detects daily-walk request. |
| `HasExplicitModerateActivityRequest(...)` | Detects moderate activity request. |
| `HasOtherDogsRequest(...)` | Detects other-dog compatibility request. |
| `HasSeniorOrSensitiveHouseholdDogRequest(...)` | Detects older/sick/sensitive dog already at home. |
| `HasSeniorDogAtHomePhrase(...)` | Detects senior dog at home phrases. |
| `HasExplicitNumericAgeRequest(...)` | Detects numeric age requests. |
| `HasYoungHouseholdDogRequest(...)` | Detects young/playful dog at home. |
| `IsCalmRequest(...)` | Detects calm/low-activity intent. |
| `HasCalmSignal(...)` | Detects calm text in dog profile. |
| `HasCalmDogPreferenceSignal(...)` | Detects preference for calm dog company. |
| `HasSeniorDogOverwhelmRisk(...)` | Detects risk for senior/sensitive dog at home. |
| `HasActiveSignal(...)` | Detects active/high-energy text. |
| `HasShortWalkEvidence(...)` | Detects short-walk evidence. |
| `HasDailyWalkEvidence(...)` | Detects daily-walk evidence. |
| `HasLongerWalkEvidence(...)` | Detects longer-walk evidence. |
| `HasHigherActivityNeedSignal(...)` | Detects high activity caution. |
| `HasExplicitApartmentFitEvidence(...)` | Detects explicit apartment-supporting evidence. |
| `IsApartmentRequest(...)` | Checks whether request includes apartment intent. |
| `IsYardRequest(...)` | Checks whether request includes yard/house-with-yard intent. |
| `BuildSearchableDogText(...)` | Combines public dog fields into lowercase text used by rule matching. |
| `BuildQueryTerms(...)` | Builds keyword terms from query arguments. |

### Constraint, parsing, and utility methods

| Method | What it does |
|---|---|
| `MergeValues(...)` | Merges multiple value lists without duplicates. |
| `SplitSingle(...)` | Splits comma-separated values. |
| `NormalizeTemperamentValues(...)` | Normalizes temperament values. |
| `NormalizeCompatibilityValues(...)` | Normalizes compatibility values. |
| `FormatLifestyleConstraint(string?)` | Formats energy/lifestyle text. |
| `FormatLifestyleConstraint(AdoptionCopilotSearchDogsArgs)` | Formats lifestyle from args. |
| `FormatHomeConstraints(...)` | Formats home constraints. |
| `BuildAppliedConstraints(...)` | Builds summary chips from args and intent. |
| `FormatActivityConstraints(...)` | Formats activity chips. |
| `AddConstraint(...)` overloads | Adds constraints to the chip list. |
| `AnalyzeIntent(...)` | Builds `CopilotIntent` from structured args. |
| `AddIntentDefaults(...)` | Adds default evidence expectations based on intent. |
| `BuildRealLifeNeed(...)` | Creates plain-language need summary internally. |
| `BuildIntentChips(...)` | Builds intent chips. |
| `FormatCompatibilityTarget(...)` | Formats compatibility target for chips. |
| `NormalizeOptionalArguments(...)` | Cleans optional args before searching. |
| `NormalizeCompatibilityTarget(...)` | Converts compatibility target to canonical form. |
| `InferPrimaryIntent(...)` | Infers primary intent when OpenAI/fallback args are incomplete. |
| `NormalizeHomeType(...)` | Normalizes home type. |
| `MatchesAgeConstraint(...)` | Checks dog age against args. |
| `FormatAgeConstraint(...)` | Formats age chip text. |
| `PluralizeYear(...)` | Formats year/years. |
| `ParseSizes(...)` | Parses size list to enum set. |
| `ParseSize(...)` | Parses one size. |
| `ParseStatuses(...)` | Parses status list to enum set. |
| `ParseCoatColors(...)` | Normalizes coat colors. |
| `ParseStatus(...)` | Parses one status. |
| `Tokenize(...)` | Splits search text into simple terms. |
| `MostCommon(...)` | Finds most common values from favorites/recent dogs. |
| `Contains(...)` | Case-insensitive contains helper. |
| `ContainsAny(...)` | Checks multiple terms. |
| `IsNearestSort(...)` | Checks if sort mode is nearest. |
| `EmptyToNull(...)` | Converts blank to null. |

### How to explain this file simply

`AdoptionCopilotToolService` is the engine that turns structured intent into dog candidates. It decides which dogs are allowed, which dogs match hard filters, which descriptions contain useful evidence, how much each dog should score, and which tags/cautions appear on the cards.

---

## 8. `Services/IOpenAiAdoptionCopilotClient.cs`

### Methods

| Method | What it does |
|---|---|
| `AskWithToolsAsync(...)` | Sends Copilot request to OpenAI and allows the model to call PawConnect tools through a provided executor delegate. |

---

## 9. `Services/OpenAiAdoptionCopilotClient.cs`

This class handles the OpenAI Responses API call. It is deliberately separated from database logic.

### Main OpenAI methods

| Method | What it does |
|---|---|
| `AskWithToolsAsync(...)` | Main OpenAI flow. Builds system/user input, sends request, extracts tool calls, executes tools through delegate, reads final JSON response, normalizes output. |
| `SendResponsesRequestAsync(...)` | Sends HTTP POST to `v1/responses` with model, input, tools, tool choice, and strict JSON response format. |
| `BuildResponseFormat()` | Defines strict JSON schema for the final Copilot response. |
| `BuildTools()` | Defines tools OpenAI may call: `search_dogs`, `get_adopter_profile_summary`, `get_favorite_and_recent_preferences`, `get_dog_details_public`. |

### Schema helper methods

| Method | What it does |
|---|---|
| `EmptyObjectSchema()` | Defines an empty JSON object schema for tools with no args. |
| `StringSchema(...)` | Helper for string JSON schema properties. |
| `NumberSchema(...)` | Helper for numeric schema properties. |
| `BooleanSchema(...)` | Helper for boolean schema properties. |
| `ArraySchema(...)` | Helper for string-array schema properties. |

### Response parsing and cleanup methods

| Method | What it does |
|---|---|
| `ExtractToolCalls(...)` | Reads OpenAI response JSON and extracts function/tool calls. |
| `ExtractOutputText(...)` | Finds final assistant output text from the Responses API JSON. |
| `DeserializePayload(...)` | Parses final strict JSON into internal payload type. |
| `NormalizeMatchLabel(...)` | Converts returned labels to supported PawConnect labels. |
| `NormalizeReasons(...)` | Cleans reason/tag arrays. |
| `SafeTrim(...)` | Trims long strings and handles blank values. |

### How to explain this file simply

This class is only the OpenAI adapter. It sends the prompt and tool definitions to OpenAI, receives either tool calls or final JSON, and normalizes the response. It does not query the database directly.

---

## 10. `Services/AdoptionCopilotConstraintNormalizer.cs`

This file cleans up criteria chips such as:

- `Home: Apartment`
- `Activity: Longer walks`
- `Lifestyle: Moderate activity`
- `Compatibility: Sensitive dog`

### Methods

| Method | What it does |
|---|---|
| `Normalize(...)` | Main method. Groups, deduplicates, normalizes, orders, and returns final constraint chips. |
| `AddValue(...)` | Adds a value under a label without duplicates. |
| `NormalizeValue(...)` | Moves values to correct categories. Example: `longer walks` becomes `Activity`, not `Temperament`. |
| `NormalizeLabel(...)` | Cleans labels such as `Behavior` -> `Temperament`, `CoatColor` -> `Coat color`. |
| `CanonicalizeSimpleValue(...)` | Standardizes common temperament values like calm/friendly/shy. |
| `SplitValues(...)` | Splits comma-separated chip values. |
| `GetLabelOrder(...)` | Sorts chips in a stable order. |
| `ContainsAny(...)` | Helper for phrase detection. |
| `ToTitleCase(...)` | Formats simple values. |

### Why it matters

Without this file, the Copilot could show duplicate or confusing summary chips like:

```text
Activity: Longer walks
Activity: Longer walks, Moderate activity
Temperament: longer walks
```

The normalizer keeps chips clean and categorized.

---

## 11. `Services/CopilotStateService.cs`

This service stores the last Copilot response in memory for the current user context.

### Methods

| Method | What it does |
|---|---|
| `GetState(string? userId)` | Returns saved Copilot state only if it belongs to the same user. |
| `SaveState(string userId, string query, AdoptionCopilotResponse response)` | Saves query, assistant message, result IDs, scores, labels, tags, flags, and generated time. |
| `ClearState(string? userId)` | Clears state if user matches or user ID is blank. |

### Records

| Record | Purpose |
|---|---|
| `CopilotSessionState` | Stores last query, assistant message, constraints, result summaries, AI/semantic/tool flags. |
| `CopilotSavedDogResult` | Stores one saved result using dog ID and display metadata. |

Important:

The state stores dog IDs and display metadata, but `AdoptionCopilot.razor` reloads actual dog records from the database before showing restored cards.

---

## 12. `Services/AdoptionCopilotModels.cs`

This file contains the main models passed between the service and UI.

| Type | Purpose |
|---|---|
| `AdoptionCopilotResponse` | Final response returned to UI. Contains message, dog results, flags, fallback reason, applied constraints. |
| `AdoptionCopilotConstraint` | One summary/matched chip, such as `Home: Apartment`. |
| `AdoptionCopilotDogResult` | One final dog card result for the UI. |
| `AdoptionCopilotToolOpenAiRequest` | Request passed to OpenAI client, containing user message and deterministic constraints. |
| `OpenAiAdoptionCopilotResponse` | Parsed response from OpenAI client. |
| `OpenAiAdoptionCopilotItem` | One dog item returned by OpenAI JSON. |
| `OpenAiCopilotToolCall` | Represents one tool call requested by OpenAI. |
| `OpenAiCopilotToolOutput` | Represents the tool output sent back to OpenAI. |
| `OpenAiCopilotToolExecutor` | Delegate type used so `AdoptionCopilotService` can execute tool calls safely. |

---

## 13. `Services/AdoptionCopilotToolModels.cs`

This file contains models used inside the safe tools.

| Type | Purpose |
|---|---|
| `AdoptionCopilotSearchDogsArgs` | Structured search arguments. Natural language becomes this shape. |
| `CopilotIntent` | Interpreted intent model: primary intent, compatibility target, home type, activity level, evidence needs, chips, statuses, city, sizes, limit. |
| `EvidenceItem` | One evidence item with label, strength, source field, and matched text. |
| `CopilotDogEvidence` | Full evidence model for one dog: direct, indirect, generic, positive, caution, negative, missing, display tags. |
| `AdoptionCopilotToolSearchResult` | Result returned by `SearchDogsAsync`: candidates, constraints, semantic flag, empty reason. |
| `AdoptionCopilotToolDogCandidate` | Internal backend candidate with dog entity, score, label, reasons, tags, evidence. |
| `AdoptionCopilotDogToolDto` | Sanitized dog DTO sent to OpenAI as tool output. |
| `AdoptionCopilotToolJsonResult` | JSON wrapper for tool output. |
| `AdoptionCopilotProfileToolResult` | Sanitized adopter profile summary. |
| `AdoptionCopilotPreferenceToolResult` | Aggregated favorite/recent preferences. |

---

## 14. `Services/SemanticDogSearchService.cs`

Semantic search is optional support for matching meaning, not the main safety layer.

### Methods

| Method | What it does |
|---|---|
| `SearchDogsAsync(...)` | Main semantic search entry. Tries embeddings if enabled, otherwise keyword fallback. |
| `BuildRecommendationMapAsync(...)` | Gets rule-based recommendations for adopter to use as a bonus. |
| `TrySemanticSearchAsync(...)` | Generates query embedding, compares it with stored dog embeddings, adds recommendation/distance bonuses, returns ranked results. |
| `KeywordFallbackSearchAsync(...)` | Uses search documents and keyword scoring when embeddings are unavailable. |
| `BuildResult(...)` | Builds one `SemanticDogSearchResult`. |
| `DogMatchesOptions(...)` | Applies public-safe search options: shelter, size, status, location, neighborhood, coat color, radius. |
| `DeserializeEmbedding(...)` | Converts stored embedding JSON into float array. |
| `Tokenize(...)` | Splits query into search terms. |
| `CalculateKeywordScore(...)` | Gives points for query terms found in dog document. |
| `GetMatchLabel(...)` | Converts semantic score to label. |
| `BuildSearchSummary(...)` | Builds short semantic result summary. |

### How it fits Copilot

`AdoptionCopilotToolService.GetSemanticRankingsAsync()` calls `SemanticDogSearchService.SearchDogsAsync()`. The semantic result can add a small score bonus or reason, but hard filters and public-safe filtering still happen in the Copilot tool service.

---

## 15. `Services/SemanticDogSearchModels.cs`

| Type | Purpose |
|---|---|
| `SemanticDogSearchOptions` | Filters for semantic search: shelter, size, status, location, neighborhood, coat colors, origin coordinates, radius. |
| `SemanticDogSearchResult` | One semantic result: dog ID, dog entity, score, label, reasons, summary, distance, whether embeddings were used. |

---

## 16. `Services/DogSearchDocumentService.cs`

This file builds the text used for semantic and keyword search.

### Methods

| Method | What it does |
|---|---|
| `BuildDocument(Dog dog)` | Builds public-safe searchable text from dog name, age, size, breed, status, location, coat color, shelter, description, behavior, medical summary, and food preference. |
| `ComputeContentHash(string content)` | Hashes the search document so PawConnect can know if an embedding is stale. |

### Why it matters

Embeddings and keyword fallback should be based on public-safe dog profile data, not private user/admin data.

---

## 17. `Services/DogSearchEmbeddingService.cs`

This service maintains stored dog embeddings.

### Methods

| Method | What it does |
|---|---|
| `RefreshDogEmbeddingAsync(int dogId, ...)` | Refreshes embedding for one dog. |
| `RefreshMissingDogEmbeddingsAsync(...)` | Refreshes embeddings that are missing. |
| `RefreshAllDogEmbeddingsAsync(...)` | Refreshes all searchable dog embeddings. |
| `RebuildDogSearchIndexAsync(...)` | Full rebuild: refreshes eligible dog embeddings and removes stale ones. |
| `GetSearchableDogEmbeddingsAsync(...)` | Loads embeddings for public-searchable dogs. |
| `RefreshDogEmbeddingCoreAsync(...)` | Core refresh logic: builds document, hashes content, checks OpenAI config, generates embedding, saves row. |
| `RemoveStaleEmbeddingsAsync(...)` | Removes embeddings for dogs that should no longer be searchable or whose content is stale. |
| `LogOpenAiEmbeddingConfiguration(...)` | Logs whether embedding configuration is valid. |

### How it fits Copilot

This is not called directly by the Copilot page. It supports `SemanticDogSearchService`, which the Copilot tool service can use for better search ranking.

---

## 18. `Services/OpenAiEmbeddingService.cs`

### Methods

| Method | What it does |
|---|---|
| `GenerateEmbeddingAsync(string text, ...)` | Calls OpenAI embeddings API and returns a float vector for text. |
| `CosineSimilarity(...)` | Compares two embedding vectors; higher similarity means closer meaning. |

### How it fits Copilot

This is used by `SemanticDogSearchService.TrySemanticSearchAsync()`, not by the Copilot UI directly.

---

## 19. OpenAI Tool Flow in Detail

When OpenAI is enabled, this sequence happens:

```text
AdoptionCopilotService.AskAsync()
  -> openAiCopilotClient.AskWithToolsAsync(request, executeToolAsync)
    -> OpenAiAdoptionCopilotClient.SendResponsesRequestAsync()
      sends prompt + tool schema to OpenAI
    -> OpenAiAdoptionCopilotClient.ExtractToolCalls()
      sees model requested search_dogs or another tool
    -> executeToolAsync(toolCall)
      implemented by AdoptionCopilotService
    -> AdoptionCopilotService.ExecuteToolCallAsync()
      calls AdoptionCopilotToolService.SearchDogsAsync()
    -> tool output JSON is sent back to OpenAI
    -> model returns final strict JSON
    -> OpenAiAdoptionCopilotClient.DeserializePayload()
    -> AdoptionCopilotService validates dog IDs and builds final UI results
```

Important safety point:

OpenAI never receives raw SQL access. It only receives sanitized tool results.

---

## 20. Fallback Flow in Detail

If OpenAI is disabled, missing API key, fails, or returns invalid results:

```text
AdoptionCopilotService.AskAsync()
  -> BuildDeterministicSearchArgsAsync()
  -> AdoptionCopilotToolService.SearchDogsAsync()
  -> BuildFallbackResponse()
  -> BuildFallbackFromCandidates()
  -> BuildFallbackDogResult()
  -> return AdoptionCopilotResponse
```

Fallback can still:

- parse many common constraints
- filter public-safe dogs
- score candidates
- create tags and cautions
- return useful results

Fallback cannot fully replace OpenAI for complex natural-language interpretation, but the app remains usable.

---

## 21. How to Explain the Copilot in a Thesis Defense

Use this explanation:

> The Copilot is implemented as a structured workflow, not a free-form chatbot. The Blazor page sends the user's natural-language prompt to `AdoptionCopilotService`. The service first extracts deterministic constraints like size, coat color, location, activity level, home type, or compatibility needs. It then calls `AdoptionCopilotToolService`, which loads only public-safe dogs, applies hard filters, extracts evidence from descriptions, scores candidates, and builds display/caution tags. If OpenAI is configured, the model can call safe PawConnect tools through the Responses API, but it never accesses the database directly. Any dog ID returned by OpenAI is validated against the backend candidate list before display. This keeps the AI useful for language interpretation while PawConnect remains responsible for data, privacy, filtering, and final results.

---

## 22. Most Important Methods to Remember

If you can only remember a few methods, remember these:

| Method | Why it matters |
|---|---|
| `AdoptionCopilot.razor -> AskCopilotAsync()` | UI entry point |
| `AdoptionCopilotService.AskAsync()` | Main orchestration method |
| `AdoptionCopilotService.BuildDeterministicSearchArgsAsync()` | Converts prompt into structured constraints before OpenAI |
| `AdoptionCopilotService.ExecuteToolCallAsync()` | Executes OpenAI-requested tools safely |
| `AdoptionCopilotToolService.SearchDogsAsync()` | Main backend search/scoring tool |
| `AdoptionCopilotToolService.MatchesHardFilters()` | Enforces strict user filters |
| `AdoptionCopilotToolService.BuildCandidate()` | Builds one scored dog result |
| `AdoptionCopilotToolService.ExtractDogEvidence()` | Turns dog descriptions into evidence/tags |
| `AdoptionCopilotToolService.CalibrateRecommendationScore()` | Converts raw points into UI score |
| `OpenAiAdoptionCopilotClient.AskWithToolsAsync()` | OpenAI tool-calling loop |
| `OpenAiAdoptionCopilotClient.BuildTools()` | Defines allowed AI tools |
| `SemanticDogSearchService.SearchDogsAsync()` | Optional semantic/keyword search support |

---

## 23. Common Questions and Answers

### What does OpenAI do?

It helps understand natural-language prompts and produce a concise explanation/ranking using only candidates returned by PawConnect tools.

### Does OpenAI decide final dog visibility?

No. PawConnect filters public-safe dogs first and validates dog IDs afterward.

### Where are public-safe rules enforced?

Mainly in `AdoptionCopilotToolService.SearchDogsAsync()` and `MatchesHardFilters()`, where only `Available` and `Reserved` dogs are loaded.

### Where are scores calculated?

Mainly in `AdoptionCopilotToolService.BuildCandidate()`, with helpers such as `ExtractDogEvidence()`, `CalculateIntentEvidenceScore()`, `CalibrateRecommendationScore()`, and `ApplyFinalVisibleDifferentiation()`.

### Where are tags created?

Mostly in `ExtractDogEvidence()`, then filtered by `IsDisplayTagRelevantToIntent()` and `IsDisplayTagBackedByDogData()`.

### Where is OpenAI output validated?

In `AdoptionCopilotService.AskAsync()`, where OpenAI dog IDs are checked against `allowedCandidateMap`.

### What happens if OpenAI fails?

`AdoptionCopilotService.AskAsync()` catches expected failures and returns fallback results built from `AdoptionCopilotToolService.SearchDogsAsync()`.

