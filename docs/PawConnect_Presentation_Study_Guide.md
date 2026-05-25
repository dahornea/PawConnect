# PawConnect Presentation Study Guide

## 1. Application Overview

PawConnect is a web application for stray dog adoption and shelter management. It helps public visitors discover adoptable dogs, adopters manage adoption requests, shelters manage their dogs and resources, and administrators supervise the whole platform.

The main problem it solves is fragmentation: dog information, adoption requests, shelter resources, notifications, reports, and AI-assisted matching are all handled in one application instead of separate spreadsheets, phone calls, and manual records.

| Role | What the role can do | What I should say in presentation |
|------|----------------------|-----------------------------------|
| Public visitor | Browse dogs, filter dogs, view dog details, browse shelters, view success stories, apply as a shelter, register or log in. | "Public users can inspect public-safe dog information without needing an account." |
| Adopter | Complete an adopter profile, save favorite dogs, view recently viewed dogs, receive recommendations, use the Adoption Copilot, submit and track adoption requests, receive notifications. | "The adopter experience is personalized through profile data, favorites, recommendations, and the AI Copilot." |
| Shelter representative | Manage shelter dogs, images, medical records, resource stock, adoption requests, visits, reports, and notifications. | "Shelters own their dogs and request workflow, so they can update operational data without admin intervention." |
| Administrator | Manage users, shelters, dogs, shelter applications, adoption requests, reports, activity logs, and rebuild the semantic dog search index. | "Admins have platform-wide visibility and maintenance tools." |

The important idea is that PawConnect is not only a dog listing website. It is a role-based adoption management system with a recommendation engine and an optional OpenAI-powered Adoption Copilot.

## 2. Technology Stack

| Technology | Why it is used | Relevant files |
|------------|----------------|----------------|
| C# | Main programming language for backend logic, services, entities, and Blazor components. | `Program.cs`, `Services/*.cs`, `Entities/*.cs` |
| ASP.NET Core | Hosts the web application, dependency injection, authentication, routing, middleware, and Razor components. | `Program.cs`, `Components/App.razor`, `Components/Routes.razor` |
| Blazor Server | Builds interactive UI with C# components instead of a separate JavaScript frontend. | `Components/Pages/*.razor`, `Components/Layout/*.razor` |
| Entity Framework Core | Maps C# entities to SQL Server tables and handles migrations. | `Data/ApplicationDbContext.cs`, `Data/Migrations/*` |
| SQL Server | Main relational database for users, shelters, dogs, requests, embeddings, logs, and reports. | `appsettings.json`, `Data/ApplicationDbContext.cs` |
| ASP.NET Core Identity | Handles users, roles, sign-in, password management, and authorization. | `Data/ApplicationUser.cs`, `Data/IdentitySeedData.cs`, `Components/Account/*` |
| MudBlazor | UI component library for cards, tables, dialogs, chips, forms, snackbars, and layout. | `Program.cs`, Razor pages in `Components/Pages` |
| MailKit / MimeKit | Sends SMTP emails with HTML content and attachments. | `Services/SmtpEmailService.cs`, `Services/EmailMimeBuilder.cs` |
| QuestPDF | Generates PDF reports for adoption requests, adoption status, low stock, shelter registration, and shelter summaries. | `Services/PdfReportService.cs` |
| Quartz.NET | Runs scheduled background jobs, such as visit reminders and shelter summary reports. | `Jobs/VisitReminderJob.cs`, `Jobs/ShelterSummaryReportJob.cs`, `Program.cs` |
| xUnit | Automated testing framework. | `PawConnect.Tests/PawConnect.Tests.csproj`, `PawConnect.Tests/Tests/*` |
| OpenAI API | Optional AI enhancement for recommendations, Adoption Copilot tool calling, and embeddings. | `Services/OpenAiSettings.cs`, `Services/OpenAiRecommendationClient.cs`, `Services/OpenAiAdoptionCopilotClient.cs`, `Services/OpenAiEmbeddingService.cs` |

Presentation sentence:
"The application uses ASP.NET Core and Blazor Server for the web app, EF Core with SQL Server for persistence, Identity for role security, MudBlazor for UI, and optional OpenAI services for recommendations, semantic search, and the Adoption Copilot."

## 3. Project Structure

| Folder/File | Purpose | What I should say in presentation |
|------------|---------|-----------------------------------|
| `Program.cs` | Application startup, dependency injection, authentication, services, Quartz jobs, OpenAI configuration. | "This is where the app is assembled." |
| `Components` | Blazor UI root folder. | "The UI is built with Razor components." |
| `Components/Pages` | Routeable pages such as `/dogs`, `/adopter/copilot`, `/admin/dogs`. | "Each main screen is a Razor page." |
| `Components/Pages/Adopter` | Adopter-only pages: dashboard, profile, favorites, recommendations, Copilot, adoption requests. | "Adopter functionality is grouped by role." |
| `Components/Pages/Shelter` | Shelter-only pages: dashboard, manage dogs, resources, adoption requests. | "Shelters manage operational data here." |
| `Components/Pages/Admin` | Admin-only pages for users, shelters, dogs, reports, activity logs, applications, adoption requests. | "Admins supervise the platform." |
| `Components/Shared` | Reusable dialogs/components such as maps, notifications, request details, status history. | "Common UI pieces are reused instead of duplicated." |
| `Components/Layout` | Main layout and navigation menu. | "Role-based navigation lives here." |
| `Components/Account` | Identity pages and account management. | "Login, registration, and account pages come from ASP.NET Identity." |
| `Data` | EF Core context, application user, migrations, seed data. | "This folder defines database setup and startup data." |
| `Entities` | Domain entities mapped to database tables. | "These classes represent the business data." |
| `Services` | Business logic, AI integration, email, PDF, exports, imports, maps, recommendations, Copilot. | "Razor pages call services instead of directly writing business logic." |
| `Jobs` | Quartz scheduled background jobs. | "Periodic tasks are separated from UI requests." |
| `Repositories` | Generic EF repository abstraction. | "A small generic repository is available for shared data access patterns." |
| `wwwroot` | Static assets, CSS, JavaScript, images. | "Static files used by the UI live here." |
| `PawConnect.Tests` | xUnit tests and test helpers. | "The project includes service and integration-style tests." |
| `docs` | Technical documentation and generated diagrams. | "Documentation is kept with the project." |

## 4. How the Application Starts

When the app starts, `Program.cs` configures the database, authentication, UI services, business services, optional AI services, and background jobs.

Startup flow:

1. Configuration is loaded from `appsettings.json`, `appsettings.Development.json`, user secrets, and environment variables.
2. Razor components are registered with `AddRazorComponents().AddInteractiveServerComponents()`.
3. MudBlazor services are registered with `AddMudServices()`.
4. Authentication is configured with Identity cookies.
5. `ApplicationDbContext` is registered with SQL Server using the `DefaultConnection` connection string.
6. `ApplicationUser` and Identity roles are registered with EF Core stores.
7. Application services are registered with dependency injection.
8. OpenAI settings are registered from the `OpenAI` configuration section.
9. HTTP clients are registered for Nominatim geocoding and OpenAI clients.
10. Quartz jobs are registered for scheduled reports and visit reminders.
11. The request pipeline is built, including HTTPS, static assets, antiforgery, Razor components, and Identity endpoints.
12. `IdentitySeedData.SeedAsync(...)` seeds roles, demo users, lookup data, shelters, and demo dogs.

Important services registered in `Program.cs` include:

- Dog and shelter services: `IDogService`, `IShelterService`, `IDogImageService`, `IMedicalRecordService`, `IDogBreedService`
- Adoption services: `IAdoptionRequestService`, `IFavoriteDogService`, `IRecentlyViewedDogService`, `IAdopterProfileService`
- Reporting and communication: `IEmailService`, `IPdfReportService`, `IExportService`, `ICsvImportService`, `IReportHistoryService`
- Platform services: `IAuditLogService`, `INotificationService`, `IResourceStockService`
- AI services: `IDogRecommendationService`, `IAdoptionCopilotService`, `IAdoptionCopilotToolService`, `ISemanticDogSearchService`, `IDogSearchEmbeddingService`, `IEmbeddingService`
- State and helpers: `ICopilotStateService`, `IDistanceService`, `IGeocodingService`

Important configuration keys:

```json
"OpenAI": {
  "Enabled": true,
  "ApiKey": "",
  "Model": "gpt-5.4-mini",
  "ChatModel": "gpt-5.4-mini",
  "EmbeddingModel": "text-embedding-3-small"
}
```

In the current project, the OpenAI section exists but the API key is empty in committed settings. The OpenAI clients check both `Enabled` and `HasApiKey`, so if the key is missing the app uses safe fallback behavior.

## 5. Database Design

The EF Core database context is `Data/ApplicationDbContext.cs`. It inherits from:

```csharp
IdentityDbContext<ApplicationUser, IdentityRole, string>
```

This means PawConnect uses ASP.NET Core Identity tables plus custom PawConnect tables.

