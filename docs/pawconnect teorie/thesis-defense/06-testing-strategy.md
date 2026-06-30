# Testing Strategy

## Test Suite Summary

The repository contains one test project:

- `PawConnect.Tests/PawConnect.Tests.csproj`

The test framework is:

- xUnit
- EF Core InMemory
- Microsoft.NET.Test.Sdk

Static count by `[Fact]` and `[Theory]` attributes:

- Total: 281 tests

This number is high because PawConnect has many combinations of:

- user roles
- dog statuses
- adoption request states
- validation rules
- public-safe filters
- AI enabled/disabled behavior
- fallback behavior
- CSV import row validation
- resource thresholds
- dog breed formatting
- image URL validation

## How to Run Tests

```powershell
dotnet test
```

## Test Infrastructure

| File | Purpose |
| --- | --- |
| `PawConnect.Tests/Tests/Helpers/TestDbContextFactory.cs` | Creates EF Core InMemory contexts, seeds Identity users/roles/lookups, creates test users and shelters. |
| `PawConnect.Tests/Tests/Helpers/TestDoubles.cs` | Provides fake email and PDF services. |

The tests mostly exercise the service layer directly. This is appropriate because PawConnect's important business rules are in services such as `DogService`, `AdoptionRequestService`, `AdoptionCopilotService`, and `ResourceStockService`.

## Test File Summary

| Test file | Count | System under test | What is verified | Why it matters |
| --- | ---: | --- | --- | --- |
| `AdoptionRequestServiceTests.cs` | 19 | `AdoptionRequestService` | Request creation, duplicate blocking, visit confirmation, adoption finalization, rejection, cancellation, email/PDF side effects. | Adoption workflow is core business logic. |
| `AuditLogServiceTests.cs` | 4 | `AuditLogService` | Audit logging and query behavior. | Admin traceability. |
| `CopilotStateServiceTests.cs` | 4 | `CopilotStateService` | Saving/restoring Copilot session state. | UI continuity for Copilot. |
| `CsvImportServiceTests.cs` | 21 | `CsvImportService` | Resource/dog/shelter request import preview, validation errors, imports. | CSV import has many row/field edge cases. |
| `DistanceServiceTests.cs` | 3 | `DistanceService` | Distance calculations. | Nearby shelter/dog browsing support. |
| `DogBreedFormatterTests.cs` | 10 | `DogBreedFormatter` | Breed display and parsing for primary, mixed, secondary, custom, unknown. | Breed correctness affects UI/search/import/export. |
| `DogBreedInformationFormatterTests.cs` | 10 | `DogBreedInformationFormatter` | Breed notes, fallback notes, health note wording, mixed breed disclaimer. | Keeps dog details educational and safe. |
| `DogCoatColorOptionsTests.cs` | 2 | `DogCoatColorOptions` | Coat color normalization/options. | Supports coat color filters and Copilot. |
| `DogImageServiceTests.cs` | 9 | `DogImageService` | Image add/delete/main image rules and validation. | Prevents bad image data and gallery issues. |
| `DogImageUrlValidatorTests.cs` | 9 | `DogImageUrlValidator` | Valid/invalid URL detection, placeholder rejection. | Prevents broken demo/public images. |
| `DogRecommendationServiceTests.cs` | 15 | `DogRecommendationService` | Rule-based recommendation scoring, OpenAI enhancement, fallback, unknown dog ID validation. | Personalized recommendations must be safe. |
| `DogServiceTests.cs` | 12 | `DogService` | Public visibility, filtering, dog validation, status history, breed/coat handling. | Dog management is central. |
| `EmailMimeBuilderTests.cs` | 2 | `EmailMimeBuilder` | MIME/email construction. | Email correctness. |
| `ExportServiceTests.cs` | 11 | `ExportService` | CSV/PDF export content and formatting. | Reporting/export reliability. |
| `FavoriteDogServiceTests.cs` | 4 | `FavoriteDogService` | Favorite add/remove, duplicate prevention, user scoping. | Adopter personalization. |
| `Integration/ServiceFlowIntegrationTests.cs` | 6 | Multiple services together | End-to-end-style service workflows. | Shows services cooperate correctly. |
| `LocalReturnUrlHelperTests.cs` | 3 | `LocalReturnUrlHelper` | Allows local URLs, rejects external ones. | Prevents unsafe redirects. |
| `NominatimGeocodingServiceTests.cs` | 10 | `NominatimGeocodingService` | Address lookup/reverse geocoding response handling. | Shelter location/map features. |
| `NotificationServiceTests.cs` | 13 | `NotificationService` | Create/list/read/delete notification behavior. | User feedback and alerts. |
| `PawConnectIdentityEmailSenderTests.cs` | 3 | Identity email sender adapter | Identity email integration. | Account email flow support. |
| `PdfReportServiceTests.cs` | 3 | `PdfReportService` | PDF generation for key reports. | Thesis/demo reporting feature. |
| `ReportHistoryServiceTests.cs` | 8 | `ReportHistoryService` | Successful/failed report history recording and querying. | Traceability for reports. |
| `ResourceStockServiceTests.cs` | 9 | `ResourceStockService` | Resource CRUD, validation, low stock behavior. | Shelter operations. |
| `SemanticDogSearchServiceTests.cs` | 65 | Semantic search, embeddings, Copilot | Embedding refresh, fallback, public-safe filtering, Copilot intent/scoring/tagging, OpenAI failure/unknown IDs. | Most important AI/search coverage. |
| `ShelterRegistrationRequestServiceTests.cs` | 16 | `ShelterRegistrationRequestService` | Application submit/accept/reject, duplicate checks, account/shelter creation. | Shelter onboarding. |
| `ShelterSummaryReportServiceTests.cs` | 4 | `ShelterSummaryReportService` | Manual/scheduled report sending. | Scheduled reporting. |
| `VisitReminderServiceTests.cs` | 6 | `VisitReminderService` | Due reminder selection and sending. | Visit follow-up workflow. |

