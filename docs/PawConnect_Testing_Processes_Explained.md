# PawConnect Testing Processes Explained

## 1. Testing Overview

PawConnect uses automated tests to protect the most important business rules of the application. The current test suite is in `PawConnect.Tests` and uses xUnit with EF Core InMemory. After cleanup, `dotnet test` reports 146 tests.

The tests focus mainly on service-level business rules because this is where the application enforces:

- public-safe dog visibility
- shelter ownership checks
- adopter ownership checks
- adoption request state transitions
- dog status changes
- resource validation and low-stock behavior
- CSV import/export safety
- notification, report, and audit metadata
- recommendation and Adoption Copilot safety rules

Most tests do not open a browser. This is intentional. In PawConnect, the UI pages call services such as `DogService`, `AdoptionRequestService`, `ResourceStockService`, `DogRecommendationService`, and `AdoptionCopilotService`. The critical rules are enforced in those services, so testing them gives strong confidence without needing slower and more fragile browser end-to-end tests.

EF Core InMemory is used so each test can create an isolated database quickly. Tests do not require a real SQL Server instance. Fake email and PDF services are used so adoption flows, reports, and notifications can be tested without sending real emails or depending on external services.

The main idea:

> The tests focus on service-level business rules because this is where ownership checks, validation, status transitions, and workflow rules are enforced.

## 2. Types of Tests Used

### Unit-style tests

Unit-style tests focus on one service or one small piece of logic. They usually create a small in-memory database or test a helper directly.

Examples:

- `DogServiceTests.CreateDogAsync_InvalidAgeMonthsFailsValidation`
- `FavoriteDogServiceTests.AddFavoriteAsync_DoesNotCreateDuplicateFavorites`
- `ResourceStockServiceTests.GetLowStockResourcesForShelterAsync_ReturnsQuantityAtOrBelowThreshold`
- `DogBreedFormatterTests.Parse_KnownMixedBreedPair_MapsPrimaryAndSecondaryBreeds`
- `DogImageUrlValidatorTests.GetPrimaryRealDogImageUrl_PrefersMainRealImage`

What to say:

> Unit-style tests verify one rule at a time, such as validation, duplicate prevention, image URL validation, or breed formatting.

### Integration-style service tests

Integration-style service tests exercise multiple entities and services together using EF Core InMemory. They do not start a browser, SQL Server, SMTP server, or OpenAI API, but they simulate realistic backend workflows.

Examples:

- `ServiceFlowIntegrationTests.AdoptionRequestFlow_SubmitViewAcceptAndNotifyWithPdfAttachment`
- `ServiceFlowIntegrationTests.ResourceStockFlow_EnforcesOwnershipLowStockAndNotifications`
- `ServiceFlowIntegrationTests.FavoritesAndDogDeletionFlow_KeepsFavoritesPrivateAndDeletesFavoriteOnlyDog`
- `CsvImportServiceTests.ImportedShelterRequest_CanBeAcceptedThroughExistingApprovalFlow`

What to say:

> These are not full browser end-to-end tests. They are integration-style service tests because they use real EF Core entities and services together, but with fake external dependencies.

## 3. Test Infrastructure

| Helper/File | Purpose | Why it matters |
| ----------- | ------- | -------------- |
| `PawConnect.Tests/PawConnect.Tests.csproj` | Defines the test project. Uses xUnit, `Microsoft.EntityFrameworkCore.InMemory`, `Microsoft.NET.Test.Sdk`, and `coverlet.collector`. | Shows the project uses standard .NET testing tools and can be run with `dotnet test`. |
| `PawConnect.Tests/Tests/Helpers/TestDbContextFactory.cs` | Creates isolated `ApplicationDbContext` instances using EF Core InMemory. Seeds roles, users, shelters, resource categories, food type, and dog breeds. | Makes service tests fast, isolated, and repeatable. |
| `TestDbContextFactory.CreateContext()` | Creates a fresh in-memory database and seeds test identity/lookups. | Prevents tests from depending on a shared database. |
| `TestDbContextFactory.CreateUserManager()` | Creates a real `UserManager<ApplicationUser>` backed by the in-memory context. | Allows tests to verify Identity-related flows such as shelter account creation. |
| `TestDbContextFactory.CreateDog()` | Creates a valid default dog for tests. | Reduces repeated setup and keeps dog tests readable. |
| `PawConnect.Tests/Tests/Helpers/TestDoubles.cs` | Contains `TestEmailService` and `TestPdfReportService`. | Lets tests verify emails/PDF attachments without sending real emails or generating full production PDFs. |
| `TestEmailService` | Captures sent emails in memory and can throw when needed. | Tests adoption request emails, visit reminders, and best-effort failure behavior. |
| `TestPdfReportService` | Returns small fake PDF bytes for report methods. | Tests report attachment flow without depending on full PDF rendering. |
| Fake OpenAI clients inside `SemanticDogSearchServiceTests.cs` and `DogRecommendationServiceTests.cs` | Simulate OpenAI success, failure, unknown IDs, and captured requests. | Tests AI safety without making live OpenAI API calls. |
| Fake geocoding service inside `SemanticDogSearchServiceTests.cs` | Returns fixed coordinates for Copilot/search tests. | Avoids external geocoding calls while testing location-related behavior. |

