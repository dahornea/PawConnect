# PawConnect Project Overview

## Project Purpose

PawConnect is a C# ASP.NET Core Blazor Server web application for stray dog adoption and shelter management. It connects public visitors, adopters, shelters, and administrators in one platform.

The main problem it solves is that adoption information is usually scattered: dogs are listed in one place, adopter requests are managed elsewhere, shelters track resources manually, and adopters often do not know which dog fits their living situation. PawConnect centralizes these workflows and adds recommendation/search support to make dog discovery more meaningful.

Important project files:

| File | Why it matters |
| --- | --- |
| `Program.cs` | Configures the application, services, authentication, OpenAI clients, EF Core, MudBlazor, and Quartz jobs. |
| `Data/ApplicationDbContext.cs` | Defines the database model and relationships. |
| `Components/Pages/Dogs.razor` | Public dog listing and filtering page. |
| `Components/Pages/DogDetails.razor` | Public dog profile, adoption request entry point, gallery, breed information, and role-aware actions. |
| `Components/Pages/Adopter/AdoptionCopilot.razor` | Natural-language Adoption Copilot UI. |
| `Components/Pages/Adopter/Recommendations.razor` | Personalized recommended dogs page. |
| `Components/Pages/Shelter/ManageDogs.razor` | Shelter dog management page. |
| `Components/Pages/Shelter/ShelterAdoptionRequests.razor` | Shelter adoption request workflow. |
| `Components/Pages/Admin/AdminDashboard.razor` | Admin entry point for platform oversight. |

## Main Users and Roles

| Role | What the role can do | Key files |
| --- | --- | --- |
| Public visitor | Browse dogs, view dog details, view shelters, view success stories, submit shelter applications. | `Components/Pages/Dogs.razor`, `Components/Pages/DogDetails.razor`, `Components/Pages/Shelters.razor`, `Components/Pages/ApplyForShelter.razor` |
| Adopter | Manage profile, save favorite dogs, view recommendations, use Adoption Copilot, submit/cancel adoption requests, receive notifications. | `Components/Pages/Adopter/MyAdopterProfile.razor`, `Components/Pages/Adopter/Recommendations.razor`, `Components/Pages/Adopter/AdoptionCopilot.razor`, `Components/Pages/Adopter/MyAdoptionRequests.razor` |
| Shelter representative | Manage shelter dogs, images, medical records, resources, adoption requests, visit confirmations, and adoption completion. | `Components/Pages/Shelter/ManageDogs.razor`, `CreateDog.razor`, `EditDog.razor`, `Resources.razor`, `ShelterAdoptionRequests.razor` |
| Administrator | Manage users, shelters, dogs, adoption requests, shelter applications, reports, audit logs, and dog search index rebuild. | `Components/Pages/Admin/*` |

Roles are seeded in `Data/IdentitySeedData.cs` using:

- `Adopter`
- `Shelter`
- `Admin`

## Main Features

| Feature | Summary | Important code |
| --- | --- | --- |
| Public dog browsing | Shows only public-safe dogs, with filters for search, breed, size, age, shelter, neighborhood, status, and coat color. | `Components/Pages/Dogs.razor`, `Services/DogService.cs` |
| Dog details | Shows dog profile, images/gallery, breed information, shelter, food, medical records, adoption request form, and role-aware actions. | `Components/Pages/DogDetails.razor` |
| Dog breed system | Uses `DogBreed` lookup with mixed breed, secondary breed, custom breed, and breed information notes. | `Entities/DogBreed.cs`, `Services/DogBreedFormatter.cs`, `Services/DogBreedInformationFormatter.cs` |
| Adoption request workflow | Adopter submits request, shelter confirms visit, dog becomes reserved, shelter can finalize adoption. | `Entities/AdoptionRequest.cs`, `Services/AdoptionRequestService.cs` |
| Recommended Dogs | Uses adopter profile, favorites, recently viewed dogs, and optional OpenAI enhancement to rank dogs. | `Services/DogRecommendationService.cs`, `Services/OpenAiRecommendationClient.cs` |
| Adoption Copilot | Lets adopters type natural-language adoption needs and returns evidence-backed dog suggestions. | `Services/AdoptionCopilotService.cs`, `Services/AdoptionCopilotToolService.cs`, `Services/OpenAiAdoptionCopilotClient.cs` |
| Semantic dog search | Uses dog search documents, embeddings, cosine similarity, and fallback keyword/rule search. | `Services/SemanticDogSearchService.cs`, `Services/DogSearchDocumentService.cs`, `Services/DogSearchEmbeddingService.cs`, `Entities/DogSearchEmbedding.cs` |
| Resource stock | Shelters track stock, categories, food types, quantity, thresholds, and low-stock notifications/reports. | `Entities/ResourceStock.cs`, `Services/ResourceStockService.cs`, `Components/Pages/Shelter/Resources.razor` |
| Email/PDF/reporting | Sends emails with PDF attachments and records report history. | `Services/SmtpEmailService.cs`, `Services/PdfReportService.cs`, `Services/ReportHistoryService.cs` |
| Notifications and audit logs | In-app notifications and admin traceability for important operations. | `Services/NotificationService.cs`, `Services/AuditLogService.cs` |
| Scheduled jobs | Quartz jobs send shelter summary reports and visit reminders. | `Jobs/ShelterSummaryReportJob.cs`, `Jobs/VisitReminderJob.cs` |
| CSV import/export | Supports importing/exporting resources, dogs, adoption requests, shelters, and reports. | `Services/CsvImportService.cs`, `Services/ExportService.cs` |

