# Architecture and Code Map

## Architecture Summary

PawConnect is an ASP.NET Core Blazor Server application. The UI is built from Razor components. Those components call C# services through dependency injection. Services use Entity Framework Core through `ApplicationDbContext` to access SQL Server.

The important architectural idea is separation of responsibilities:

- Razor components handle UI state and user interaction.
- Services handle business rules and workflows.
- Entities represent database tables.
- EF Core maps entities to SQL Server.
- External integrations are wrapped in services.
- Tests verify services and domain behavior with EF Core InMemory and test doubles.

## Main Projects and Folders

| Path | Purpose | Important files/classes | Why it matters |
| --- | --- | --- | --- |
| `PawConnect.csproj` | Main web application project. | Package references, target framework. | Shows the stack: ASP.NET Core, EF Core, Identity, MudBlazor, MailKit, QuestPDF, Quartz. |
| `Program.cs` | Application startup and dependency injection. | `AddRazorComponents`, `AddDbContext`, Identity, service registrations, OpenAI clients, Quartz jobs. | This is where the app is assembled. |
| `Data/` | EF Core context, user model, seed data. | `ApplicationDbContext.cs`, `ApplicationUser.cs`, `IdentitySeedData.cs`, `DogBreedSeedData.cs`. | Defines database access and demo/lookup data. |
| `Entities/` | Domain/database entities and enums. | `Dog.cs`, `Shelter.cs`, `AdoptionRequest.cs`, `DogBreed.cs`, `DogSearchEmbedding.cs`, `ResourceStock.cs`. | These are the main tables and relationships. |
| `Services/` | Business logic and external integrations. | `DogService.cs`, `AdoptionRequestService.cs`, `AdoptionCopilotService.cs`, `DogRecommendationService.cs`, `SemanticDogSearchService.cs`. | Most important logic is here, not in Razor pages. |
| `Components/Pages/` | Routed Blazor pages. | `Dogs.razor`, `DogDetails.razor`, `Adopter/*`, `Shelter/*`, `Admin/*`. | User-facing and role-specific screens. |
| `Components/Shared/` | Shared dialogs/components/helpers. | `AdoptionRequestDetailsDialog.razor`, `DogStatusHistoryDialog.razor`, `DogImagePreviewDialog.razor`, `DogCardImage.razor`. | Reusable UI pieces. |
| `Components/Layout/` | Layout and navigation. | `MainLayout.razor`, `NavMenu.razor`. | Defines shell and role-based navigation. |
| `Components/Account/` | Identity UI pages. | Login/register/account pages. | Handles user authentication screens. |
| `Jobs/` | Quartz scheduled jobs. | `ShelterSummaryReportJob.cs`, `VisitReminderJob.cs`. | Background automation for reports/reminders. |
| `Repositories/` | Generic EF repository abstraction. | `IGenericRepository.cs`, `GenericRepository.cs`. | Generic data access support, although most complex logic is in services. |
| `Migrations/` | EF Core migrations. | Timestamped migration files. | Shows schema evolution: dog breeds, embeddings, coat color, etc. |
| `wwwroot/` | Static assets and CSS. | `app.css`, images/scripts. | Styling and client static files. |
| `PawConnect.Tests/` | xUnit test project. | `Tests/*.cs`, `Tests/Helpers/*`. | 281 automated tests for service/domain behavior. |
| `docs/` | Existing project documentation. | `PawConnect_Technical_Context.md`, diagrams, study guide. | Useful thesis/demo documentation. |

## How Frontend Communicates With Backend

PawConnect does not mainly use public REST controllers for its internal app flows. It uses Blazor Server:

1. User interacts with a Razor component.
2. The component calls an injected service.
3. The service uses EF Core and other services.
4. The component updates its state and re-renders.

Example:

| User action | UI file | Service | Database |
| --- | --- | --- | --- |
| Open dog listing | `Components/Pages/Dogs.razor` | `Services/DogService.cs` | `Dogs`, `Shelters`, `DogImages`, `DogBreeds` |
| Submit adoption request | `Components/Pages/DogDetails.razor` | `Services/AdoptionRequestService.cs` | `AdoptionRequests`, `Notifications`, `DogStatusHistories` |
| Use Copilot | `Components/Pages/Adopter/AdoptionCopilot.razor` | `Services/AdoptionCopilotService.cs` | `Dogs`, `DogSearchEmbeddings`, favorites/recent views if tools request them |
| Manage resources | `Components/Pages/Shelter/Resources.razor` | `Services/ResourceStockService.cs` | `ResourceStocks`, `ResourceCategories`, `FoodTypes` |

## Dependency Injection

Dependency injection is configured in `Program.cs`.

Important registrations:

- `ApplicationDbContext` with SQL Server.
- ASP.NET Core Identity with `ApplicationUser` and `IdentityRole`.
- MudBlazor services.
- Domain services such as `DogService`, `AdoptionRequestService`, `ShelterService`, `ResourceStockService`.
- AI/search services such as `AdoptionCopilotService`, `AdoptionCopilotToolService`, `SemanticDogSearchService`, `DogSearchEmbeddingService`, `OpenAiAdoptionCopilotClient`, `OpenAiEmbeddingService`.
- Email/PDF/report services such as `SmtpEmailService`, `PdfReportService`, `ReportHistoryService`.
- Quartz jobs for scheduled reports and visit reminders.

## Authentication and Authorization Configuration

Authentication is configured in `Program.cs` using ASP.NET Core Identity:

- `AddAuthentication(...).AddIdentityCookies()`
- `AddIdentityCore<ApplicationUser>()`
- `AddRoles<IdentityRole>()`
- `AddEntityFrameworkStores<ApplicationDbContext>()`

Roles are seeded in `Data/IdentitySeedData.cs`.

Pages are protected with attributes such as:

- `@attribute [Authorize(Roles = "Adopter")]`
- `@attribute [Authorize(Roles = "Shelter")]`
- `@attribute [Authorize(Roles = "Admin")]`

Examples:

| Page | Authorization |
| --- | --- |
| `Components/Pages/Adopter/AdoptionCopilot.razor` | Adopter only |
| `Components/Pages/Shelter/ManageDogs.razor` | Shelter only |
| `Components/Pages/Admin/AdminDogs.razor` | Admin only |

## Validation Locations

Validation appears at multiple levels:

| Level | Examples |
| --- | --- |
| Entity attributes | `Entities/ResourceStock.cs`, `Entities/DogImage.cs`, `Entities/AdopterProfile.cs` |
| UI validation | MudBlazor form fields in `CreateDog.razor`, `EditDog.razor`, `Resources.razor`, `DogDetails.razor` |
| Service validation | `DogService.ValidateAndNormalizeDogAsync`, `AdoptionRequestService.ValidateQuestionnaire`, `ResourceStockService.PrepareResourceAsync` |
| Database constraints | Unique indexes and relationships in `ApplicationDbContext.cs` |
| AI output validation | `AdoptionCopilotService.BuildAiResult`, `DogRecommendationService.GetOpenAiEnhancedRecommendationsAsync` |

## Most Important Business Logic

| Business area | Main files |
| --- | --- |
| Dog visibility and search | `Services/DogService.cs`, `Services/SemanticDogSearchService.cs` |
| Dog creation/edit/status history | `Services/DogService.cs`, `Components/Pages/Shelter/CreateDog.razor`, `EditDog.razor` |
| Adoption lifecycle | `Services/AdoptionRequestService.cs`, `Components/Pages/Shelter/ShelterAdoptionRequests.razor`, `Components/Pages/Adopter/MyAdoptionRequests.razor` |
| Recommendations | `Services/DogRecommendationService.cs`, `Services/OpenAiRecommendationClient.cs` |
| Adoption Copilot | `Services/AdoptionCopilotService.cs`, `Services/AdoptionCopilotToolService.cs`, `Services/OpenAiAdoptionCopilotClient.cs` |
| Search embeddings | `Services/DogSearchDocumentService.cs`, `Services/DogSearchEmbeddingService.cs`, `Services/OpenAiEmbeddingService.cs`, `Services/SemanticDogSearchService.cs` |
| Shelter resources | `Services/ResourceStockService.cs`, `Components/Pages/Shelter/Resources.razor` |
| Reports/emails | `Services/PdfReportService.cs`, `Services/SmtpEmailService.cs`, `Services/ReportHistoryService.cs` |
| Notifications/audit logs | `Services/NotificationService.cs`, `Services/AuditLogService.cs` |

## How to Explain the Architecture to the Committee

You can say:

"PawConnect is a Blazor Server application, so the UI components and backend services run in the same ASP.NET Core application. The Razor pages are responsible for interaction and rendering, while the services contain the business logic. EF Core maps the entities to SQL Server. Identity handles login and roles. For AI features, OpenAI is isolated behind service classes, and the backend remains the source of truth. This means the AI can help interpret queries, but real data access, filtering, validation, and final dog selection are controlled by PawConnect services."

## Important Architectural Strengths

- Clear role-based page separation.
- Business logic is mostly in services, not directly in UI.
- EF Core relationships and indexes enforce important constraints.
- AI integrations are optional and have deterministic fallbacks.
- External effects like email/PDF/report history are isolated and non-blocking.
- Tests exercise the service layer heavily.

## Architectural Limitations

- The app is Blazor Server, so it depends on a persistent server connection.
- Most flows are not exposed as public REST APIs, which is fine for this architecture but different from API-first systems.
- UI/component testing is limited compared with service testing.
- External image URLs and OpenAI depend on third-party availability.