## 4. Processes Tested

### 4.1 Public Dog Visibility

What is tested:

- `Available` dogs are visible publicly.
- `Reserved` dogs are visible publicly.
- `Adopted` dogs are hidden from public/adopter browsing.
- `InTreatment` dogs are hidden from public/adopter browsing.

Example tests:

- `DogServiceTests.GetAvailableDogsAsync_ReturnsOnlyAvailableAndReservedDogs`
- `ServiceFlowIntegrationTests.PublicDogFlow_ReturnsOnlyPublicSafeDogsForListingAndFeaturedPreview`
- `DogRecommendationServiceTests.RuleBasedRecommendations_ExcludeAdoptedAndInTreatmentDogs`
- `SemanticDogSearchServiceTests.AdoptionCopilotToolSearch_ReturnsOnlyPublicSafeDogs`
- `SemanticDogSearchServiceTests.RebuildIndex_CreatesEmbeddingsForPublicSafeDogsOnly`

Why it matters:

Public visitors and adopters should only see dogs that can realistically be considered for adoption. Adopted dogs and dogs in treatment should not appear in public search, recommendations, or Copilot results.

Presentation explanation:

> I test public-safe filtering in normal browsing, recommendations, Copilot tools, and semantic search indexing because this rule protects the adoption workflow across multiple features.

### 4.2 Dog Management

What is tested:

- creating dogs
- age validation
- deleting dogs
- deletion blocked when adoption request history exists
- favorites are removed when a favorite-only dog is deleted
- public search filters by shelter, neighborhood, and coat color
- dog breed formatting/custom breed support
- dog status history retrieval

Example tests:

- `DogServiceTests.CreateDogAsync_InvalidAgeMonthsFailsValidation`
- `DogServiceTests.DeleteDogAsync_DeletesDogWithoutReferences`
- `DogServiceTests.DeleteDogAsync_AllowsDogWithFavoritesAndRemovesFavorites`
- `DogServiceTests.DeleteDogAsync_BlocksDogWithAdoptionRequestHistory`
- `DogServiceTests.SearchDogsAsync_FiltersPublicDogsByShelter`
- `DogServiceTests.SearchDogsAsync_FiltersPublicDogsByShelterNeighborhood`
- `DogServiceTests.SearchDogsAsync_FiltersPublicDogsByCoatColor`
- `DogServiceTests.GetStatusHistoryForDogAsync_ReturnsChangedByUserAndNotes`
- `DogBreedFormatterTests.Parse_KnownBreedText_MapsToLookup`

Why it matters:

Dog profiles are the central data of the platform. The tests check that dogs can be managed correctly without breaking adoption history or public visibility.

Presentation explanation:

> Dog management tests check both simple validation, like age, and domain rules, like not deleting dogs that already have adoption request history.

### 4.3 Dog Images

What is tested:

- adding image URLs for a shelter dog
- rejecting empty image URLs
- rejecting invalid or placeholder image URLs
- blocking duplicate image URLs for the same dog
- allowing dogs without images
- blocking another shelter from deleting a dog image
- choosing the main real image for display
- falling back when the main image is a placeholder
- excluding invalid, unavailable, placeholder, and duplicate images

Example tests:

- `DogImageServiceTests.AddDogImageAsync_AddsImageForShelterDog`
- `DogImageServiceTests.AddDogImageAsync_RejectsEmptyImageUrl`
- `DogImageServiceTests.AddDogImageAsync_RejectsInvalidImageUrl`
- `DogImageServiceTests.AddDogImageAsync_BlocksDuplicateImageUrlForSameDog`
- `DogImageServiceTests.DeleteDogImageAsync_BlocksOtherShelter`
- `DogImageUrlValidatorTests.GetPrimaryRealDogImageUrl_PrefersMainRealImage`
- `DogImageUrlValidatorTests.GetPrimaryRealDogImageUrl_FallsBackWhenMainImageIsPlaceholder`
- `DogImageUrlValidatorTests.GetRealDogImages_ExcludesInvalidUnavailablePlaceholderAndDuplicateImages`