## Technical Stack

| Technology | Purpose in PawConnect | Files |
| --- | --- | --- |
| C# / .NET 10 | Main programming language and runtime. | `PawConnect.csproj` |
| ASP.NET Core | Web app hosting, dependency injection, authentication, Identity endpoints. | `Program.cs` |
| Blazor Server | Interactive UI rendered through server-side components. | `Components/**/*.razor` |
| Entity Framework Core | Database access and migrations. | `Data/ApplicationDbContext.cs`, `Migrations/*` |
| SQL Server | Main relational database provider. | `Program.cs`, `appsettings.json` |
| ASP.NET Core Identity | Authentication, users, roles, cookies. | `Data/ApplicationUser.cs`, `Data/IdentitySeedData.cs`, `Components/Account/*` |
| MudBlazor | UI components: cards, tables, dialogs, chips, buttons, forms. | Most `.razor` pages |
| MailKit / MimeKit | SMTP email sending. | `Services/SmtpEmailService.cs`, `Services/EmailMimeBuilder.cs` |
| QuestPDF | PDF report generation. | `Services/PdfReportService.cs` |
| Quartz.NET | Scheduled background jobs. | `Program.cs`, `Jobs/*` |
| OpenAI API | Optional recommendation enhancement, Copilot tool calling, embeddings. | `Services/OpenAi*.cs`, `Services/OpenAiSettings.cs` |
| xUnit | Automated tests. | `PawConnect.Tests/*` |

## High-Level Architecture

PawConnect uses a layered server-side architecture:

1. Blazor components render pages and handle user events.
2. Components call injected services directly through dependency injection.
3. Services contain business logic, validation, ownership checks, and workflow rules.
4. Services use `ApplicationDbContext` and EF Core to query and update SQL Server.
5. External integrations are isolated behind services: email, PDF, OpenAI, geocoding, scheduled jobs.

This is not a REST API-first application. Core flows happen inside the Blazor Server app, so the UI and backend services run in the same ASP.NET Core process.

## Original Contribution

The strongest original parts are:

- Adoption workflow adapted to shelters: request, preferred visit, confirmation, reservation, final adoption.
- Role-based management for public users, adopters, shelters, and admins.
- AI-assisted Adoption Copilot that connects natural-language adopter needs to real public dog data.
- Evidence-based Copilot pipeline with deterministic parsing, public-safe candidate retrieval, semantic search, scoring, display tags, and OpenAI validation.
- Dog recommendations combining adopter profile, favorites, recently viewed dogs, public-safe filters, rule-based scoring, and optional OpenAI enhancement.
- Operational shelter tools: resources, low-stock reports, PDF/email notifications, audit logs, report history.
- A large automated test suite with 281 tests covering service logic, AI fallback, filters, CSV import/export, adoption flows, and edge cases.

## What to Emphasize During the Thesis Defense

Focus on these points:

1. PawConnect is a workflow system, not just a CRUD app.
2. The domain model connects users, shelters, dogs, adoption requests, resources, notifications, reports, and AI search data.
3. The AI is optional and controlled by backend rules.
4. OpenAI cannot invent dogs because the backend validates all dog IDs against real database candidates.
5. Public-safe filtering prevents adopted or in-treatment dogs from appearing in public Copilot/search results by default.
6. Private adopter/shelter data is not sent to OpenAI in the Copilot candidate DTOs.
7. The application still works when OpenAI is disabled or unavailable.
8. The test suite is large because the project has many role, status, validation, and AI fallback combinations.
9. EF Core migrations, Identity roles, and service-level ownership checks make the implementation structured.
10. The project has realistic future improvements, but the current version already demonstrates a complete full-stack thesis application.