| Entity | File | Purpose | Important fields | Relationships |
|-------|------|---------|------------------|---------------|
| `ApplicationUser` | `Data/ApplicationUser.cs` | Identity user extended with PawConnect profile links. | `FullName`, `Shelter`, `AdopterProfile`, collections for favorites, requests, notifications. | One user can be an adopter, shelter user, or admin through roles. |
| `Shelter` | `Entities/Shelter.cs` | Shelter profile and location. | `Name`, `Address`, `City`, `Neighborhood`, `Latitude`, `Longitude`, `ApplicationUserId`. | One shelter has many dogs and resources; optional one-to-one with a shelter user. |
| `Dog` | `Entities/Dog.cs` | Main adoptable dog record. | `Name`, `Breed` legacy text, `DogBreedId`, `IsMixedBreed`, `CustomBreedName`, `AgeYears`, `AgeMonths`, `Size`, `Status`, `Description`, `BehaviorDescription`, `MedicalStatus`, `ShelterId`. | Belongs to one shelter; has images, medical records, status history, requests, favorites, recently viewed rows. |
| `DogBreed` | `Entities/DogBreed.cs` | Lookup table for consistent breed values. | `Name`, `IsActive`, `GeneralDescription`, `TypicalTraits`, `CareNotes`, `CommonHealthConsiderations`. | One breed can be referenced by many dogs. |
| `DogImage` | `Entities/DogImage.cs` | Dog image URLs. | `DogId`, `ImageUrl`, `IsMainImage`. | Many images belong to one dog. |
| `MedicalRecord` | `Entities/MedicalRecord.cs` | Dog medical records. | `VaccineName`, `TreatmentDescription`, `RecordDate`, `Notes`. | Many records belong to one dog. |
| `AdoptionRequest` | `Entities/AdoptionRequest.cs` | Adoption request and visit workflow. | `DogId`, `AdopterId`, `Status`, `VisitStatus`, `PreferredVisitDateTime`, `ReasonForAdoption`, `HoursAlonePerDay`, `ShelterInternalNotes`. | Links adopter user to dog. |
| `FavoriteDog` | `Entities/FavoriteDog.cs` | Join table for adopter favorites. | `AdopterId`, `DogId`, `CreatedAt`. | Many-to-many between adopters and dogs. |
| `RecentlyViewedDog` | `Entities/RecentlyViewedDog.cs` | Tracks adopter dog views. | `AdopterId`, `DogId`, `ViewedAt`. | Many-to-many style tracking table. |
| `AdopterProfile` | `Entities/AdopterProfile.cs` | Adopter preferences and home context. | `City`, `HousingType`, `HasYard`, `HasOtherPets`, `HasChildren`, `ExperienceWithDogs`, `AdditionalNotes`. | One-to-one with `ApplicationUser`. |
| `ResourceStock` | `Entities/ResourceStock.cs` | Shelter inventory item. | `Name`, `Quantity`, `Unit`, `LowStockThreshold`, `ShelterId`, `ResourceCategoryId`, `FoodTypeId`. | Belongs to shelter and lookup category/food type. |
| `ResourceCategory` | `Entities/ResourceCategory.cs` | Resource category lookup. | `Name`, `IsActive`. | Used by resource stock. |
| `FoodType` | `Entities/FoodType.cs` | Food type lookup. | `Name`, `IsActive`. | Used by resources and preferred dog food. |
| `DogStatusHistory` | `Entities/DogStatusHistory.cs` | Tracks dog status transitions. | `OldStatus`, `NewStatus`, `ChangedAt`, `ChangedByUserId`, `Notes`. | Belongs to dog; optional changed-by user. |
| `Notification` | `Entities/Notification.cs` | User notification bell data. | `UserId`, `Title`, `Message`, `Category`, `Type`, `Link`, `IsRead`. | Belongs to one user. |
| `ReportHistory` | `Entities/ReportHistory.cs` | Records generated/sent reports. | `ReportType`, `RecipientEmail`, `FileName`, `WasSuccessful`, `ShelterId`. | Optional shelter link. |
| `AuditLog` | `Entities/AuditLog.cs` | Platform traceability log. | `Action`, `EntityName`, `EntityId`, `Description`, `UserId`, `UserRole`, `CreatedAt`. | Stores who did what and when. |
| `ShelterRegistrationRequest` | `Entities/ShelterRegistrationRequest.cs` | Public shelter application before admin approval. | `ShelterName`, `ContactPersonName`, `Email`, `City`, `Neighborhood`, `Latitude`, `Longitude`, `Status`. | Can create a user and shelter after approval. |
| `DogSearchEmbedding` | `Entities/DogSearchEmbedding.cs` | Stores semantic search vector for one dog. | `DogId`, `Content`, `ContentHash`, `EmbeddingJson`, `EmbeddingModel`, `UpdatedAt`. | One-to-one with `Dog`. |

Relationship concepts:

- One-to-many: one `Shelter` has many `Dog` records.
- One-to-one: one `ApplicationUser` can have one `AdopterProfile`.
- Join entities: `FavoriteDog` and `RecentlyViewedDog` connect adopters to dogs.
- Lookup tables: `DogBreed`, `ResourceCategory`, and `FoodType` keep values consistent.
- History tables: `DogStatusHistory`, `AuditLog`, and `ReportHistory` keep traceability.

EF Core migrations in `Data/Migrations` are used to evolve the database schema safely. Examples include dog ages, adopter profiles, dog status history, notifications, report history, shelter coordinates, DogSearchEmbeddings, shelter neighborhoods, dog breeds, and breed information fields.

## 6. Main User Flows

### Public Dog Browsing Flow

Step 1:
Page: `Components/Pages/Dogs.razor`  
Service: `IDogService.SearchDogsAsync(...)`  
Database: `Dogs`, `Shelters`, `DogBreeds`, `DogImages`  
Explanation: The visitor opens `/dogs`, and the page loads only public-safe dogs.

Step 2:
Page: `Components/Pages/Dogs.razor`  
Service: `DogService.SearchDogsAsync(...)`  
Rule: Only `Available` and `Reserved` dogs are shown by default. `Adopted` and `InTreatment` dogs are excluded from public browsing.

Step 3:
Page: `Components/Pages/DogDetails.razor`  
Service: `IDogService.GetDogDetailsAsync(id)`  
Explanation: The visitor clicks a dog and sees details, images, behavior, medical status, shelter, food, medical records, and breed information.

### Adopter Flow

Step 1:
Page: `Components/Account/Pages/Register.razor` or `Login.razor`  
Service: ASP.NET Core Identity  
Database: Identity tables and `ApplicationUser`  
Explanation: A user registers or logs in as an adopter.

Step 2:
Page: `Components/Pages/Adopter/MyAdopterProfile.razor`  
Service: `IAdopterProfileService`  
Database: `AdopterProfiles`  
Explanation: The adopter completes home and preference information such as city, housing type, yard, pets, children, and dog experience.

Step 3:
Page: `Components/Pages/Adopter/AdopterDashboard.razor`  
Services: `IDogRecommendationService`, `IAdoptionRequestService`, `INotificationService`  
Explanation: The adopter sees dashboard information and top recommendations.

Step 4:
Page: `Components/Pages/Adopter/Favorites.razor`  
Service: `IFavoriteDogService`  
Database: `FavoriteDogs`  
Explanation: The adopter saves and reviews favorite dogs.

Step 5:
Page: `Components/Pages/Adopter/Recommendations.razor`  
Service: `IDogRecommendationService`  
Explanation: The adopter receives personalized dog recommendations based on profile, favorites, recently viewed dogs, and rule-based scoring.

Step 6:
Page: `Components/Pages/Adopter/AdoptionCopilot.razor`  
Service: `IAdoptionCopilotService`  
Explanation: The adopter types natural language requests and receives real dog suggestions backed by PawConnect data.

Step 7:
Page: `Components/Pages/Adopter/MyAdoptionRequests.razor`  
Service: `IAdoptionRequestService`  
Database: `AdoptionRequests`  
Explanation: The adopter tracks requests, visit status, and cancellation options.

### Shelter Flow

Step 1:
Page: `Components/Pages/Shelter/ShelterDashboard.razor`  
Service: `IShelterService`, `IAdoptionRequestService`, resource/report services  
Explanation: The shelter sees operational summary data.

Step 2:
Page: `Components/Pages/Shelter/ManageDogs.razor`  
Service: `IDogService`  
Database: `Dogs`  
Explanation: The shelter lists and manages only its own dogs.

Step 3:
Pages: `Components/Pages/Shelter/CreateDog.razor`, `Components/Pages/Shelter/EditDog.razor`  
Services: `IDogService`, `IDogBreedService`, `IDogImageService`, `IMedicalRecordService`  
Explanation: The shelter creates or edits dogs, selects breed from autocomplete, adds images and medical information.