Why it matters:

The dog image gallery and public dog cards should show real dog images when available and should not store placeholder/broken URLs as real dog images.

Presentation explanation:

> Image tests protect both input validation and display logic: invalid URLs are rejected, and placeholders are only used as UI fallback.

### 4.4 Adoption Request Workflow

What is tested:

- adopter creates an adoption request
- questionnaire fields are saved
- non-adopters cannot create adoption requests
- duplicate pending requests are blocked
- requests for adopted dogs are blocked
- preferred visit time is validated
- past visit times are rejected
- visits outside shelter hours are rejected
- pending request can be cancelled by its owner
- another adopter cannot cancel someone else's request
- admin details graph is loaded for request details

Example tests:

- `AdoptionRequestServiceTests.CreateRequestAsync_SavesQuestionnaireFieldsForAvailableDog`
- `AdoptionRequestServiceTests.CreateRequestAsync_BlocksNonAdopterUsers`
- `AdoptionRequestServiceTests.CreateRequestAsync_BlocksDuplicatePendingRequest`
- `AdoptionRequestServiceTests.CreateRequestAsync_BlocksAdoptedDog`
- `AdoptionRequestServiceTests.CreateRequestAsync_BlocksPastVisitTime`
- `AdoptionRequestServiceTests.CreateRequestAsync_BlocksVisitOutsideShelterHours`
- `AdoptionRequestServiceTests.CancelRequestAsync_OnlyCancelsPendingOwnRequest`
- `AdoptionRequestServiceTests.CancelRequestAsync_BlocksAnotherAdopter`
- `AdoptionRequestServiceTests.GetByIdAsync_LoadsAdminDetailsGraph`

Why it matters:

Adoption requests are the core adoption process. These tests check that requests are valid, owned by the right user, and cannot be duplicated or submitted for unavailable dogs.

Presentation explanation:

> I test adoption requests heavily because this is where user intent becomes a real database workflow involving adopters, shelters, dogs, emails, and statuses.

### 4.5 Visit Confirmation and Final Adoption

What is tested:

- shelter confirms a visit
- request becomes `VisitConfirmed`
- visit status becomes `Confirmed`
- dog becomes `Reserved`
- dog status history is created when status changes
- repeated status changes do not create unnecessary history
- confirmed visit emails include calendar attachments
- Bucharest local time is used in calendar invites
- after visit, dog can become `Adopted`
- rejected confirmed visit can return a dog to `Available`

Example tests:

- `AdoptionRequestServiceTests.ConfirmVisitAsync_UpdatesRequestDogStatusAndStatusHistory`
- `AdoptionRequestServiceTests.ConfirmVisitAsync_DoesNotCreateHistoryWhenStatusDoesNotChange`
- `AdoptionRequestServiceTests.ConfirmVisitAsync_BlocksNonPendingRequest`
- `AdoptionRequestServiceTests.ConfirmVisitAsync_BlocksAdoptedDog`
- `AdoptionRequestServiceTests.MarkAsAdoptedAsync_AfterConfirmedVisitUpdatesDogStatus`
- `AdoptionRequestServiceTests.RejectRequestAsync_AfterConfirmedVisitReturnsReservedDogToAvailable`
- `AdoptionRequestServiceTests.ConfirmVisitAsync_SendsCalendarInviteAttachment`
- `AdoptionRequestServiceTests.ConfirmVisitAsync_UsesBucharestLocalTimeInCalendarInvite`
- `VisitReminderServiceTests.SendDueVisitRemindersAsync_SendsEmailWithCalendarAttachmentAndMarksSent`

State transitions:

- Request: `Pending -> VisitConfirmed -> Accepted`
- Request: `Pending -> Rejected`
- Request: `Pending -> Cancelled`
- Dog: `Available -> Reserved -> Adopted`

Presentation explanation:

> The tests verify that adoption status and dog status move together. For example, confirming a visit reserves the dog, and final adoption marks the dog as adopted.

### 4.6 Shelter Ownership and Authorization Rules

What is tested:

- shelter can only manage adoption requests for its own dogs
- shelter can only manage its own dog images
- shelter can only manage its own resources
- adopter can only manage own favorites and requests
- admin-only actions are blocked for non-admin users in shelter application approval
- local return URLs are checked to avoid unsafe redirects

Example tests:

- `AdoptionRequestServiceTests.ShelterCannotManageAnotherSheltersRequest`
- `DogImageServiceTests.DeleteDogImageAsync_BlocksOtherShelter`
- `ResourceStockServiceTests.UpdateResourceAsync_BlocksAnotherSheltersResource`
- `FavoriteDogServiceTests.RemoveFavoriteAsync_OnlyRemovesCurrentAdoptersFavorite`
- `ShelterRegistrationRequestServiceTests.AcceptRequestAsync_BlocksNonAdminUser`
- `LocalReturnUrlHelperTests.IsSafeLocalPath_BlocksExternalOrMalformedValues`