## Most Heavily Tested Area

`SemanticDogSearchServiceTests.cs` has 65 tests. This is expected because it covers several complex areas:

- embedding refresh and content hashes
- OpenAI disabled/failure fallback
- semantic search fallback
- public-safe search indexing
- Adoption Copilot deterministic interpretation
- Copilot scoring and labels
- coat color filters
- short-walk vs longer-walk logic
- unknown OpenAI dog ID validation
- evidence-backed tags and chip cleanup

This is a strong point for the thesis defense because AI features are risky and need many edge-case tests.

## How to Justify 200+ Tests to the Committee

Say this:

"The test count is high because the application is not only CRUD. It has workflow rules, role restrictions, state transitions, AI fallback behavior, imports, exports, reports, notifications, and filtering combinations. Many tests are small and focused. They check one rule at a time, which is better than a few large fragile tests."

Important argument:

- The number is not for show.
- Each test protects a business rule or edge case.
- Adoption systems have many status transitions.
- AI features need tests for disabled API, invalid output, hallucinated IDs, and fallback.
- CSV import needs tests for invalid rows and different columns.
- Role/ownership checks need dedicated tests.

## Most Impressive Tests to Mention

| Test area | Why mention it |
| --- | --- |
| OpenAI unknown dog ID ignored | Demonstrates AI safety and backend validation. |
| OpenAI disabled/failing fallback | Demonstrates Copilot works without external API. |
| Semantic index excludes unavailable dogs | Demonstrates public-safe search. |
| Adoption request lifecycle tests | Demonstrates workflow correctness. |
| Dog status history tests | Demonstrates traceability. |
| CSV import validation tests | Demonstrates robust data import. |
| Resource low-stock tests | Demonstrates operational shelter logic. |
| Local return URL tests | Demonstrates security awareness. |
| Dog image URL tests | Demonstrates data hygiene and UI reliability. |
| Breed formatter tests | Demonstrates careful handling of real-world messy breed data. |

## Potential Weaknesses in the Test Suite

Be honest:

- The tests are mostly service/domain tests, not browser end-to-end UI tests.
- Blazor component rendering is not heavily tested.
- Visual layout changes are mostly manually verified.
- OpenAI behavior is tested with fake clients, not live API calls, which is correct for deterministic tests but not full integration.
- SQL Server-specific behavior is mostly covered through EF Core design/migrations and not every test uses real SQL Server.

## Testing-Related Committee Questions

| Question | Suggested answer |
| --- | --- |
| Why so many tests? | The app has many workflows and combinations: roles, statuses, AI fallback, filters, imports, reports. Tests are focused on individual rules. |
| Are these unit or integration tests? | Mostly service/domain tests using EF Core InMemory, plus integration-style service flow tests. |
| Do you test the UI? | The current suite focuses more on service logic than full browser UI. UI was manually verified during development. |
| How do you test OpenAI without calling the API? | Fake clients simulate OpenAI responses, including failures and invalid IDs. |
| What is the most important AI test? | Unknown OpenAI dog IDs are ignored and fallback works when OpenAI is disabled or fails. |