Step 4:
Page: `Components/Pages/Shelter/ShelterAdoptionRequests.razor`  
Service: `IAdoptionRequestService`  
Database: `AdoptionRequests`, `DogStatusHistories`, `Notifications`  
Explanation: The shelter reviews requests, confirms visits, rejects requests, and marks adoption completed.

Step 5:
Page: `Components/Pages/Shelter/Resources.razor`  
Service: `IResourceStockService`, `ICsvImportService`, `IExportService`  
Explanation: The shelter manages food and resource stock, imports CSV, exports reports, and receives low-stock signals.

### Admin Flow

Step 1:
Page: `Components/Pages/Admin/AdminDashboard.razor`  
Explanation: Admin sees platform-level overview.

Step 2:
Pages: `AdminUsers.razor`, `AdminShelters.razor`, `AdminDogs.razor`  
Services: `UserManager<ApplicationUser>`, `IShelterService`, `IDogService`  
Explanation: Admin supervises users, shelters, and dogs.

Step 3:
Page: `Components/Pages/Admin/AdminShelterRequests.razor`  
Service: `IShelterRegistrationRequestService`  
Explanation: Admin accepts or rejects public shelter applications.

Step 4:
Page: `Components/Pages/Admin/AdminAdoptionRequests.razor`  
Service: `IAdoptionRequestService`  
Component: `Components/Shared/AdoptionRequestDetailsDialog.razor`  
Explanation: Admin views adoption request activity across all shelters and can open request details.

Step 5:
Page: `Components/Pages/Admin/AdminDogs.razor`  
Services: `IDogService`, `IDogSearchEmbeddingService`  
Explanation: Admin can view dog status history and rebuild the semantic dog search index.

### Shelter Application Flow

Step 1:
Page: `Components/Pages/ShelterApply.razor`  
Service: `IShelterRegistrationRequestService.SubmitRequestAsync(...)`  
Database: `ShelterRegistrationRequests`  
Explanation: A public user submits a shelter application with contact, address, city, neighborhood, and optional map coordinates.

Step 2:
Page: `Components/Pages/Admin/AdminShelterRequests.razor`  
Service: `ShelterRegistrationRequestService.AcceptRequestAsync(...)` or `RejectRequestAsync(...)`  
Database: `ApplicationUser`, `Shelters`, `ShelterRegistrationRequests`  
Explanation: Admin approval creates a shelter user, assigns the Shelter role, and creates a shelter profile.

### Adoption Request Flow

Step 1:
Page: `Components/Pages/DogDetails.razor`  
Service: `AdoptionRequestService.CreateRequestAsync(...)`  
Database: `AdoptionRequests`  
Rule: The dog must be `Available` or `Reserved`, and duplicate active requests are blocked.

Step 2:
Page: `Components/Pages/Shelter/ShelterAdoptionRequests.razor`  
Service: `AdoptionRequestService.ConfirmVisitAsync(...)`  
Database: `AdoptionRequests`, `Dogs`, `DogStatusHistories`  
Rule: Confirming a visit sets the request to `VisitConfirmed`, sets visit status to `Confirmed`, and reserves the dog.

Step 3:
Page: `ShelterAdoptionRequests.razor`  
Service: `AdoptionRequestService.MarkAsAdoptedAsync(...)`  
Rule: After a confirmed visit, the shelter can mark adoption completed. The request becomes `Accepted`, the visit becomes `Completed`, and the dog becomes `Adopted`.

Step 4:
Supporting services: `SmtpEmailService`, `PdfReportService`, `NotificationService`, `AuditLogService`, `ReportHistoryService`  
Explanation: The system sends emails, generates PDFs, creates notifications, and logs important actions.

## 7. Services Layer

The service layer keeps business rules out of Razor components. Razor components handle UI, while services handle validation, database access, status transitions, notifications, email, PDF, AI, and security-related checks.

| Service | Interface | Implementation | What it does | Used by |
|---------|-----------|----------------|--------------|---------|
| Dog service | `Services/IDogService.cs` | `Services/DogService.cs` | Dog CRUD, public dog search, admin dog list, status history, delete rules, breed normalization. | Dogs, DogDetails, shelter/admin dog pages. |
| Dog image service | `Services/IDogImageService.cs` | `Services/DogImageService.cs` | Manage dog images and main image selection. | Create/Edit dog pages. |
| Medical record service | `Services/IMedicalRecordService.cs` | `Services/MedicalRecordService.cs` | Manage dog medical records. | Shelter dog edit/details flows. |
| Adoption request service | `Services/IAdoptionRequestService.cs` | `Services/AdoptionRequestService.cs` | Submit, confirm, reject, cancel, and complete adoption requests. | Dog details, adopter/shelter/admin request pages. |
| Favorite dog service | `Services/IFavoriteDogService.cs` | `Services/FavoriteDogService.cs` | Save/remove favorites and check favorite state. | Dog cards, favorites page, Copilot. |
| Recently viewed service | `Services/IRecentlyViewedDogService.cs` | `Services/RecentlyViewedDogService.cs` | Track recently viewed dogs for adopters. | Dog details and recommendations. |
| Adopter profile service | `Services/IAdopterProfileService.cs` | `Services/AdopterProfileService.cs` | Create/update adopter profiles. | Adopter profile and recommendation features. |
| Shelter service | `Services/IShelterService.cs` | `Services/ShelterService.cs` | Shelter data, profile and location. | Shelter pages, public shelter pages, admin pages. |
| Shelter registration service | `Services/IShelterRegistrationRequestService.cs` | `Services/ShelterRegistrationRequestService.cs` | Public shelter applications and admin approval. | Shelter application and admin review pages. |
| Resource stock service | `Services/IResourceStockService.cs` | `Services/ResourceStockService.cs` | Shelter resources, thresholds, stock management. | Shelter resources page. |
| Notification service | `Services/INotificationService.cs` | `Services/NotificationService.cs` | Create/read notification bell items. | Layout, notifications page, services. |
| Email service | `Services/IEmailService.cs` | `Services/SmtpEmailService.cs` | Send email through SMTP with optional attachments. | Adoption, reports, shelter registration. |
| PDF report service | `Services/IPdfReportService.cs` | `Services/PdfReportService.cs` | Generate PDF files with QuestPDF. | Email/report/export flows. |
| Export service | `Services/IExportService.cs` | `Services/ExportService.cs` | CSV and PDF exports. | Admin and shelter export buttons. |
| CSV import service | `Services/ICsvImportService.cs` | `Services/CsvImportService.cs` | Preview and import dogs/resources/shelter requests from CSV. | Shelter/admin import flows. |
| Audit log service | `Services/IAuditLogService.cs` | `Services/AuditLogService.cs` | Record important user actions. | Service actions and admin activity log. |
| Report history service | `Services/IReportHistoryService.cs` | `Services/ReportHistoryService.cs` | Store generated/sent report metadata. | Report pages and scheduled jobs. |
| Recommendation service | `Services/IDogRecommendationService.cs` | `Services/DogRecommendationService.cs` | Rule-based dog recommendations with optional OpenAI enhancement. | Recommendations page and adopter dashboard. |
| Adoption Copilot service | `Services/IAdoptionCopilotService.cs` | `Services/AdoptionCopilotService.cs` | Main Copilot orchestration, deterministic parsing, tool calling, fallback. | `/adopter/copilot`. |
| Copilot tool service | `Services/IAdoptionCopilotToolService.cs` | `Services/AdoptionCopilotToolService.cs` | Safe application tools for Copilot, candidate retrieval, evidence extraction and scoring. | Adoption Copilot and OpenAI tool calls. |
| Semantic dog search service | `Services/ISemanticDogSearchService.cs` | `Services/SemanticDogSearchService.cs` | Semantic or keyword/rule-based dog search. | Copilot and semantic search features. |
| Dog search document service | `Services/IDogSearchDocumentService.cs` | `Services/DogSearchDocumentService.cs` | Builds public-safe text documents for embeddings. | Embedding refresh. |
| Dog search embedding service | `Services/IDogSearchEmbeddingService.cs` | `Services/DogSearchEmbeddingService.cs` | Refresh, rebuild, remove stale dog embeddings. | Admin dog search index action and dog changes. |
| OpenAI recommendation client | `Services/IOpenAiRecommendationClient.cs` | `Services/OpenAiRecommendationClient.cs` | Optional OpenAI recommendation explanation/reranking. | Recommendation service. |
| OpenAI Copilot client | `Services/IOpenAiAdoptionCopilotClient.cs` | `Services/OpenAiAdoptionCopilotClient.cs` | OpenAI Responses API tool/function calling for Copilot. | Adoption Copilot service. |
| OpenAI embedding service | `Services/IEmbeddingService.cs` | `Services/OpenAiEmbeddingService.cs` | Calls OpenAI embeddings API and computes cosine similarity. | Semantic search index and query embedding. |
| OpenAI settings | - | `Services/OpenAiSettings.cs` | Stores `Enabled`, `ApiKey`, `Model`, `ChatModel`, `EmbeddingModel`, and helper properties. | All OpenAI clients. |

## 8. Authentication and Authorization