Why it matters:

UI hiding is not enough. A user could try to call a service action directly, so ownership rules must be enforced in the service layer.

Presentation explanation:

> Authorization is checked in the backend services, not only in the UI. The tests prove that a shelter cannot manage another shelter's data.

### 4.7 Favorites and Recently Viewed Dogs

What is tested:

- adopter can favorite a dog
- duplicate favorites are blocked
- non-adopters cannot favorite dogs
- favorite removal only affects the current adopter
- favorites remain private per adopter in integration-style flow
- deleting a dog removes related favorites when deletion is allowed

Example tests:

- `FavoriteDogServiceTests.AddFavoriteAsync_AddsFavoriteForAdopter`
- `FavoriteDogServiceTests.AddFavoriteAsync_DoesNotCreateDuplicateFavorites`
- `FavoriteDogServiceTests.AddFavoriteAsync_BlocksNonAdopterUsers`
- `FavoriteDogServiceTests.RemoveFavoriteAsync_OnlyRemovesCurrentAdoptersFavorite`
- `ServiceFlowIntegrationTests.FavoritesAndDogDeletionFlow_KeepsFavoritesPrivateAndDeletesFavoriteOnlyDog`

Recently viewed:

- The current trimmed test suite does not include a dedicated `RecentlyViewedDogService` test class.
- Recently viewed data is part of the application model and recommendation context, but it is not directly tested in the current suite.

Presentation explanation:

> Favorites are tested for privacy, duplicate prevention, and cleanup. Recently viewed dogs are a possible area for future additional tests.

### 4.8 Resource Stock and Low Stock

What is tested:

- creating shelter resources
- rejecting non-positive quantity
- requiring category
- blocking duplicate resource entries
- requiring food type for food resources
- clearing food type for non-food resources
- shelter ownership on update/delete
- low-stock detection
- low-stock report email/PDF behavior through integration flow

Example tests:

- `ResourceStockServiceTests.CreateResourceAsync_CreatesShelterResource`
- `ResourceStockServiceTests.CreateResourceAsync_BlocksNonPositiveQuantity`
- `ResourceStockServiceTests.CreateResourceAsync_RequiresCategory`
- `ResourceStockServiceTests.CreateResourceAsync_BlocksDuplicateShelterResource`
- `ResourceStockServiceTests.CreateResourceAsync_RequiresFoodTypeForFoodResources`
- `ResourceStockServiceTests.UpdateResourceAsync_BlocksAnotherSheltersResource`
- `ResourceStockServiceTests.GetLowStockResourcesForShelterAsync_ReturnsQuantityAtOrBelowThreshold`
- `ResourceStockServiceTests.UpdateResourceAsync_ClearsFoodTypeForNonFoodCategory`
- `ServiceFlowIntegrationTests.ResourceStockFlow_EnforcesOwnershipLowStockAndNotifications`

Why it matters:

Shelter resources are operational data. The tests verify that shelters manage only their own stock and that low-stock warnings are reliable.

Presentation explanation:

> Resource tests protect inventory validation and shelter scoping, which are important because each shelter has its own stock.

### 4.9 Shelter Registration Requests

What is tested:

- public shelter application submission
- pending request creation
- admin email/PDF attachment on submission
- duplicate pending email blocked
- existing shelter account email blocked
- invalid coordinates rejected
- admin accepts application
- accepting creates `ApplicationUser`, Shelter role, and `Shelter` profile
- rejecting does not create user/shelter
- non-admin users cannot accept

Example tests:

- `ShelterRegistrationRequestServiceTests.SubmitRequestAsync_CreatesPendingRequestAndSendsAdminEmail`
- `ShelterRegistrationRequestServiceTests.SubmitRequestAsync_BlocksDuplicatePendingRequestForSameEmail`
- `ShelterRegistrationRequestServiceTests.SubmitRequestAsync_BlocksExistingShelterAccountEmail`
- `ShelterRegistrationRequestServiceTests.SubmitRequestAsync_RejectsInvalidCoordinates`
- `ShelterRegistrationRequestServiceTests.AcceptRequestAsync_CreatesShelterUserRoleAndLinkedShelterWithCoordinates`
- `ShelterRegistrationRequestServiceTests.AcceptRequestAsync_BlocksNonAdminUser`
- `ShelterRegistrationRequestServiceTests.RejectRequestAsync_DoesNotCreateUserOrShelter`
- `CsvImportServiceTests.ImportedShelterRequest_CanBeAcceptedThroughExistingApprovalFlow`

