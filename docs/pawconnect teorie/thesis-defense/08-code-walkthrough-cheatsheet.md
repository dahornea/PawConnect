# Code Walkthrough Cheat Sheet

Use this file when you need to quickly open code during the presentation.

| Topic | File/path to open | What to show | Short explanation to say out loud |
| --- | --- | --- | --- |
| App startup | `Program.cs` | Service registrations, Identity, DbContext, OpenAI clients, Quartz jobs. | "This is where PawConnect wires the database, authentication, UI library, business services, AI clients, and scheduled jobs." |
| Database model | `Data/ApplicationDbContext.cs` | DbSets, relationships, indexes. | "The context maps domain entities to SQL Server and defines constraints like unique favorites and pending adoption requests." |
| User model | `Data/ApplicationUser.cs` | `FullName` and navigation properties. | "The app extends Identity users with domain navigation data." |
| Roles/seed data | `Data/IdentitySeedData.cs` | Role constants and demo seed flow. | "Roles and demo accounts/data are seeded here." |
| Dog entity | `Entities/Dog.cs` | Breed, coat color, status, descriptions, relationships. | "Dog is the central domain entity, connected to shelter, images, records, requests, favorites, and search embeddings." |
| Dog breed system | `Entities/DogBreed.cs`, `Services/DogBreedFormatter.cs` | Lookup fields and formatting logic. | "Breed is database-backed but still supports mixed, secondary, custom, and unknown breeds." |
| Dog browsing | `Components/Pages/Dogs.razor` | Filter UI and `ApplyFiltersAsync`. | "Public browsing calls `DogService` and only shows public-safe dogs." |
| Dog search service | `Services/DogService.cs` | `GetAvailableDogsAsync`, `SearchDogsAsync`, `GetDogDetailsAsync`. | "This service applies public status filters and normal dog search filters." |
| Dog details | `Components/Pages/DogDetails.razor` | Gallery, breed info, adoption request form. | "This page shows the public profile and starts the adoption request flow." |
| Dog image UI | `Components/Shared/DogCardImage.razor`, `DogImagePreviewDialog.razor` | Placeholder and lightbox behavior. | "Images use real valid URLs first, then UI fallback placeholders." |
| Dog image validation | `Services/DogImageService.cs`, `Services/DogImageUrlValidator.cs` | URL validation and main image logic. | "Invalid or placeholder image URLs are not saved as real dog images." |
| Shelter dog management | `Components/Pages/Shelter/ManageDogs.razor` | Dog table, imports, actions. | "Shelters manage dogs scoped to their own shelter." |
| Create dog | `Components/Pages/Shelter/CreateDog.razor` | Breed autocomplete, image URL, submit. | "This creates a dog using service validation and shelter ownership." |
| Edit dog | `Components/Pages/Shelter/EditDog.razor` | Save, image/medical sections, status history. | "Editing goes through `DogService.UpdateDogAsync`, which validates ownership and status history." |
| Adoption request entity | `Entities/AdoptionRequest.cs` | Status, visit status, questionnaire fields. | "This stores the request plus visit scheduling and shelter notes." |
| Adoption request service | `Services/AdoptionRequestService.cs` | `CreateRequestAsync`, `ConfirmVisitAsync`, `MarkAsAdoptedAsync`, `CancelRequestAsync`. | "This is the adoption workflow state machine." |
| Adopter requests UI | `Components/Pages/Adopter/MyAdoptionRequests.razor` | Request cards, cancel behavior, status labels. | "Adopters can track and cancel their own pending requests." |
| Shelter requests UI | `Components/Pages/Shelter/ShelterAdoptionRequests.razor` | Confirm visit, reject, mark adopted. | "Shelters manage requests for dogs they own." |
| Admin adoption requests | `Components/Pages/Admin/AdminAdoptionRequests.razor` | Admin table/details dialog. | "Admins can inspect adoption request data platform-wide." |
| Recommendations UI | `Components/Pages/Adopter/Recommendations.razor` | Recommendation cards. | "The adopter sees personalized dog recommendations." |
| Recommendation service | `Services/DogRecommendationService.cs` | `GetRuleBasedRecommendationsAsync`, `GetOpenAiEnhancedRecommendationsAsync`. | "Recommendations start rule-based and optionally let OpenAI improve explanation/ranking from candidates." |
| Recommendation OpenAI client | `Services/OpenAiRecommendationClient.cs` | Prompt and JSON response parsing. | "OpenAI receives sanitized candidates and must return JSON with candidate dog IDs only." |
| Copilot UI | `Components/Pages/Adopter/AdoptionCopilot.razor` | Query input, chips, result cards. | "The adopter types natural language and sees evidence-backed suggestions." |
| Copilot orchestration | `Services/AdoptionCopilotService.cs` | `AskAsync`, tool execution, OpenAI validation. | "This service parses the query, calls backend tools, optionally uses OpenAI, and validates results." |
| Copilot tool service | `Services/AdoptionCopilotToolService.cs` | `SearchDogsAsync`, evidence extraction/scoring. | "This is where public-safe candidates are retrieved and scored." |
| Copilot OpenAI tool calling | `Services/OpenAiAdoptionCopilotClient.cs` | Tool definitions and prompt. | "The model can request predefined tools, but the app executes them safely." |
| Copilot DTOs | `Services/AdoptionCopilotModels.cs`, `AdoptionCopilotToolModels.cs` | Intent, evidence, candidate DTOs. | "These models separate user intent, dog evidence, and display tags." |
| Semantic search | `Services/SemanticDogSearchService.cs` | Embedding search and fallback. | "Semantic search compares meaning with embeddings and falls back when unavailable." |
| Search documents | `Services/DogSearchDocumentService.cs` | Public dog document building. | "Only public-safe dog text is used for search embeddings." |
| Embedding storage | `Entities/DogSearchEmbedding.cs`, `Services/DogSearchEmbeddingService.cs` | Content hash and embedding JSON. | "Embeddings are derived search data stored separately from dog records." |
| OpenAI settings | `Services/OpenAiSettings.cs`, `appsettings.json` | Enabled/API key/model fields. | "OpenAI is configurable and optional." |
| Resource system | `Components/Pages/Shelter/Resources.razor`, `Services/ResourceStockService.cs` | Validation and low-stock logic. | "Shelters manage resources with categories, quantities, thresholds, imports, and exports." |
| CSV import | `Services/CsvImportService.cs` | Preview and import methods. | "Imports validate rows before saving." |
| Export/PDF | `Services/ExportService.cs`, `Services/PdfReportService.cs` | CSV/PDF generation. | "The app can produce operational reports." |
| Email | `Services/SmtpEmailService.cs`, `Services/PawConnectEmailTemplate.cs` | SMTP send and template. | "Emails are generated with templates and optional attachments." |
| Notifications | `Services/NotificationService.cs`, `Entities/Notification.cs` | Create/read/delete. | "Notifications keep users informed inside the app." |
| Audit logs | `Services/AuditLogService.cs`, `Entities/AuditLog.cs` | Log creation/query. | "Audit logs support traceability for admin review." |
| Scheduled jobs | `Jobs/ShelterSummaryReportJob.cs`, `Jobs/VisitReminderJob.cs` | Quartz job execution. | "Background jobs automate reports and visit reminders." |
| Tests overview | `PawConnect.Tests/Tests` | Test files. | "There are 281 xUnit tests focused on service and workflow correctness." |
| Test infrastructure | `PawConnect.Tests/Tests/Helpers/TestDbContextFactory.cs` | InMemory context and seed data. | "Tests use isolated in-memory databases and seeded users/roles." |
| AI tests | `PawConnect.Tests/Tests/SemanticDogSearchServiceTests.cs` | Copilot/semantic tests. | "This file verifies AI fallback, embeddings, public-safe search, and Copilot scoring behavior." |