PawConnect uses ASP.NET Core Identity with a custom `ApplicationUser`. Roles are seeded in `Data/IdentitySeedData.cs`:

- `Adopter`
- `Shelter`
- `Admin`

Pages are protected with `[Authorize]` and role-specific attributes. Examples:

- `Components/Pages/Adopter/AdoptionCopilot.razor` uses `[Authorize(Roles = "Adopter")]`.
- `Components/Pages/Shelter/ManageDogs.razor` is shelter-only.
- `Components/Pages/Admin/AdminDogs.razor` is admin-only.

Service-level checks are also important:

- A shelter can only edit dogs belonging to its own `ShelterId`.
- An adopter can only cancel their own pending adoption requests.
- Admin pages use platform-wide service methods.
- Public dog browsing only returns `Available` and `Reserved` dogs.
- Copilot and recommendations use adopter identity but send only sanitized profile fields to OpenAI.

Presentation sentence:
"Authorization is not only in the menu. It is also enforced in services, so even if a user tries to call a route directly, ownership and role rules still protect the data."

## 9. Dog Management

A shelter creates or edits dogs in:

- `Components/Pages/Shelter/CreateDog.razor`
- `Components/Pages/Shelter/EditDog.razor`
- `Components/Pages/Shelter/ManageDogs.razor`

The main service is `Services/DogService.cs`.

Important dog fields:

- Identity: `Name`
- Breed: `DogBreedId`, `DogBreed`, `IsMixedBreed`, `CustomBreedName`, legacy `Breed`
- Age: `AgeYears`, `AgeMonths`
- Size: `DogSize`
- Status: `DogStatus`
- Public text: `Description`, `BehaviorDescription`, `MedicalStatus`
- Location: `Location`, shelter city/neighborhood
- Food: `PreferredFoodTypeId`, `DailyFoodAmountGrams`
- Relationships: images, medical records, adoption requests, status history

Important rules in `DogService`:

- Public queries only show `Available` and `Reserved`.
- `Adopted` dogs are read-only for shelter editing.
- Delete is blocked when adoption request history exists.
- Status changes create `DogStatusHistory` records.
- When a dog is created, edited, or its status changes, the embedding refresh is attempted best-effort. If embedding generation fails, the dog change still succeeds.

Status history is displayed with:

- `Components/Shared/DogStatusHistoryDialog.razor`
- `DogService.GetStatusHistoryForDogAsync(...)`
- `DogService.GetStatusHistoryForShelterDogAsync(...)`

## 10. Dog Breed System

PawConnect now has a database-backed breed lookup table.

Files:

- Entity: `Entities/DogBreed.cs`
- Formatter: `Services/DogBreedFormatter.cs`
- Information formatter: `Services/DogBreedInformationFormatter.cs`
- Service: `Services/DogBreedService.cs`
- Seed data: `Data/DogBreedSeedData.cs`
- Migration examples: `Data/Migrations/20260517120000_AddDogBreeds.cs`, `20260518110000_AddDogBreedInformation.cs`, `20260518113000_AddDogBreedHealthConsiderations.cs`

Important fields:

- `Dog.DogBreedId`
- `Dog.DogBreed`
- `Dog.IsMixedBreed`
- `Dog.CustomBreedName`
- legacy `Dog.Breed`

Display examples:

| Stored data | Display value |
|-------------|---------------|
| DogBreed = Labrador Retriever, not mixed | Labrador Retriever |
| DogBreed = Labrador Retriever, mixed | Labrador Retriever Mix |
| DogBreed = Mixed Breed | Mixed Breed |
| DogBreed = Unknown | Unknown |
| CustomBreedName = "Local Shepherd" | Local Shepherd |

Why this is better than free text:

- avoids values like `labrador mix`, `Labrador Mix`, `Mixed`, `unknown`
- improves filters, exports, search documents, Copilot candidate data
- still supports mixed, unknown, and custom breeds

Breed UI:

- Shelter create/edit dog forms use breed autocomplete.
- Shelters can mark a dog as mixed.
- Shelters can use a custom breed name when needed.

Breed information:

- `DogDetails.razor` shows a "Breed Information" section.
- It displays overview, typical traits, care context, common health considerations, and a disclaimer.
- Breed information is educational only. The dog's own description, behavior, medical status, and medical records are more important.

Breed is used in:

- public dog cards
- dog details
- recommendations
- semantic search documents
- Adoption Copilot candidate data
- CSV import/export
- PDF reports

## 11. Adoption Request System

Main files:

- Entity: `Entities/AdoptionRequest.cs`
- Status enum: `Entities/AdoptionRequestStatus.cs`
- Visit enum: `Entities/AdoptionVisitStatus.cs`
- Service: `Services/AdoptionRequestService.cs`
- Adopter page: `Components/Pages/Adopter/MyAdoptionRequests.razor`
- Shelter page: `Components/Pages/Shelter/ShelterAdoptionRequests.razor`
- Admin page: `Components/Pages/Admin/AdminAdoptionRequests.razor`
- Details dialog: `Components/Shared/AdoptionRequestDetailsDialog.razor`

Important questionnaire fields:

- `ReasonForAdoption`
- `HoursAlonePerDay`
- `AdditionalInformation`
- `PreferredVisitDateTime`
- `Message`
- `ShelterInternalNotes`
- `VisitNotes`

Request status transitions:

```text
Pending -> VisitConfirmed -> Accepted
Pending -> Rejected
Pending -> Cancelled
VisitConfirmed -> Rejected
```

Visit status transitions:

```text
Requested -> Confirmed -> Completed
Requested -> Cancelled
Confirmed -> Cancelled
```

Dog status transitions during adoption:

```text
Available -> Reserved -> Adopted
```

Important service behavior:

- `CreateRequestAsync(...)` validates adopter role, questionnaire data, preferred visit time, dog visibility, and duplicate active requests.
- `ConfirmVisitAsync(...)` is shelter-scoped, reserves the dog, confirms visit status, rejects other pending requests for that dog, sends email/calendar notification, and creates notifications/audit logs.
- `MarkAsAdoptedAsync(...)` completes adoption, changes dog status to `Adopted`, rejects other active requests, sends reports and notifications.
- `RejectRequestAsync(...)` rejects a request and can return a reserved dog to available.
- `CancelRequestAsync(...)` lets an adopter cancel only their own pending request.

## 12. Email and PDF System

Main files:

- `Services/IEmailService.cs`
- `Services/SmtpEmailService.cs`
- `Services/EmailSettings.cs`
- `Services/EmailMimeBuilder.cs`
- `Services/PawConnectEmailTemplate.cs`
- `Services/IPdfReportService.cs`
- `Services/PdfReportService.cs`

Email:

- MailKit and MimeKit build and send emails.
- SMTP settings come from `EmailSettings`.
- Development settings use local SMTP style configuration.
- `SmtpEmailService` logs warnings if SMTP is not configured or sending fails.

PDF:

- QuestPDF is configured with the Community license in `PdfReportService`.
- PDFs are generated for adoption requests, adoption status, low stock resources, shelter registration requests, and shelter summaries.

Examples:

- Adoption request notification with PDF attachment.
- Adoption status report.
- Low stock resource report.
- Shelter summary report.
- Visit confirmation email with `.ics` calendar invite.

Important design rule:
Email and PDF failures are logged and handled carefully. They should not roll back the main business action, such as confirming a visit or creating a request.

## 13. Notifications, Audit Logs, and Report History

| Feature | Entity | Service | UI |
|---------|--------|---------|----|
| Notifications | `Entities/Notification.cs` | `Services/NotificationService.cs` | `Components/Shared/NotificationBell.razor`, `Components/Pages/Notifications.razor` |
| Audit logs | `Entities/AuditLog.cs` | `Services/AuditLogService.cs` | `Components/Pages/Admin/AdminActivityLog.razor` |
| Report history | `Entities/ReportHistory.cs` | `Services/ReportHistoryService.cs` | `Components/Pages/Admin/AdminReportHistory.razor` and shelter report views |

Why these matter:

- Notifications give users immediate feedback.
- Audit logs help admins trace important actions.
- Report history records generated and sent reports for accountability.

## 14. Resource Stock System

Main files:

- `Entities/ResourceStock.cs`
- `Entities/ResourceCategory.cs`
- `Entities/FoodType.cs`
- `Services/IResourceStockService.cs`
- `Services/ResourceStockService.cs`
- `Components/Pages/Shelter/Resources.razor`

Resource stock is shelter-scoped. Each item can have:

- name
- quantity
- unit
- low stock threshold
- category
- food type, where relevant

The low stock threshold helps shelters identify supplies that need attention. Resource data can be imported/exported through CSV and included in reports.

## 15. CSV Import/Export

Main files:

- `Services/ICsvImportService.cs`
- `Services/CsvImportService.cs`
- `Services/IExportService.cs`
- `Services/ExportService.cs`
- `Services/IBrowserFileDownloadService.cs`
- `Services/BrowserFileDownloadService.cs`