Presentation explanation:

> Shelter registration tests show that a public application can become a real shelter account only through admin approval.

### 4.10 Email and PDF Flows

What is tested:

- fake email service captures outgoing email data
- fake PDF service provides report bytes
- adoption request email includes PDF attachment
- visit confirmation email includes `.ics` calendar attachment
- visit reminders include calendar invite data
- low-stock flow sends report email/PDF
- email failure prevents marking reminder as sent
- export PDF generation returns PDF bytes

Example tests:

- `AdoptionRequestServiceTests.ConfirmVisitAsync_SendsCalendarInviteAttachment`
- `AdoptionRequestServiceTests.ConfirmVisitAsync_UsesBucharestLocalTimeInCalendarInvite`
- `VisitReminderServiceTests.SendDueVisitRemindersAsync_SendsEmailWithCalendarAttachmentAndMarksSent`
- `VisitReminderServiceTests.SendVisitReminderAsync_DoesNotMarkSentWhenEmailThrows`
- `ServiceFlowIntegrationTests.AdoptionRequestFlow_SubmitViewAcceptAndNotifyWithPdfAttachment`
- `ServiceFlowIntegrationTests.ResourceStockFlow_EnforcesOwnershipLowStockAndNotifications`
- `ExportServiceTests.GenerateAdoptionRequestsPdfAsync_ReturnsPdfBytes`

Important note:

- Tests use `TestEmailService` and `TestPdfReportService`, not real SMTP and not full production PDF rendering.
- The tests verify that the workflow requests emails/PDFs/attachments correctly.

Presentation explanation:

> I do not send real emails in tests. I use fake services that capture what would be sent, including PDF and calendar attachments.

### 4.11 Notifications

What is tested:

- notification records are created
- notification starts unread
- unread count works
- newest-first ordering
- category and unread filtering
- only the owner can mark a notification as read
- duplicate suppression can prevent repeated notifications

Example tests:

- `NotificationServiceTests.CreateNotificationAsync_CreatesUnreadNotificationForUser`
- `NotificationServiceTests.MarkAsReadAsync_OnlyMarksNotificationsOwnedByUser`
- `NotificationServiceTests.GetNotificationsForUserAsync_ReturnsOwnNotificationsNewestFirst`
- `NotificationServiceTests.GetNotificationsForUserAsync_FiltersByCategoryAndUnreadStatus`
- `NotificationServiceTests.CreateNotificationAsync_CanSuppressRecentDuplicate`

Presentation explanation:

> Notification tests verify that notifications are private to the user and support unread counts, filtering, ordering, and duplicate suppression.

### 4.12 Report History

What is tested:

- report/export metadata is recorded
- report history stores metadata only
- generated PDF/CSV content bytes are not stored in the database
- shelter users only see their own report history
- admin can filter failed reports
- CSV export generation creates report history

Example tests:

- `ReportHistoryServiceTests.RecordReportSentAsync_CreatesMetadataOnlyRecord`
- `ReportHistoryServiceTests.GetReportHistoryForShelterAsync_ReturnsOnlyCurrentShelterRecords`
- `ReportHistoryServiceTests.GetAdminReportHistoryAsync_CanFilterFailures`
- `ReportHistoryServiceTests.ExportGeneration_CreatesReportHistoryRecord`

Presentation explanation:

> Report history stores traceability metadata, not the generated file bytes. This keeps the database smaller and records what was generated or sent.

### 4.13 Audit Logs

What is tested:

- recent logs are ordered newest first
- dog creation writes an audit log
- visit confirmation writes an audit log
- resource update writes an audit log
- sensitive fields such as `PasswordHash` and `SecurityStamp` are not included in dog audit descriptions

Example tests:

- `AuditLogServiceTests.GetRecentLogsAsync_ReturnsNewestFirst`
- `AuditLogServiceTests.CreateDogAsync_WritesAuditLog`
- `AuditLogServiceTests.ConfirmVisitAsync_WritesAuditLog`
- `AuditLogServiceTests.UpdateResourceAsync_WritesAuditLog`

Presentation explanation:

> Audit logs help trace important platform actions, and tests verify that important actions create logs without exposing sensitive identity fields.

### 4.14 CSV Import/Export

What is tested:

- resource CSV preview parses valid rows
- invalid quantity fails validation
- resource import affects only the current shelter
- dog import creates dogs and images
- dog import maps known breed text to `DogBreed`
- dog import handles primary and secondary mixed breed parsing
- invalid image URL in dog CSV is rejected
- admin shelter request CSV creates pending requests and notifications
- imported shelter request can later be accepted through normal approval workflow
- exports omit sensitive Identity fields
- shelter exports are scoped to the current shelter
- adoption request and resource exports contain expected operational data