## If Asked to Explain Copilot Quickly

Open these files in order:

1. `Components/Pages/Adopter/AdoptionCopilot.razor`
2. `Services/AdoptionCopilotService.cs`
3. `Services/AdoptionCopilotToolService.cs`
4. `Services/OpenAiAdoptionCopilotClient.cs`
5. `Services/AdoptionCopilotToolModels.cs`
6. `PawConnect.Tests/Tests/SemanticDogSearchServiceTests.cs`

Short explanation:

"The UI sends a natural-language query to `AdoptionCopilotService`. The service parses deterministic criteria, retrieves public-safe candidates through `AdoptionCopilotToolService`, extracts evidence and scores dogs, optionally asks OpenAI to use safe tools, validates returned dog IDs, then displays real database-backed cards."

## If Asked to Explain Adoption Requests Quickly

Open:

1. `Entities/AdoptionRequest.cs`
2. `Services/AdoptionRequestService.cs`
3. `Components/Pages/DogDetails.razor`
4. `Components/Pages/Shelter/ShelterAdoptionRequests.razor`
5. `PawConnect.Tests/Tests/AdoptionRequestServiceTests.cs`

Short explanation:

"The adoption request service controls the state transitions: pending request, confirmed visit, reserved dog, adopted dog, rejection or cancellation. It also creates notifications, emails, PDFs, and status history."