CSV import:

- Resource import is shelter-scoped.
- Dog import reads dog data, including breed text.
- Breed text is parsed with `DogBreedFormatter.Parse(...)`.
- Unknown or unmatched breed text can become `CustomBreedName` instead of failing.
- Preview methods show row errors before import.

CSV/PDF export:

- Admin can export users, shelters, dogs, adoption requests, and shelter requests.
- Shelter can export its own operational records.
- Dog export uses formatted breed display, not raw `DogBreedId`.

Presentation sentence:
"CSV import/export keeps the demo practical because shelters and admins can move data in and out without direct database access."

## 16. Scheduled Jobs

PawConnect uses Quartz.NET for scheduled background jobs.

| Job | File | Purpose | Settings |
|-----|------|---------|----------|
| Shelter summary report | `Jobs/ShelterSummaryReportJob.cs` | Sends scheduled shelter summary reports. | `ScheduledReportSettings` |
| Visit reminder | `Jobs/VisitReminderJob.cs` | Sends reminders for upcoming confirmed visits. | `VisitReminderSettings` |

Quartz registration is in `Program.cs`.

Development note:
Some jobs can be disabled in development configuration. For example, `appsettings.Development.json` disables visit reminders. This keeps local testing from sending reminders unexpectedly.

## 17. Maps and Location

Main files:

- `Services/IGeocodingService.cs`
- `Services/NominatimGeocodingService.cs`
- `Services/IDistanceService.cs`
- `Services/DistanceService.cs`
- `Components/Shared/ShelterMap.razor`

Location features:

- Shelters store `Latitude` and `Longitude`.
- Shelters also store `City` and optional `Neighborhood`.
- Maps use Leaflet/OpenStreetMap in the frontend.
- Geocoding uses Nominatim, not Google Maps API.
- Reverse geocoding can extract neighborhood from fields such as `neighbourhood`, `suburb`, `quarter`, `city_district`, or `district`.
- Nearby browsing uses Haversine distance from `DistanceService`.

Privacy note:
Browser location is optional and used for nearby calculations. It is not stored as a permanent user search location.

## 18. Recommended Dogs System - Explain in Detail

The Recommended Dogs feature gives adopters personalized dog suggestions. It is different from normal browsing because it uses adopter profile information, favorites, recently viewed dogs, and scoring rules.

Main files:

- `Services/IDogRecommendationService.cs`
- `Services/DogRecommendationService.cs`
- `Services/DogRecommendationResult.cs`
- `Services/RecommendationOpenAiRequest.cs`
- `Services/IOpenAiRecommendationClient.cs`
- `Services/OpenAiRecommendationClient.cs`
- `Components/Pages/Adopter/Recommendations.razor`
- `Components/Pages/Adopter/AdopterDashboard.razor`

Where it appears:

- Full page: `/adopter/recommendations`
- Dashboard section: `Recommended for you` in `/adopter/dashboard`

High-level flow:

```text
Adopter opens recommendations
-> DogRecommendationService loads adopter profile
-> service loads public-safe dogs
-> rule-based scoring evaluates fit
-> optional OpenAI enhancement receives sanitized candidates
-> backend validates AI output
-> UI displays cards with score, label, summary, and reasons
```

Public-safe filtering:

- Recommended dogs are loaded from dogs with status `Available` or `Reserved`.
- `Adopted` and `InTreatment` dogs are not recommended.

Rule-based scoring uses:

- Home fit: apartment, house, yard, dog size.
- Location fit: same city or nearby signals.
- Behavior fit: calm, active, social, gentle, children, pets.
- Experience fit: adopter dog experience compared with dog energy/training language.
- Preferences fit: favorite and recently viewed dog breed/size patterns.
- Status: `Available` receives a small positive signal.

Example:
If the adopter lives in an apartment and has no yard, smaller or medium dogs with calmer behavior receive stronger recommendation signals. A large energetic dog receives less benefit for that profile.

Match percentage:

- The internal score is transformed into a visible percentage-like value.
- The UI shows text such as `86% match`.
- The UI does not expose the raw internal score.

Match labels:

- `Excellent match`
- `Good match`
- `Possible match`

Reasons and categories:

`DogRecommendationService` creates `DogRecommendationReason` items grouped by categories such as:

- Home fit
- Experience fit
- Location fit
- Behavior fit
- Preferences fit

OpenAI enhancement:

- `OpenAiRecommendationClient` calls the OpenAI Responses API if OpenAI is enabled and an API key exists.
- It receives only sanitized adopter profile data and backend-provided dog candidates.
- It can improve summaries, reasons, and ordering.
- It cannot add dogs that were not already backend candidates.
- Unknown dog IDs are ignored by `DogRecommendationService`.
- If OpenAI is disabled, missing a key, fails, or returns invalid output, PawConnect keeps the rule-based results.

Privacy:

The recommendation OpenAI request includes safe profile fields such as city, housing type, yard, pets, children, and dog experience. It does not include adopter full name, email, phone, exact address, passwords, tokens, or private notes.

Presentation sentence:
"The recommendation system is rule-based first. OpenAI is optional and can polish or rerank only the candidates the backend already selected."

## 19. Adoption Copilot System - Explain in Very High Detail

The Adoption Copilot is located at:

- Route: `/adopter/copilot`
- UI file: `Components/Pages/Adopter/AdoptionCopilot.razor`
- Main service: `Services/AdoptionCopilotService.cs`

The Copilot lets an adopter type natural language requests such as:

- "calm dogs in Zorilor"
- "I have a cat at home"
- "I have a sick dog recovering at home"
- "small dog for an apartment"
- "active dog for a house with a yard"

It differs from normal browsing and recommendations:

| Feature | Input | Main logic | Output |
|--------|-------|------------|--------|
| Browse Dogs | Filters/search text | `DogService` public filtering | Dog list |
| Recommended Dogs | Adopter profile/preferences | Rule-based scoring plus optional OpenAI enhancement | Personalized recommendations |
| Adoption Copilot | Natural-language query | Intent parsing, safe tools, semantic/rule-based search, evidence scoring, optional OpenAI explanation | AI-assisted dog suggestions |

Important files:

- `Services/IAdoptionCopilotService.cs`
- `Services/AdoptionCopilotService.cs`
- `Services/IAdoptionCopilotToolService.cs`
- `Services/AdoptionCopilotToolService.cs`
- `Services/AdoptionCopilotModels.cs`
- `Services/AdoptionCopilotToolModels.cs`
- `Services/IOpenAiAdoptionCopilotClient.cs`
- `Services/OpenAiAdoptionCopilotClient.cs`
- `Services/ISemanticDogSearchService.cs`
- `Services/SemanticDogSearchService.cs`
- `Services/IDogSearchDocumentService.cs`
- `Services/DogSearchDocumentService.cs`
- `Services/IDogSearchEmbeddingService.cs`
- `Services/DogSearchEmbeddingService.cs`
- `Services/IEmbeddingService.cs`
- `Services/OpenAiEmbeddingService.cs`
- `Entities/DogSearchEmbedding.cs`
- `Services/OpenAiSettings.cs`
- `Components/Pages/Admin/AdminDogs.razor`

Copilot flow:

1. The adopter writes a natural-language query in `AdoptionCopilot.razor`.
2. `AdoptionCopilotService.AskAsync(...)` receives the current adopter user id and message.
3. The service performs deterministic parsing for constraints such as size, age, neighborhood, city, home type, activity level, status, compatibility target, and lifestyle.
4. The service calls `AdoptionCopilotToolService.SearchDogsAsync(...)` first, so PawConnect has safe backend candidates even before OpenAI.
5. The tool service applies hard filters:
   - `Available` and `Reserved` only.
   - `Adopted` and `InTreatment` excluded.
   - explicit neighborhood constraints respected.
   - explicit size constraints respected.
   - age constraints respected.
6. The tool service extracts dog evidence from public-safe dog fields.
7. The tool service scores and ranks dogs with evidence-based rules and optional semantic search signals.
8. If OpenAI is disabled or unavailable, the backend fallback results are displayed.
9. If OpenAI is enabled and an API key exists, `OpenAiAdoptionCopilotClient` can use function/tool calling.
10. OpenAI may ask PawConnect to run a safe tool such as `search_dogs`.
11. PawConnect executes the tool itself using application services. The AI never queries SQL directly.
12. OpenAI may produce a final structured response with selected dog IDs, labels, tags, caution tags, and short text.
13. The backend validates all final dog IDs against backend-provided candidate IDs.
14. Unknown dog IDs are ignored.
15. The UI shows real database-backed dog cards.

Function/tool calling in simple words:

"The AI does not query the database directly. Instead, it can request a predefined safe tool, like `search_dogs`. PawConnect executes that tool using its own services, applies all business rules, and returns sanitized candidate dog data."

Tools implemented in `OpenAiAdoptionCopilotClient` and executed by `AdoptionCopilotService`:

| Tool | Purpose | Safety rule |
|------|---------|-------------|
| `search_dogs` | Search public-safe dogs using structured filters. | Always applies PawConnect hard filters. |
| `get_adopter_profile_summary` | Return safe current adopter profile summary. | No arbitrary user id; no full name/email/phone/address. |
| `get_favorite_and_recent_preferences` | Return aggregate preference signals. | Only current user, aggregate sizes/breeds/cities. |
| `get_dog_details_public` | Fetch one public-safe dog by id. | Returns only `Available` or `Reserved` dog data. |

Structured Copilot models:

- `AdoptionCopilotResponse`
- `AdoptionCopilotDogResult`
- `AdoptionCopilotConstraint`
- `AdoptionCopilotSearchDogsArgs`
- `CopilotIntent`
- `CopilotDogEvidence`
- `EvidenceItem`
- `AdoptionCopilotToolDogCandidate`
- `AdoptionCopilotDogToolDto`

Hard filters:

- Public-safe statuses are enforced in C#.
- If the user asks for a neighborhood, PawConnect does not silently substitute another neighborhood.
- If the user asks for Medium dogs, the backend enforces Medium dogs.
- If no exact match exists, the UI shows a friendly no-results message instead of violating the constraint.

Public-safe dog data sent to AI can include:

- dog id
- name
- formatted breed
- age text
- size
- status
- public description
- behavior description
- shelter name
- shelter city
- shelter neighborhood
- image URL
- safe reasons/tags/evidence

Private data not sent:

- adopter full name
- adopter email
- adopter phone
- exact address
- shelter internal notes
- private adoption request notes
- passwords
- tokens
- security fields
- SMTP credentials
- audit logs
- raw SQL

Copilot UI:

`AdoptionCopilot.razor` displays:

- natural-language input
- suggested prompt buttons
- assistant summary
- chips such as `AI-assisted explanation`, `Semantic search`, `Used PawConnect data`, or `Rule-based fallback`
- applied constraint chips
- dog result cards with image, name, breed, shelter line, status, score, label, action text, display tags, caution tags, View Details, and Save

State preservation:

- `Services/CopilotStateService.cs` stores the last query, assistant message, constraints, dog IDs, tags, and flags in scoped session state.
- It is not stored in the database.
- Returning from dog details to `/adopter/copilot` can restore the previous search state.

## 20. OpenAI API Integration - Explain in Very High Detail

OpenAI settings are represented by:

- `Services/OpenAiSettings.cs`

Configuration keys:

- `OpenAI:Enabled`
- `OpenAI:ApiKey`
- `OpenAI:Model`
- `OpenAI:ChatModel`
- `OpenAI:EmbeddingModel`

Current committed settings contain model names but no real API key. The API key should be provided through user secrets or environment variables, not committed to source control.

OpenAI clients:

| Use | File | Model/key behavior |
|-----|------|--------------------|
| Recommendations | `Services/OpenAiRecommendationClient.cs` | Uses `OpenAI:Model` or safe default. Requires enabled and API key. |
| Adoption Copilot | `Services/OpenAiAdoptionCopilotClient.cs` | Uses `OpenAI:ChatModel`, Responses API, function/tool calling, strict JSON output. |
| Embeddings | `Services/OpenAiEmbeddingService.cs` | Uses `OpenAI:EmbeddingModel`, default `text-embedding-3-small`. |

### 20.1 OpenAI for Recommendations

The recommendation flow is backend-first:

1. `DogRecommendationService` creates rule-based candidates.
2. `OpenAiRecommendationClient` receives only sanitized adopter profile data and candidate dog data.
3. OpenAI can improve ordering, summary wording, and reasons.
4. `DogRecommendationService` validates returned dog IDs.
5. Unknown dog IDs are ignored.
6. If OpenAI fails, rule-based results remain.

### 20.2 OpenAI for Adoption Copilot

The Copilot uses OpenAI Responses API function/tool calling.

Flow:

```text
User prompt
-> OpenAI receives safe tool definitions
-> model requests search_dogs or another safe tool
-> PawConnect executes the tool
-> PawConnect returns sanitized tool output
-> model writes structured final response
-> backend validates final dog IDs and tags
-> UI renders real DB-backed dog cards
```

Important:

- OpenAI does not access SQL.
- OpenAI does not choose arbitrary users.
- OpenAI cannot bypass public-safe dog filtering.
- The backend can ignore invalid model output.

### 20.3 OpenAI for Embeddings / Semantic Search

Embeddings transform text into numerical vectors, so the system can compare meaning instead of only exact words.

Example:

- Dog text: "enjoys short walks and settles indoors"
- User query: "dog for apartment with low activity"

Even if the dog text does not say "apartment", semantic search can see that the meanings are related.

Files:

- `Services/DogSearchDocumentService.cs`
- `Services/OpenAiEmbeddingService.cs`
- `Services/DogSearchEmbeddingService.cs`
- `Services/SemanticDogSearchService.cs`
- `Entities/DogSearchEmbedding.cs`

Fallback:

If OpenAI is disabled, the API key is missing, embeddings are empty, or the API call fails, PawConnect falls back to deterministic parsing, keyword search, rule-based scoring, and safe UI messages.

Limitations:

- AI can misunderstand ambiguous requests.
- AI does not guarantee compatibility.
- Shelter confirmation is still required.
- Dog descriptions must contain enough useful public information.
- Backend rules are necessary for safety and consistency.

## 21. Semantic Search and Embeddings - Detailed Explanation

Semantic search was added to make natural-language search smarter than exact keyword matching.

Main problem solved:

- Keyword search only matches exact words.
- Semantic search can match meaning.

Example:

```text
Query: "quiet dog for apartment"
Dog text: "settles indoors after short walks"
```

A keyword search might miss this if "quiet" or "apartment" is not present. Semantic search can connect the ideas.

Dog search documents:

`DogSearchDocumentService.BuildDocument(Dog dog)` builds public-safe text from:

- dog name
- formatted age
- size
- formatted breed
- status
- dog location
- shelter name
- shelter neighborhood
- shelter city
- public description
- behavior description
- medical status summary
- food preference

Excluded:

- adopter information
- adoption request notes
- shelter internal notes
- audit logs
- private medical/internal data
- email/password/security data

Embeddings table:

`DogSearchEmbedding` stores:

- `DogId`
- `Content`
- `ContentHash`
- `EmbeddingJson`
- `EmbeddingModel`
- `UpdatedAt`

Content hash:

The content hash prevents unnecessary regeneration. If the public search document did not change, the embedding row is left unchanged.

Refresh behavior:

`DogSearchEmbeddingService` can:

- refresh one dog embedding
- refresh missing embeddings
- refresh all embeddings
- rebuild the dog search index
- remove stale embeddings for dogs that are no longer public-searchable

Admin rebuild:

`Components/Pages/Admin/AdminDogs.razor` includes a "Rebuild Dog Search Index" action. It calls `IDogSearchEmbeddingService.RebuildDogSearchIndexAsync()`.

Public-searchable dogs:

- `Available`
- `Reserved`

Excluded:

- `Adopted`
- `InTreatment`

Similarity:

`OpenAiEmbeddingService.CosineSimilarity(...)` compares two vectors. Higher similarity means the query and dog document are more semantically related.

Fallback:

`SemanticDogSearchService` first tries embedding-based search when possible. If embeddings are unavailable, it uses keyword/rule-based fallback search.

## 22. Evidence, Tags, Scores, and Cautions in Copilot

The Copilot was improved to reason through:

```text
User situation
-> real-life need
-> required evidence
-> dog evidence
-> evidence-based ranking
-> intent-relevant display tags
```

Important models:

- `CopilotIntent`
- `CopilotDogEvidence`
- `EvidenceItem`
- `AdoptionCopilotToolDogCandidate`

Evidence types:

| Evidence type | Meaning | Example |
|---------------|---------|---------|
| Direct | Strong evidence for the exact user need. | "Calm near cats" for a cat query. |
| Indirect | Helpful but not enough alone. | "Settles quickly" for a sensitive dog query. |
| Generic | Positive but vague. | "Friendly dog." |
| Caution | Useful warning or uncertainty. | "Needs slow dog introductions." |
| Missing | Important evidence not found. | "No cat history found." |

Why this matters:

- A dog with "Ask shelter about cats" should not be `Excellent` for a cat query.
- A dog with direct cat evidence can score higher for "I have a cat at home."
- "Indoor rest" is relevant for apartment/low-activity queries, but not as a main tag for cat queries.

Examples:

Query: `I have a cat at home`

Relevant tags:

- Calm near cats
- Redirectable around cats
- Needs slow cat introductions
- Ask shelter about cats
- No cat history found

Not relevant unless also requested:

- Indoor rest
- Short walks
- Settles quickly

Query: `I live in an apartment`

Relevant tags:

- Short walks
- Indoor rest
- Settles quickly
- Quiet routine
- Small size
- Medium size

Query: `I have a sick dog recovering at home`

Relevant tags:

- Calm dog company
- Respectful around dogs
- Needs slow dog introductions
- Not too energetic
- Ask shelter about sensitive dog fit

Score caps:

- Direct evidence, no major caution: up to about 96.
- Direct evidence plus caution: usually lower, around Good match.
- Only indirect evidence: not Excellent.
- Generic evidence only: low confidence.
- Missing evidence or Ask shelter tags: cannot be Excellent.

Presentation sentence:
"The Copilot does not just show tags from the user query. Tags must be backed by public dog data, and uncertain evidence lowers confidence."

## 23. UI/UX Design

PawConnect uses MudBlazor and a consistent soft card-based style.

Important UI files:

- `Components/Layout/MainLayout.razor`
- `Components/Layout/NavMenu.razor`
- `Components/Shared/NotificationBell.razor`
- `Components/Pages/Dogs.razor`
- `Components/Pages/DogDetails.razor`
- `Components/Pages/Adopter/AdoptionCopilot.razor`
- `Components/Pages/Adopter/Recommendations.razor`
- `Components/Pages/Shelter/ManageDogs.razor`
- `Components/Pages/Admin/AdminDogs.razor`

UI patterns:

- cards for dogs, requests, resources, dashboards
- tables for admin/shelter management pages
- dialogs for details and history
- chips for statuses, filters, tags, and match labels
- snackbars for success/error feedback
- role-based nav items in `NavMenu.razor`

Visual style:

- green/teal primary color
- orange accent for warnings/reserved/favorites
- rounded cards
- soft background
- compact chips
- clean dashboard sections

## 24. Validation and Error Handling

Validation happens at multiple layers:

- data annotations on entities
- form validation in Razor components
- service-level business validation
- database indexes and relationships
- ownership checks
- OpenAI output validation

Examples:

- Duplicate active adoption requests are blocked by service logic and a filtered database index.
- Duplicate favorites are blocked by a unique index.
- A shelter cannot edit another shelter's dogs.
- Public users cannot see `Adopted` or `InTreatment` dogs in public/adopter searches.
- Invalid visit times are rejected.
- CSV import preview reports row errors before import.
- Missing OpenAI key does not break recommendations or Copilot.
- Unknown OpenAI dog IDs are ignored.

Non-blocking failures:

- Email failure is logged.
- PDF/report failure is logged.
- Embedding refresh failure is logged.
- These should not destroy the primary business action.

## 25. Testing

Test project:

- `PawConnect.Tests/PawConnect.Tests.csproj`

Frameworks/libraries:

- xUnit
- EF Core InMemory provider
- fake/test services in `PawConnect.Tests/Tests/Helpers`

Important test helpers:

- `PawConnect.Tests/Tests/Helpers/TestDbContextFactory.cs`
- `PawConnect.Tests/Tests/Helpers/TestDoubles.cs`

Test areas:

- dog visibility and dog service behavior
- dog breed formatting and breed information
- favorites
- adoption request flow
- visit confirmation and reminders
- dog status history
- resource stock
- shelter registration
- CSV import/export
- PDF/email
- notifications
- report history
- audit logs
- dog recommendations
- Copilot public-safe filtering
- OpenAI fallback
- unknown dog ID validation
- sanitized OpenAI DTOs
- semantic search fallback
- Nominatim geocoding parsing
- local return URL safety

Integration-style service flow tests:

- `PawConnect.Tests/Tests/Integration/ServiceFlowIntegrationTests.cs`

Run tests:

```bash
dotnet test
```

## 26. Most Important Files to Know for Presentation

| File | Why it matters | What I should say if asked |
|------|----------------|----------------------------|
| `Program.cs` | Startup, dependency injection, authentication, services, jobs. | "This wires the whole application together." |
| `Data/ApplicationDbContext.cs` | EF Core database model and relationships. | "This is the bridge between C# entities and SQL Server tables." |
| `Data/ApplicationUser.cs` | Custom Identity user. | "Identity users are extended with PawConnect relationships." |
| `Data/IdentitySeedData.cs` | Seeds roles, demo users, shelters, dogs, lookups. | "This creates demo data for development and presentation." |
| `Data/DogBreedSeedData.cs` | Seeds dog breed lookup values and breed notes. | "Breed data is centralized and reusable." |
| `Entities/Dog.cs` | Main dog domain model. | "This is the central adoption entity." |
| `Entities/DogBreed.cs` | Breed lookup and breed information fields. | "This replaces inconsistent free-text breed handling." |
| `Entities/Shelter.cs` | Shelter profile and location. | "Shelters own dogs and resources." |
| `Entities/AdoptionRequest.cs` | Adoption request and visit workflow. | "This records adopter interest and shelter decisions." |
| `Entities/DogSearchEmbedding.cs` | Semantic search vector storage. | "This stores embedding data for natural language dog search." |
| `Services/DogService.cs` | Dog CRUD, search, status history, visibility rules. | "Public-safe dog filtering is enforced here." |
| `Services/AdoptionRequestService.cs` | Request lifecycle and dog status transitions. | "This protects the adoption workflow." |
| `Services/DogRecommendationService.cs` | Personalized recommendation scoring. | "Recommendations work even without AI." |
| `Services/OpenAiRecommendationClient.cs` | Optional recommendation OpenAI enhancement. | "OpenAI can polish backend-selected recommendations." |
| `Services/AdoptionCopilotService.cs` | Main Copilot orchestration. | "This coordinates parsing, tools, fallback, and validation." |
| `Services/AdoptionCopilotToolService.cs` | Safe Copilot tools, evidence extraction, scoring. | "The AI requests tools, but this service chooses real candidates safely." |
| `Services/OpenAiAdoptionCopilotClient.cs` | OpenAI Responses API tool calling. | "This defines safe tools and strict structured output." |
| `Services/SemanticDogSearchService.cs` | Semantic or keyword/rule-based dog search. | "This ranks dogs by meaning when embeddings are available." |
| `Services/DogSearchDocumentService.cs` | Public-safe dog search documents. | "Only safe dog text is embedded." |
| `Services/DogSearchEmbeddingService.cs` | Embedding refresh/rebuild. | "This maintains the semantic search index." |
| `Services/OpenAiEmbeddingService.cs` | OpenAI embedding API and cosine similarity. | "This converts text to vectors for semantic search." |
| `Services/OpenAiSettings.cs` | AI configuration. | "OpenAI is optional and model names are configurable." |
| `Services/SmtpEmailService.cs` | SMTP email sending. | "Emails are generated through a service, not inside pages." |
| `Services/PdfReportService.cs` | QuestPDF report generation. | "PDF reports are generated server-side." |
| `Services/CsvImportService.cs` | CSV preview/import. | "Imports validate rows before applying changes." |
| `Services/ExportService.cs` | CSV/PDF export generation. | "Exports are role-aware and use formatted values." |
| `Components/Pages/Dogs.razor` | Public dog browsing UI. | "This is the main public discovery page." |
| `Components/Pages/DogDetails.razor` | Dog profile page. | "This shows dog details, breed info, shelter, food, medical records, and actions." |
| `Components/Pages/Adopter/Recommendations.razor` | Recommendation UI. | "This displays personalized match scores and reasons." |
| `Components/Pages/Adopter/AdoptionCopilot.razor` | Copilot UI. | "This is the natural-language dog search experience." |
| `Components/Pages/Adopter/MyAdoptionRequests.razor` | Adopter request tracking. | "Adopters can follow adoption progress." |
| `Components/Pages/Shelter/ManageDogs.razor` | Shelter dog management. | "Shelters manage their own dogs here." |
| `Components/Pages/Shelter/ShelterAdoptionRequests.razor` | Shelter request workflow. | "Shelters confirm visits and finalize adoption here." |
| `Components/Pages/Admin/AdminDogs.razor` | Admin dogs and search index rebuild. | "Admin can supervise dogs and rebuild embeddings." |
| `Components/Pages/Admin/AdminAdoptionRequests.razor` | Admin adoption request overview. | "Admin can inspect request activity across shelters." |
| `Components/Shared/AdoptionRequestDetailsDialog.razor` | Request details dialog. | "Shared dialog for admin/shelter request detail views." |
| `Components/Shared/DogStatusHistoryDialog.razor` | Dog status history dialog. | "Shows status transitions and notes." |

## 27. Database Tables to Know