Example tests:

- `CsvImportServiceTests.PreviewShelterResourcesImportAsync_ValidResourcesCsvParsesSuccessfully`
- `CsvImportServiceTests.PreviewShelterResourcesImportAsync_InvalidQuantityFailsValidation`
- `CsvImportServiceTests.ImportShelterResourcesAsync_AffectsOnlyCurrentShelter`
- `CsvImportServiceTests.ImportShelterDogsAsync_ValidDogCsvCreatesDogAndImages`
- `CsvImportServiceTests.ImportShelterDogsAsync_KnownMixedBreedPairStoresPrimaryAndSecondaryBreeds`
- `CsvImportServiceTests.PreviewShelterDogsImportAsync_InvalidImageUrlFailsValidation`
- `CsvImportServiceTests.ImportAdminShelterRequestsAsync_ValidCsvCreatesPendingRequestAndNotification`
- `ExportServiceTests.GenerateUsersCsvAsync_ContainsUserData_AndExcludesSensitiveIdentityFields`
- `ExportServiceTests.GenerateShelterDogsCsvAsync_ContainsOnlyCurrentShelterDogs`
- `ExportServiceTests.GenerateShelterResourcesCsvAsync_ContainsOnlyCurrentShelterResourcesAndLowStockStatus`

Presentation explanation:

> CSV tests verify both data validation and ownership scoping. This is important because import/export can affect many records at once.

### 4.15 Recommended Dogs

What is tested:

- adopted and in-treatment dogs are excluded
- apartment profiles favor smaller or medium dogs
- missing adopter profile returns no recommendations
- OpenAI disabled uses rule-based results
- OpenAI cannot add unknown dog IDs
- OpenAI cannot add unavailable dogs
- OpenAI recommendation request does not include private adopter data

Example tests:

- `DogRecommendationServiceTests.RuleBasedRecommendations_ExcludeAdoptedAndInTreatmentDogs`
- `DogRecommendationServiceTests.RuleBasedRecommendations_ApartmentProfileFavorsSmallOrMediumDogs`
- `DogRecommendationServiceTests.RuleBasedRecommendations_MissingAdopterProfileReturnsEmptyResult`
- `DogRecommendationServiceTests.Recommendations_OpenAiDisabledUsesRuleBasedResults`
- `DogRecommendationServiceTests.Recommendations_IgnoreUnknownOpenAiDogIdsAndCannotAddUnavailableDogs`
- `DogRecommendationServiceTests.Recommendations_OpenAiRequestDoesNotIncludeSensitiveAdopterFields`

Simple explanation:

> Recommendations are tested both for the normal rule-based scoring logic and for safe behavior when OpenAI is disabled or returns invalid data.

Presentation explanation:

> The backend decides which dogs are candidates. OpenAI may help improve ordering or wording, but tests ensure it cannot inject unknown or unavailable dogs.

### 4.16 Adoption Copilot / AI Search

What is tested:

- Copilot tools search only public-safe dogs
- adopted and in-treatment dogs are excluded
- explicit size and neighborhood constraints are respected
- OpenAI disabled/failing still allows fallback search
- unknown OpenAI dog IDs are ignored
- private adopter data is not sent to OpenAI
- coat-color filter queries return exact matches
- apartment plus longer-walk query does not reward short-walk-only dogs
- apartment support can outrank a space-only longer-walk match
- cat query does not show unrelated apartment tags
- sick/recovering dog query maps to sensitive-dog compatibility
- "Ask shelter..." uncertainty prevents overconfident strong matching
- sensitive-dog tool output includes direct, indirect, generic, and missing evidence strength

Example tests:

- `SemanticDogSearchServiceTests.AdoptionCopilot_IgnoresUnknownOpenAiDogIds`
- `SemanticDogSearchServiceTests.AdoptionCopilot_OpenAiRequestDoesNotIncludeSensitiveAdopterFields`
- `SemanticDogSearchServiceTests.AdoptionCopilot_BlackAndTanFilterReturnsExactCoatColorMatches`
- `SemanticDogSearchServiceTests.AdoptionCopilot_ApartmentLongerWalksDoesNotRewardShortWalkOnlyDogs`
- `SemanticDogSearchServiceTests.AdoptionCopilot_ApartmentSupportOutranksSpaceOnlyLongerWalkFit`
- `SemanticDogSearchServiceTests.AdoptionCopilot_CatQueryDoesNotShowUnrelatedApartmentTags`
- `SemanticDogSearchServiceTests.AdoptionCopilot_SickRecoveringHouseholdDogUsesSensitiveDogIntent`
- `SemanticDogSearchServiceTests.AdoptionCopilot_AskShelterPrimaryEvidenceCannotBeStrongEvenWithOpenAiScore`
- `SemanticDogSearchServiceTests.AdoptionCopilotToolSearch_ReturnsOnlyPublicSafeDogs`
- `SemanticDogSearchServiceTests.AdoptionCopilotToolSearch_FiltersByMediumSizeAndNeighborhood`
- `SemanticDogSearchServiceTests.AdoptionCopilotToolSearch_EmitsStructuredSensitiveDogEvidenceStrengths`

Important explanation:

> The AI does not directly access the database. PawConnect services provide safe tool outputs and public-safe dog candidates. Final dog IDs are validated by the backend.

Presentation explanation:

> Copilot tests focus on safety and explainability: no private data, no invented dogs, public-safe filtering, and evidence-based tags for compatibility queries.

### 4.17 Semantic Search / Embeddings

Semantic search and embeddings are implemented and tested.

What is tested:

- dog search document generation includes public dog/shelter data
- sensitive shelter contact fields are excluded
- embeddings are created only for public-safe dogs
- missing OpenAI API key causes a safe failure
- semantic search falls back when OpenAI is disabled
- semantic search uses embeddings when available

Example tests:

- `SemanticDogSearchServiceTests.DogSearchDocument_IncludesPublicFieldsAndExcludesSensitiveShelterContactFields`
- `SemanticDogSearchServiceTests.RebuildIndex_CreatesEmbeddingsForPublicSafeDogsOnly`
- `SemanticDogSearchServiceTests.RebuildIndex_MissingApiKeyReturnsSafeFailureWithoutCallingOpenAi`
- `SemanticDogSearchServiceTests.SemanticSearch_FallsBackWhenOpenAiIsDisabled`
- `SemanticDogSearchServiceTests.SemanticSearch_UsesEmbeddingsWhenAvailable`

Note:

- The current trimmed suite no longer has a dedicated stale-embedding removal test or cosine-similarity helper test.
- The suite still covers the main semantic-search safety story: public-safe indexing, no private fields, configured failure, disabled fallback, and embedding use.

Presentation explanation:

> Embedding tests verify that semantic search is optional and safe. If OpenAI is unavailable, the application falls back instead of breaking.

### 4.18 Integration-Style Service Flows

Integration-style tests are in:

- `PawConnect.Tests/Tests/Integration/ServiceFlowIntegrationTests.cs`

Tested flows:

- `PublicDogFlow_ReturnsOnlyPublicSafeDogsForListingAndFeaturedPreview`
- `FavoritesAndDogDeletionFlow_KeepsFavoritesPrivateAndDeletesFavoriteOnlyDog`
- `AdoptionRequestFlow_SubmitViewAcceptAndNotifyWithPdfAttachment`
- `AdoptionRequestFlow_RejectAndCancelRespectOwnershipAndPendingRules`
- `DogCreateImageAndAgeFlow_SavesImagesAndFormatsAge`
- `ResourceStockFlow_EnforcesOwnershipLowStockAndNotifications`

Why they matter:

These tests combine multiple services/entities and verify realistic workflows:

- public dog listing
- favorites and dog deletion
- adoption request submission, ownership, visit confirmation, email/PDF/calendar attachment
- dog creation, image saving, age formatting
- resource stock update, low-stock detection, notification/email/PDF behavior

Presentation explanation:

> Integration-style service tests are the closest automated tests to real user flows. They do not open the browser, but they execute the backend workflow with real services and database entities.

## 5. What Is Not Tested

The current suite is honest and practical, but it does not test everything.

Not tested or only lightly tested:

- no full browser end-to-end tests
- limited visual/UI layout testing
- no real SMTP integration in automated tests
- no live OpenAI API calls in normal test runs
- no production SQL Server dependency in tests
- recently viewed dogs are not directly covered by a dedicated test class in the trimmed suite
- exact Copilot score micro-calibration is intentionally not exhaustively tested
- full PDF visual rendering is not deeply tested

Why this is acceptable:

- Service-level tests are faster and more stable.
- External services are replaced with fakes for reliability.
- The most important business risks are in backend rules, not visual layout.
- Browser UI tests would be a useful future improvement, but they are not required to prove core service correctness.

Presentation explanation:

> I focused on service-level testing because the most important correctness rules are in the services. Full UI automation and live external integrations are possible future improvements.

## 6. How Tests Are Run

Run all tests with:

```bash
dotnet test
```

Current result after cleanup:

```text
Passed: 146, Failed: 0, Skipped: 0
```