| Table/entity | Purpose | Key columns | Related tables | Presentation explanation |
|--------------|---------|-------------|----------------|--------------------------|
| `AspNetUsers` / `ApplicationUser` | User accounts. | `Id`, `Email`, `FullName`. | Roles, profiles, requests, notifications. | "Identity handles authentication and role users." |
| `Shelters` | Shelter profiles. | `Name`, `City`, `Neighborhood`, `Latitude`, `Longitude`. | Dogs, resources, shelter user. | "Shelters own dogs and resources." |
| `Dogs` | Dog records. | `Name`, `DogBreedId`, `Size`, `Status`, `ShelterId`. | Shelter, images, medical records, requests. | "Central public adoption data." |
| `DogBreeds` | Breed lookup. | `Name`, `IsActive`, breed notes. | Dogs. | "Consistent breed handling and breed info." |
| `DogImages` | Dog photos. | `DogId`, `ImageUrl`, `IsMainImage`. | Dogs. | "Public visual identity for dogs." |
| `MedicalRecords` | Dog medical history. | `DogId`, `RecordDate`, `TreatmentDescription`. | Dogs. | "Actual medical records, separate from breed education." |
| `AdoptionRequests` | Adoption workflow. | `DogId`, `AdopterId`, `Status`, `VisitStatus`. | Dogs, users. | "Tracks request and visit lifecycle." |
| `FavoriteDogs` | Saved dogs. | `AdopterId`, `DogId`. | Users, dogs. | "Adopter preference signal." |
| `RecentlyViewedDogs` | Recently viewed dogs. | `AdopterId`, `DogId`, `ViewedAt`. | Users, dogs. | "Another preference signal for recommendations." |
| `AdopterProfiles` | Adopter home/profile data. | `City`, `HousingType`, `HasYard`, `HasChildren`. | Users. | "Used for recommendations and safe personalization." |
| `DogStatusHistories` | Status transition audit. | `DogId`, `OldStatus`, `NewStatus`, `ChangedAt`. | Dogs, users. | "Explains how dog status changed over time." |
| `Notifications` | User notifications. | `UserId`, `Title`, `Message`, `IsRead`. | Users. | "In-app feedback." |
| `AuditLogs` | Platform activity. | `Action`, `EntityName`, `UserId`, `CreatedAt`. | Users indirectly. | "Admin traceability." |
| `ReportHistories` | Report generation history. | `ReportType`, `WasSuccessful`, `GeneratedAt`. | Shelters optionally. | "Shows what reports were produced." |
| `ResourceStocks` | Shelter supplies. | `ShelterId`, `Quantity`, `LowStockThreshold`. | Shelter, category, food type. | "Inventory tracking." |
| `ResourceCategories` | Resource category lookup. | `Name`, `IsActive`. | Resource stock. | "Keeps inventory categories consistent." |
| `FoodTypes` | Food lookup. | `Name`, `IsActive`. | Dogs, resources. | "Used by food preferences and stock." |
| `ShelterRegistrationRequests` | Shelter applications. | `ShelterName`, `Email`, `Status`. | Created shelter/user after approval. | "Public application to become a shelter." |
| `DogSearchEmbeddings` | Semantic dog search index. | `DogId`, `ContentHash`, `EmbeddingJson`. | Dogs. | "Stores vectors for natural language search." |

## 28. Questions the Committee Might Ask

### General Questions

**Why did you use Blazor Server?**  
Because it lets me build an interactive web UI with C# and Razor components while keeping most logic on the server.

**Why did you use EF Core?**  
EF Core maps C# entities to SQL Server tables and gives migrations, relationships, queries, and tracking.

**How is authentication handled?**  
ASP.NET Core Identity handles users, passwords, cookies, roles, and account pages. PawConnect adds roles: Adopter, Shelter, and Admin.

**How do you prevent shelters from editing each other's dogs?**  
Shelter service methods use the current shelter/user context and check `ShelterId` ownership before editing.

**How do public users only see available dogs?**  
Public search methods filter dogs to `Available` and `Reserved`, excluding `Adopted` and `InTreatment`.

**How are adoption requests processed?**  
The adopter submits a request, the shelter confirms a visit, the dog becomes reserved, and after the visit the shelter can mark the adoption completed.

**How are emails and PDFs generated?**  
Services generate PDFs with QuestPDF and send email through MailKit/MimeKit. Failures are logged and do not break the main action.

**How do scheduled jobs work?**  
Quartz.NET runs background jobs for visit reminders and shelter summaries based on settings.

**What did you test?**  
Services, imports/exports, recommendations, Copilot safety, public-safe filters, adoption flows, notifications, reports, geocoding, and integration-style flows.

**What would you improve in the future?**  
More structured dog compatibility fields, stronger admin analytics, richer shelter onboarding, and production-grade deployment monitoring.

### AI-Specific Questions

**Why did you add an AI Copilot?**  
Normal filters are rigid. The Copilot lets adopters describe real-life situations in natural language, such as having a cat or a recovering dog.

**Does the AI access the database directly?**  
No. It can only request predefined application tools, and PawConnect executes those tools safely.

**Can the AI invent dogs?**  
The model can output bad IDs, but the backend validates all IDs against backend candidates and ignores unknown IDs.

**What happens when OpenAI is unavailable?**  
The app uses deterministic parsing, keyword/rule-based scoring, semantic fallback where available, and still returns safe results.

**What private data is sent to OpenAI?**  
Only sanitized data is sent. Full name, email, phone, exact address, internal notes, passwords, tokens, audit logs, and SMTP settings are excluded.

**What are embeddings?**  
Embeddings are numerical vectors representing text meaning. They help match queries to dog descriptions by meaning rather than exact words.

**Why semantic search instead of only keyword search?**  
Semantic search can match "low activity apartment dog" with dog text like "short walks and indoor rest."

**How do recommendations differ from Copilot suggestions?**  
Recommendations are profile-based and proactive. Copilot is query-based and interactive.

**How are AI recommendations explained?**  
The UI shows scores, match labels, short explanations, display tags, caution tags, and applied constraints.

**What are AI limitations?**  
AI can misunderstand ambiguous text and cannot guarantee compatibility. Shelter confirmation remains important.

## 29. Presentation Cheat Sheet

### 10 Key Sentences

1. PawConnect is a role-based web application for dog adoption and shelter management.
2. Public users can browse only public-safe dogs.
3. Shelters manage their own dogs, resources, and adoption requests.
4. Admins supervise users, shelters, dogs, reports, and platform activity.
5. EF Core models the database through entities and migrations.
6. Identity provides login, roles, and authorization.
7. Adoption requests follow a controlled workflow from pending to visit confirmed to adopted.
8. Recommendations work with rule-based scoring and optional OpenAI enhancement.
9. The Adoption Copilot uses natural language but the backend remains the source of truth.
10. OpenAI is optional, sanitized, validated, and safely falls back when unavailable.

### 10 Important Technical Terms

1. Blazor Server: interactive UI rendered from server-side C# components.
2. Entity: a C# class mapped to a database table.
3. DbContext: EF Core class that manages database access.
4. Migration: versioned database schema change.
5. Identity: ASP.NET system for users, roles, and authentication.
6. Service layer: classes that contain business logic outside UI pages.
7. Public-safe filtering: showing only dog data allowed for public/adopter views.
8. Embedding: numerical representation of text meaning.
9. Function/tool calling: AI requests a safe predefined app function instead of accessing data directly.
10. Fallback: non-AI behavior used when OpenAI is disabled or fails.

### 10 Most Important Files

1. `Program.cs`
2. `Data/ApplicationDbContext.cs`
3. `Entities/Dog.cs`
4. `Entities/AdoptionRequest.cs`
5. `Services/DogService.cs`
6. `Services/AdoptionRequestService.cs`
7. `Services/DogRecommendationService.cs`
8. `Services/AdoptionCopilotService.cs`
9. `Services/AdoptionCopilotToolService.cs`
10. `Components/Pages/Adopter/AdoptionCopilot.razor`

### 10 Most Important Tables

1. Users / `ApplicationUser`
2. Shelters
3. Dogs
4. DogBreeds
5. AdoptionRequests
6. FavoriteDogs
7. AdopterProfiles
8. DogStatusHistories
9. Notifications
10. DogSearchEmbeddings

### 5 Most Important Flows

1. Public dog browsing and dog details.
2. Adopter profile, favorites, and recommendations.
3. Adoption request submission, visit confirmation, and adoption completion.
4. Shelter dog/resource management.
5. Adoption Copilot natural-language search with safe backend validation.

### 5 Strongest Thesis Contributions

1. Complete role-based adoption management workflow.
2. Personalized dog recommendations with explainable scores.
3. AI Adoption Copilot with safe function/tool calling.
4. Semantic dog search with embeddings and fallback behavior.
5. Reporting, notifications, audit logs, and operational shelter tools.

### 5 Key Points About the AI Copilot

1. It accepts natural-language adopter queries.
2. It extracts structured intent and constraints.
3. It retrieves only real PawConnect dogs.
4. It validates OpenAI output against backend candidates.
5. It explains results with tags, cautions, scores, and labels.

### 5 Key Points About OpenAI Safety/Fallback

1. OpenAI is optional and requires an API key.
2. The AI never runs SQL or accesses the database directly.
3. Sensitive adopter and shelter data is excluded.
4. Unknown dog IDs are ignored.
5. Rule-based/keyword fallback still works without OpenAI.

### 5 Key Points About Recommendations

1. Recommendations are rule-based first.
2. Adopter profile data affects scoring.
3. Favorites and recently viewed dogs add preference signals.
4. Match scores and labels are explainable.
5. OpenAI can improve explanations but cannot add non-candidate dogs.