The tests:

- create isolated EF Core InMemory databases
- do not require a running web server
- do not require SQL Server
- do not require a real SMTP server
- do not require real OpenAI API keys
- use fake/test services where external dependencies would be unreliable

## 7. How to Explain Testing to the Committee

### Question: Why do you have many tests?

Answer:

> The application contains many business workflows and role-based rules. I tested the important service-level behavior: adoption request transitions, shelter ownership checks, public-safe dog visibility, CSV validation, notifications, reports, and AI fallback/safety. The number comes from covering edge cases, not from testing random details.

### Question: Are these unit tests or integration tests?

Answer:

> Most tests are service-level unit or integration-style tests. They use EF Core InMemory and fake services, so they test realistic business flows without requiring a browser, SQL Server, SMTP server, or OpenAI API.

### Question: Why not UI/browser tests?

Answer:

> I focused on service-level tests because the critical rules are enforced in services. Browser end-to-end tests would be a useful future improvement, but service tests are faster, more stable, and cover the core logic.

### Question: How do you test email/PDF?

Answer:

> I use fake email and PDF services. The tests verify that the workflow creates the expected emails and attachments, including PDF reports and `.ics` calendar invites, without sending real emails.

### Question: How do you test OpenAI/Copilot?

Answer:

> The tests use fake OpenAI clients. They verify fallback behavior, public-safe filtering, sanitized DTOs, and backend validation. Normal test runs do not rely on live OpenAI calls.

### Question: How did you write so many tests?

Answer:

> I wrote them incrementally while implementing each feature. I reused test helpers for users, shelters, dogs, fake email, fake PDFs, and in-memory database setup, so adding tests for new rules became faster.

If asked about AI-assisted development:

> I used AI tools to help with test skeletons and edge-case ideas, but I reviewed and adapted them to the actual PawConnect rules and code.

## 8. Testing Cheat Sheet

### Main tested workflows

- public dog visibility
- dog creation, deletion, search filters, status history
- dog breed formatting and mixed-breed parsing
- dog image URL validation and fallback selection
- adoption request lifecycle
- visit confirmation and final adoption
- shelter ownership and adopter ownership
- favorites
- resource low-stock workflow
- shelter registration approval
- email/PDF/calendar invite flow
- notifications
- report history
- audit logs
- CSV import/export
- recommended dogs
- Adoption Copilot and semantic search safety

### Most important test classes

| Test class | What it covers |
| ---------- | -------------- |
| `AdoptionRequestServiceTests` | Adoption request validation, ownership, visit confirmation, cancellation, final adoption, calendar invite behavior. |
| `ServiceFlowIntegrationTests` | Realistic backend flows across multiple services: public visibility, favorites/deletion, adoption request, dog image/age, resource low-stock. |
| `SemanticDogSearchServiceTests` | Adoption Copilot, OpenAI safety, semantic search, embeddings, public-safe AI data, evidence tags. |
| `DogRecommendationServiceTests` | Rule-based recommendations, OpenAI fallback, unknown/unavailable dog ID protection, private data exclusion. |
| `DogServiceTests` | Dog deletion rules, public visibility, search filters, age validation, custom breed, status history. |
| `ResourceStockServiceTests` | Resource validation, duplicate prevention, food type rules, ownership, low-stock logic. |
| `CsvImportServiceTests` | CSV validation/import for resources, dogs, dog images, breed parsing, shelter requests. |
| `ExportServiceTests` | CSV/PDF export basics, shelter scoping, sensitive Identity field exclusion. |
| `ShelterRegistrationRequestServiceTests` | Shelter application submission, duplicate checks, admin approval/rejection, user/shelter creation. |
| `NotificationServiceTests` | Notification privacy, unread count, filtering, ordering, duplicate suppression. |
| `AuditLogServiceTests` | Audit log creation for important actions and safe log content. |
| `VisitReminderServiceTests` | Visit reminder eligibility, email/calendar attachment, notification/audit logging, failure behavior. |

### Key sentences for presentation

1. The tests focus on business rules rather than visual details.
2. The most important tested flow is the adoption request lifecycle, from submission to visit confirmation and final adoption.
3. Public-safe dog visibility is tested in browsing, recommendations, semantic search, and Copilot.
4. Ownership rules are tested at the service layer, so UI hiding is not the only protection.
5. Email and PDF behavior is tested with fake services, so tests are reliable and do not send real emails.
6. The Copilot is tested for safety: it cannot expose private data or return dogs outside backend-provided candidates.
7. EF Core InMemory makes the tests fast and isolated, without requiring SQL Server.
8. Integration-style service tests verify realistic backend workflows without needing a browser.

