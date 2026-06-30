# PawConnect Context Pack for a New ChatGPT Conversation

Use this file as a handoff document if the original ChatGPT conversation that helped with the thesis, presentation, or demo is unavailable.

The goal is to let a new assistant quickly understand the PawConnect thesis project, the application features, the implementation style, the demo setup, and the important topics to preserve when writing thesis sections or presentation material.

Important instruction for the new assistant:

- Do not invent PawConnect functionality.
- Use real files, routes, classes, services, entities, and tests from this repository.
- If something is unclear, say it is unclear instead of guessing.
- Treat this document as context, then inspect the repository files when precision is needed.
- The AI/Copilot described here is an application feature. Do not confuse it with AI assistance used outside the application.

---

## 1. Project Summary

PawConnect is a C# ASP.NET Core Blazor Server web application for stray dog adoption and shelter management.

It supports:

- public dog discovery;
- adopter accounts and adopter profiles;
- shelter-side dog and adoption request management;
- admin platform supervision;
- adoption request workflow with visit scheduling;
- dog images, breed information, medical records, food information, and status history;
- favorites and recently viewed dogs;
- in-app notifications;
- email notifications and PDF reports;
- CSV import/export;
- resource stock management;
- shelter maps using Leaflet/OpenStreetMap;
- dog recommendations;
- an Adoption Copilot that supports natural-language dog search with optional OpenAI integration and safe fallback behavior.

The project is intended for a Computer Science bachelor thesis. It should be presented as more than a CRUD app because it combines role-based workflows, business rules, validation, reporting, scheduled jobs, maps, AI-assisted search, semantic search, and a large service-level test suite.

---

## 2. Technology Stack

| Technology | Role in PawConnect | Important files |
|---|---|---|
| C# | Main programming language. | Most `.cs` files |
| ASP.NET Core | Web framework, dependency injection, authentication pipeline. | `Program.cs` |
| Blazor Server | Interactive UI using Razor components and SignalR. | `Components/Pages/**/*.razor` |
| Entity Framework Core | Data access and migrations. | `Data/ApplicationDbContext.cs`, `Data/Migrations/` |
| SQL Server / LocalDB | Relational database for local/demo use. | `appsettings.json` connection string |
| ASP.NET Core Identity | Users, roles, login/register/account management. | `Data/ApplicationUser.cs`, `Components/Account/` |
| MudBlazor | UI components: cards, dialogs, forms, tables, chips, buttons. | Razor components |
| Quartz.NET | Scheduled background jobs. | `Jobs/VisitReminderJob.cs`, `Jobs/ShelterSummaryReportJob.cs` |
| MailKit/MimeKit style email flow | SMTP emails and attachments. | `Services/SmtpEmailService.cs`, `Services/EmailMimeBuilder.cs` |
| QuestPDF | PDF generation. | `Services/PdfReportService.cs` |
| Leaflet/OpenStreetMap | Shelter map display and location UI. | `Components/Shared/ShelterMap.razor` |
| OpenAI Responses API | Optional Copilot/recommendation/embedding support. | `Services/OpenAiAdoptionCopilotClient.cs`, `Services/OpenAiRecommendationClient.cs`, `Services/OpenAiEmbeddingService.cs` |
| xUnit + EF Core InMemory | Automated service and integration-style tests. | `PawConnect.Tests/` |

---

## 3. Main User Roles

### Public visitor

Can:

- browse dogs at `/dogs`;
- view dog details at `/dogs/{id}`;
- view shelters at `/shelters`;
- view shelter details at `/shelters/{id}`;
- view success stories at `/success-stories`;
- submit a shelter application at `/shelters/apply`;
- register/login.

### Adopter

Can:

- use adopter dashboard at `/adopter/dashboard`;
- edit adopter profile at `/adopter/profile`;
- favorite dogs at `/favorites`;
- view recently viewed dogs;
- view recommendations at `/adopter/recommendations`;
- use Adoption Copilot at `/adopter/copilot`;
- submit adoption requests from dog details;
- track own adoption requests at `/my-adoption-requests`;
- receive notifications.

### Shelter representative

Can:

- use shelter dashboard at `/shelter/dashboard`;
- manage own dogs at `/shelter/dogs`;
- create dogs at `/shelter/dogs/create`;
- edit dogs at `/shelter/dogs/edit/{id}`;
- add/edit dog images and medical records;
- review own shelter's adoption requests at `/shelter/adoption-requests`;
- confirm visits;
- reject/cancel/finalize requests according to service rules;
- manage resources at `/shelter/resources`;
- export/import supported shelter data;
- receive notifications and reports.

### Admin

Can:

- use admin dashboard at `/admin/dashboard`;
- manage users at `/admin/users`;
- manage shelters at `/admin/shelters`;
- view all dogs at `/admin/dogs`;
- view all adoption requests at `/admin/adoption-requests`;
- review shelter applications at `/admin/shelter-requests`;
- view activity logs at `/admin/activity-log`;
- view report history at `/admin/report-history`;
- rebuild dog search embeddings from `/admin/dogs` if OpenAI embeddings are configured.

---

## 4. Demo Accounts

Source: `docs/PawConnect_Demo_Accounts.md` and `Data/IdentitySeedData.cs`.

| Role | Email | Password | Demo purpose |
|---|---|---|---|
| Admin | `admin@mail.com` | `Admin1!` | Admin dashboard, adoption requests, dogs, reports, logs. |
| Adopter | `adopter@mail.com` | `Adopter1!` | Dashboard, profile, Copilot, recommendations, favorites, request tracking. |
| Shelter | `shelter@mail.com` | `Shelter1!` | Happy Paws Shelter dog/request/resource management. |

The adopter is seeded as Ana Ionescu in Cluj-Napoca. Her profile intentionally supports demo scenarios:

- apartment;
- no yard;
- has other pets;
- one older/recovering dog at home;
- moderate dog experience;
- prefers a small or medium calm companion.

The shelter account is linked to Happy Paws Shelter in Zorilor, Cluj-Napoca.

---

## 5. Main Application Routes

| Area | Route | Component |
|---|---|---|
| Home | `/` | `Components/Pages/Home.razor` |
| Public dogs | `/dogs` | `Components/Pages/Dogs.razor` |
| Dog details | `/dogs/{Id:int}` | `Components/Pages/DogDetails.razor` |
| Shelters | `/shelters` | `Components/Pages/Shelters.razor` |
| Shelter details | `/shelters/{Id:int}` | `Components/Pages/ShelterDetails.razor` |
| Shelter application | `/shelters/apply` | `Components/Pages/ShelterApply.razor` |
| Success stories | `/success-stories` | `Components/Pages/SuccessStories.razor` |
| Notifications | `/notifications` | `Components/Pages/Notifications.razor` |
| Adopter dashboard | `/adopter/dashboard` | `Components/Pages/Adopter/AdopterDashboard.razor` |
| Adopter profile | `/adopter/profile` | `Components/Pages/Adopter/MyAdopterProfile.razor` |
| Recommendations | `/adopter/recommendations` | `Components/Pages/Adopter/Recommendations.razor` |
| Adoption Copilot | `/adopter/copilot` | `Components/Pages/Adopter/AdoptionCopilot.razor` |
| Favorites | `/favorites` | `Components/Pages/Adopter/Favorites.razor` |
| My adoption requests | `/my-adoption-requests` | `Components/Pages/Adopter/MyAdoptionRequests.razor` |
| Shelter dashboard | `/shelter/dashboard` | `Components/Pages/Shelter/ShelterDashboard.razor` |
| Shelter dogs | `/shelter/dogs` | `Components/Pages/Shelter/ManageDogs.razor` |
| Create dog | `/shelter/dogs/create` | `Components/Pages/Shelter/CreateDog.razor` |
| Edit dog | `/shelter/dogs/edit/{Id:int}` | `Components/Pages/Shelter/EditDog.razor` |
| Shelter adoption requests | `/shelter/adoption-requests` | `Components/Pages/Shelter/ShelterAdoptionRequests.razor` |
| Shelter resources | `/shelter/resources` | `Components/Pages/Shelter/Resources.razor` |
| Admin dashboard | `/admin/dashboard` | `Components/Pages/Admin/AdminDashboard.razor` |
| Admin users | `/admin/users` | `Components/Pages/Admin/AdminUsers.razor` |
| Admin shelters | `/admin/shelters` | `Components/Pages/Admin/AdminShelters.razor` |
| Admin dogs | `/admin/dogs` | `Components/Pages/Admin/AdminDogs.razor` |
| Admin adoption requests | `/admin/adoption-requests` | `Components/Pages/Admin/AdminAdoptionRequests.razor` |
| Admin shelter requests | `/admin/shelter-requests` | `Components/Pages/Admin/AdminShelterRequests.razor` |
| Admin report history | `/admin/report-history` | `Components/Pages/Admin/AdminReportHistory.razor` |
| Admin activity log | `/admin/activity-log` | `Components/Pages/Admin/AdminActivityLog.razor` |

---

## 6. Codebase Structure

| Folder/File | Purpose |
|---|---|
| `Program.cs` | App startup, dependency injection, Identity, EF Core, MudBlazor, Quartz, HTTP clients, seeding. |
| `Data/ApplicationDbContext.cs` | EF Core DbContext, DbSets, relationships, indexes, delete behavior. |
| `Data/ApplicationUser.cs` | Identity user extension. |
| `Data/IdentitySeedData.cs` | Demo users, shelters, dogs, resources, medical records, adoption requests. |
| `Data/DogBreedSeedData.cs` | Dog breed lookup data and breed notes. |
| `Entities/` | Domain entities and enums. |
| `Services/` | Business logic, validation, workflows, AI clients, reports, imports/exports. |
| `Components/Pages/` | Blazor page components. |
| `Components/Shared/` | Reusable UI components/dialogs: maps, notifications, image preview, details dialogs. |
| `Components/Layout/` | Main layout and navigation. |
| `Jobs/` | Quartz scheduled jobs. |
| `Repositories/` | Generic repository abstraction, mostly secondary to service layer. |
| `PawConnect.Tests/` | xUnit tests using EF Core InMemory and fake services. |
| `docs/` | Study guides, demo plans, test explanations, diagrams, thesis-defense notes. |

Key architectural point:

The Blazor pages do not contain all business logic. They call services such as `DogService`, `AdoptionRequestService`, `ResourceStockService`, `DogRecommendationService`, and `AdoptionCopilotService`. This is important for security and testing because service-level rules cannot be bypassed by UI changes.

---

## 7. Important Entities

Defined mostly in `Entities/` and configured in `Data/ApplicationDbContext.cs`.

| Entity | Purpose |
|---|---|
| `ApplicationUser` | Identity user with extra profile fields. |
| `Shelter` | Shelter profile, contact/location, visit hours, linked user. |
| `Dog` | Main dog profile: name, breed, age, size, status, location, behavior, food, coat color, shelter. |
| `DogBreed` | Breed lookup table with optional breed notes and health considerations. |
| `DogImage` | Image URL records for dogs, including main image flag. |
| `MedicalRecord` | Dog medical records. |
| `AdoptionRequest` | Adoption workflow record with questionnaire and visit data. |
| `FavoriteDog` | Adopter saved dogs. |
| `RecentlyViewedDog` | Adopter recently viewed dogs. |
| `AdopterProfile` | Household/contact/preference data for adopters. |
| `ResourceStock` | Shelter resources and low-stock thresholds. |
| `ResourceCategory` | Resource categories such as Food, Medicine, Cleaning Supplies. |
| `FoodType` | Food lookup values. |
| `DogStatusHistory` | Old/new status history for dog status changes. |
| `Notification` | In-app notifications. |
| `ReportHistory` | Metadata for generated reports/exports. |
| `AuditLog` | Admin/activity trace records. |
| `ShelterRegistrationRequest` | Public shelter application reviewed by admin. |
| `DogSearchEmbedding` | Public-safe dog search embeddings for semantic search. |

Important statuses:

- `DogStatus.Available`
- `DogStatus.Reserved`
- `DogStatus.Adopted`
- `DogStatus.InTreatment`

Public/adopter features should generally show only `Available` and `Reserved`.

---

## 8. Dog Breed System

PawConnect originally had a free-text `Breed` string. It now has a lookup-backed breed system:

- `DogBreedId` is the primary known breed.
- `SecondaryBreedId` optionally stores a second known/likely breed for mixed dogs.
- `IsMixedBreed` indicates mixed breed.
- `CustomBreedName` stores unlisted/custom breed text.
- `DogBreedFormatter.Format(dog)` is the display helper used in UI, exports, search documents, recommendations, and Copilot data.

Expected display examples:

- Labrador Retriever
- Labrador Retriever Mix
- Labrador Retriever x Border Collie Mix
- Mixed Breed
- Unknown
- Custom breed name

Important rule from the project history:

- Do not mark every dog as "Mix".
- Use "Mix" only when the dog is genuinely mixed or has two different breeds/unknown mixed background.

Dog details also include a Breed Information card. It shows:

- formatted breed name;
- mixed-breed chip when relevant;
- overview;
- typical traits;
- care context;
- common health considerations;
- short disclaimer that breed notes are educational, not a diagnosis.

Actual medical records remain separate and more important than breed-level notes.

Important files:

- `Entities/Dog.cs`
- `Entities/DogBreed.cs`
- `Services/DogBreedFormatter.cs`
- `Services/DogBreedInformationFormatter.cs`
- `Data/DogBreedSeedData.cs`
- `Components/Pages/DogDetails.razor`
- `Components/Pages/Shelter/CreateDog.razor`
- `Components/Pages/Shelter/EditDog.razor`

---

## 9. Dog Images and Gallery

Dog images are stored as `DogImage` records with image URLs.

Important behavior:

- The database should not store the placeholder as a real dog image.
- The UI uses a placeholder only when no valid real dog image exists.
- Image URLs are validated by `DogImageUrlValidator`.
- Dog cards choose the first valid main image, otherwise first valid real image, otherwise placeholder.
- Dog details use a gallery with:
  - main image;
  - thumbnail row for multiple images;
  - no empty thumbnail space for one image;
  - clean no-photo placeholder;
  - clickable lightbox preview;
  - previous/next navigation when multiple images exist.

Important files:

- `Entities/DogImage.cs`
- `Services/DogImageService.cs`
- `Services/DogImageUrlValidator.cs`
- `Components/Pages/Dogs.razor`
- `Components/Pages/DogDetails.razor`
- `Components/Shared/DogImagePreviewDialog.razor`
- `Components/Pages/Shelter/EditDog.razor`

Known practical note:

- Some dog images are external URLs added for demo polish. If images do not show on another laptop, check network access, external image availability, and seed data.

---

## 10. Adoption Request Workflow

Main entity: `Entities/AdoptionRequest.cs`.

Main service: `Services/AdoptionRequestService.cs`.

Important states:

- `Pending`
- `VisitConfirmed`
- `Accepted`
- `Rejected`
- `Cancelled`

Visit status is tracked separately with `AdoptionVisitStatus`.

Typical flow:

1. Adopter opens a dog details page.
2. Adopter submits an adoption request with questionnaire fields and preferred visit date/time.
3. Service validates:
   - dog is requestable;
   - adopter owns the request;
   - duplicate active request is blocked;
   - visit time is valid for shelter schedule.
4. Shelter reviews the request.
5. Shelter can confirm visit:
   - request becomes visit-confirmed;
   - dog becomes `Reserved`;
   - email/in-app notification is sent;
   - `.ics` calendar attachment can be included.
6. Shelter later accepts/finalizes adoption:
   - dog becomes `Adopted`;
   - status history is recorded;
   - notifications/reports may be generated.
7. Request can also be rejected or cancelled according to ownership/status rules.

Important UI files:

- `Components/Pages/DogDetails.razor`
- `Components/Pages/Adopter/MyAdoptionRequests.razor`
- `Components/Pages/Shelter/ShelterAdoptionRequests.razor`
- `Components/Pages/Admin/AdminAdoptionRequests.razor`
- `Components/Shared/AdoptionRequestDetailsDialog.razor`

Important business rules:

- Adopters can cancel only their own pending requests.
- Shelter users manage only requests for their own shelter's dogs.
- Adopted/InTreatment dogs cannot receive new public adoption requests.
- Service-level validation matters more than UI hiding.

---

## 11. Email, PDF, Calendar Attachments, Notifications

Email service:

- `Services/IEmailService.cs`
- `Services/SmtpEmailService.cs`
- `Services/EmailMimeBuilder.cs`
- `Services/EmailAttachment.cs`
- `Services/PawConnectEmailTemplate.cs`

PDF service:

- `Services/IPdfReportService.cs`
- `Services/PdfReportService.cs`

Notifications:

- `Entities/Notification.cs`
- `Services/NotificationService.cs`
- `Components/Shared/NotificationBell.razor`
- `Components/Pages/Notifications.razor`

Calendar invite behavior:

- For confirmed shelter visits, confirmation emails can include an iCalendar `.ics` attachment.
- The attachment contains visit-related data such as dog, shelter, visit date/time, and location.
- PawConnect does not integrate directly with external calendar APIs.
- It relies on the standard iCalendar format, which email clients can interpret.

Important presentation wording:

> The application sends a standard `.ics` calendar attachment through the existing email system. It does not need Google Calendar or Outlook API integration.

Email/PDF failures are intended to be best-effort: they should be logged and should not roll back the main database action.

---

## 12. Recommendations System

Main files:

- `Services/IDogRecommendationService.cs`
- `Services/DogRecommendationService.cs`
- `Services/DogRecommendationResult.cs`
- `Services/IOpenAiRecommendationClient.cs`
- `Services/OpenAiRecommendationClient.cs`
- `Services/RecommendationOpenAiRequest.cs`
- `Components/Pages/Adopter/Recommendations.razor`
- `Components/Pages/Adopter/AdopterDashboard.razor`

What it does:

- Provides adopter-specific dog suggestions.
- Uses adopter profile fields, public-safe dog data, shelter/city information, favorites, and recently viewed dogs.
- Produces a match percentage, match label, summary, and categorized reasons.

Safety:

- Only `Available` and `Reserved` dogs are considered.
- `Adopted` and `InTreatment` dogs are excluded.
- Rule-based scoring works without OpenAI.
- Optional OpenAI can re-rank/refine explanations only from backend-provided candidates.
- Unknown OpenAI dog IDs are ignored.
- Private adopter data is not sent to OpenAI.

Important explanation:

> Recommendations are profile-based and proactive. They use the adopter profile and behavior/preferences to rank dogs. The Adoption Copilot is query-based and responds to a specific natural-language request.

---

## 13. Adoption Copilot System

This is one of the central thesis features.

Main UI:

- `Components/Pages/Adopter/AdoptionCopilot.razor`

Main services:

- `Services/IAdoptionCopilotService.cs`
- `Services/AdoptionCopilotService.cs`
- `Services/IAdoptionCopilotToolService.cs`
- `Services/AdoptionCopilotToolService.cs`
- `Services/AdoptionCopilotModels.cs`
- `Services/AdoptionCopilotToolModels.cs`
- `Services/AdoptionCopilotConstraintNormalizer.cs`
- `Services/CopilotStateService.cs`

OpenAI client:

- `Services/IOpenAiAdoptionCopilotClient.cs`
- `Services/OpenAiAdoptionCopilotClient.cs`

Semantic search:

- `Services/IDogSearchDocumentService.cs`
- `Services/DogSearchDocumentService.cs`
- `Services/IDogSearchEmbeddingService.cs`
- `Services/DogSearchEmbeddingService.cs`
- `Services/IEmbeddingService.cs`
- `Services/OpenAiEmbeddingService.cs`
- `Services/ISemanticDogSearchService.cs`
- `Services/SemanticDogSearchService.cs`
- `Entities/DogSearchEmbedding.cs`

What the Copilot does:

- Lets adopters type natural-language requests.
- Interprets intent such as:
  - size;
  - breed;
  - coat color;
  - city/neighborhood;
  - home type;
  - activity preference;
  - temperament;
  - compatibility with cats, children, other dogs, senior dogs, sensitive dogs.
- Returns real PawConnect dog cards with:
  - match score or exact-match label;
  - match label;
  - summary chips;
  - display tags;
  - caution tags;
  - favorite action;
  - View Details link.

Important Copilot architecture:

1. User enters natural-language query.
2. PawConnect detects deterministic constraints before or alongside OpenAI.
3. If OpenAI is configured, the OpenAI Responses API may request safe tool calls.
4. The AI does not access SQL directly.
5. PawConnect executes safe tools in C#.
6. `search_dogs` returns only public-safe dogs.
7. Hard constraints such as status, size, coat color, and neighborhood are enforced by backend code.
8. Dog evidence is extracted from public-safe fields.
9. Results are scored and calibrated using direct/indirect/generic/caution/missing evidence.
10. OpenAI may only choose from candidate dog IDs returned by PawConnect.
11. Unknown dog IDs are ignored.
12. If OpenAI is disabled, missing a key, or fails, rule-based fallback still works.

Public-safe dog fields used by Copilot/search:

- dog name;
- formatted breed;
- size;
- status;
- age;
- coat color;
- location;
- shelter name/city/neighborhood;
- dog description;
- behavior description;
- public medical status where appropriate;
- public image availability/display data.

Data intentionally not sent to OpenAI:

- adopter full name;
- adopter email;
- adopter phone;
- exact private address;
- passwords/tokens/security fields;
- audit logs;
- SMTP credentials;
- shelter internal notes;
- private Identity data;
- raw SQL/database access.

Important Copilot scoring/evidence concepts:

- Direct evidence: directly supports the user's main compatibility request.
- Indirect evidence: supportive but not enough for high confidence.
- Generic evidence: friendly/sweet wording that should not dominate compatibility scoring.
- Caution evidence: needs slow introductions, ask shelter, reserved status, patient adopter needed, etc.
- Missing evidence: no data found for the primary compatibility target.

Important Copilot behavior refined during development:

- Explicit user preferences override inferred assumptions.
- "Apartment" does not always mean "short walks" if the user explicitly says longer walks.
- "Short walks" and "longer walks" are different activity preferences.
- Cat queries should not show apartment tags unless the user also asked for apartment fit.
- Children queries should show child/family evidence, not generic low-activity tags.
- "Ask shelter..." tags should prevent overconfident results.
- Simple filter-only queries like "black and tan dogs" should show "Exact match" or "Matches request", not a low percentage.
- A query like "I want a dog that will behave around an older dog" means compatibility with an older resident dog, not a request for a senior candidate. It should not create `Age: at least 7 years`.

Good demo prompts:

```text
I have a sick dog recovering at home
```

Expected:

- Compatibility: Sensitive dog
- Lifestyle: Calm
- Status: Available, Reserved
- direct/caution dog-to-dog tags such as calm dog company, respectful around dogs, slow introductions, not too energetic, ask shelter if uncertain.

```text
I live in an apartment and want a dog that does not need too much activity.
```

Expected:

- Home: Apartment
- Lifestyle: Low activity
- tags such as short walks, indoor rest, settles quickly, quiet routine, small/medium size.

```text
I live in an apartment but enjoy longer walks
```

Expected:

- Home: Apartment
- Activity: Longer walks
- Lifestyle: Moderate activity
- should not reward short-walk-only dogs as longer-walk matches.

```text
black and tan dogs
```

Expected:

- Coat color filter.
- Exact match / Matches request.
- No "status filter" wording unless status was actually requested.

---

## 14. Semantic Search and Embeddings

Semantic search is optional and depends on OpenAI embedding configuration.

Main idea:

- `DogSearchDocumentService` builds public-safe text documents for dogs.
- `OpenAiEmbeddingService` can generate embeddings.
- `DogSearchEmbeddingService` stores embeddings in `DogSearchEmbeddings`.
- `SemanticDogSearchService` compares query embedding with dog embeddings.
- If embeddings are unavailable, Copilot/search falls back to keyword/rule-based logic.

Admin action:

- `/admin/dogs` includes a "Rebuild Dog Search Index" action.

Important safety:

- Only `Available` and `Reserved` dogs should be indexed.
- Stale embeddings for unavailable statuses should be removed.
- Missing OpenAI key should not break the app.

Beginner-friendly explanation:

> Embeddings transform text into vectors, allowing the system to compare meaning instead of only exact words. For example, "quiet apartment dog" can match a dog description mentioning "settles indoors after short walks."

---

## 15. Maps and Location

PawConnect has shelter map/location features.

Important files:

- `Components/Shared/ShelterMap.razor`
- `Services/NominatimGeocodingService.cs`
- `Services/DistanceService.cs`
- `Entities/Shelter.cs`
- `Components/Pages/Shelters.razor`
- `Components/Pages/ShelterDetails.razor`
- shelter create/edit/admin pages where location controls exist.

What is safe to say:

- Shelter records can store optional latitude/longitude.
- When coordinates are available, the public shelter details page can display a read-only Leaflet/OpenStreetMap map.
- Address lookup uses Nominatim/OpenStreetMap through `NominatimGeocodingService`.
- The app does not need a Google Maps API key for the embedded map.
- External Google Maps links may be used for navigation.

Caution:

- If writing a precise statement like "shelter representative can drag the map pin to update coordinates," verify the current `EditShelter`/shelter-management implementation first.

---

## 16. Resource Stock

Main files:

- `Entities/ResourceStock.cs`
- `Entities/ResourceCategory.cs`
- `Entities/FoodType.cs`
- `Services/ResourceStockService.cs`
- `Components/Pages/Shelter/Resources.razor`

Features:

- shelters manage own resources;
- category and food type support;
- quantity/unit/threshold validation;
- low-stock detection;
- export/import support where implemented;
- notifications/reports for low stock.

Important rules:

- shelter ownership checks matter;
- food resources may require food type;
- non-food resources clear food type;
- duplicate resource entries are blocked.

---

## 17. CSV Import/Export and Reports

Main files:

- `Services/CsvImportService.cs`
- `Services/ExportService.cs`
- `Services/BrowserFileDownloadService.cs`
- `Services/PdfReportService.cs`
- `Services/ReportHistoryService.cs`
- `Entities/ReportHistory.cs`

Features:

- Admin CSV/PDF exports for platform data.
- Shelter CSV/PDF exports scoped to shelter data.
- CSV imports for supported dog/resource/shelter-request flows with preview/validation.
- Report history stores metadata, not necessarily full generated binary content.

Important presentation line:

> Exports and imports are role-scoped, so shelters do not export or import data for other shelters.

---

## 18. Authentication, Authorization, and Security

Identity setup:

- `Data/ApplicationUser.cs`
- `Program.cs`
- `Components/Account/`
- role seeding in `Data/IdentitySeedData.cs`

Roles:

- `Adopter`
- `Shelter`
- `Admin`

Security principles:

- Pages use role-based authorization.
- Services enforce ownership rules.
- UI hiding is not the only protection.
- Public dog visibility excludes `Adopted` and `InTreatment`.
- Shelter users can manage only their own dogs/resources/requests.
- Adopters can manage only their own favorites/requests.
- Admin can view platform-wide management pages.
- OpenAI requests are sanitized and validated.

Password validation:

- The project uses ASP.NET Core Identity password rules configured in `Program.cs`.
- Account pages also use standard validation attributes for forms.
- Check current `Program.cs` for exact password rule settings before writing a precise thesis paragraph.

---

## 19. Testing Strategy

Current test suite after cleanup: about 149 xUnit tests.

Important folder:

- `PawConnect.Tests/`

Test infrastructure:

- `PawConnect.Tests/PawConnect.Tests.csproj`
- `PawConnect.Tests/Tests/Helpers/TestDbContextFactory.cs`
- `PawConnect.Tests/Tests/Helpers/TestDoubles.cs`

Testing style:

- Mostly service-level unit-style and integration-style tests.
- Uses EF Core InMemory.
- Uses fake email/PDF/OpenAI/geocoding services where needed.
- Does not require SQL Server, browser, SMTP server, or live OpenAI API for normal test runs.

Important tested areas:

- public dog visibility;
- dog management and status history;
- dog image URL validation;
- dog breed formatting;
- adoption request workflow;
- visit confirmation and final adoption;
- shelter ownership rules;
- adopter ownership rules;
- favorites and recently viewed dogs;
- resource stock and low stock;
- shelter registration approval;
- CSV import/export;
- notifications;
- report history;
- audit logs;
- recommendations;
- Copilot safety/fallback;
- semantic search/embeddings.

Important test classes:

- `AdoptionRequestServiceTests`
- `DogServiceTests`
- `DogImageServiceTests`
- `DogImageUrlValidatorTests`
- `DogBreedFormatterTests`
- `FavoriteDogServiceTests`
- `ResourceStockServiceTests`
- `ShelterRegistrationRequestServiceTests`
- `NotificationServiceTests`
- `ReportHistoryServiceTests`
- `AuditLogServiceTests`
- `ExportServiceTests`
- `CsvImportServiceTests`
- `DogRecommendationServiceTests`
- `SemanticDogSearchServiceTests`
- `ServiceFlowIntegrationTests`

Important user preference:

- Do not add tests for every small UI/scoring/text tweak.
- Add tests only when explicitly requested, when touching critical business/security rules, or when existing tests need adjustment.

How to explain tests:

> The tests focus on service-level business rules because this is where ownership checks, validation, state transitions, public-safe visibility, and AI safety are enforced. They are not browser end-to-end tests, but they verify realistic backend workflows quickly and reliably.

---

## 20. Seed/Demo Data

Main source:

- `Data/IdentitySeedData.cs`

Breed source:

- `Data/DogBreedSeedData.cs`

Demo data includes:

- demo accounts;
- Cluj-Napoca shelters;
- public-safe dogs;
- adopted dogs for success stories;
- resource stock;
- medical records;
- adoption requests;
- favorites/recently viewed data;
- dog image URLs.

Known demo dogs include:

- Bella
- Nala
- Max
- Mira
- Sasha
- Oscar
- Lili
- Toby
- Pip
- Iris
- Bruno
- Finn
- Rocky
- Grace
- Hazel
- Luna
- Rex
- Alma
- Archie
- Daisy
- Milo
- Nora
- Kira
- Tara
- Ollie
- Poppy

Do not assume every listed dog is public-safe. Check each dog status in `IdentitySeedData.cs` or the database.

Good demo dog:

- Mira is useful for dog details, image, breed info, behavior, medical/food/shelter context.

Good adoption request demo:

- Bella has useful adopter/shelter request workflow data in a freshly seeded database.

Good Copilot demo:

- sick/recovering dog prompt;
- apartment/low activity prompt;
- cat prompt;
- black-and-tan filter prompt;
- apartment plus longer walks prompt.

---

## 21. Live Demo Story

Recommended short story for a 15-minute thesis presentation:

1. Public visitor browses dogs at `/dogs`.
2. Open a strong dog details page, preferably Mira.
3. Show image gallery/lightbox, breed info, behavior, medical/food info, and shelter context.
4. Login as adopter.
5. Open Adoption Copilot and use a natural-language prompt.
6. Show recommendations briefly.
7. Show adopter request tracking.
8. Login as shelter.
9. Show shelter adoption request review and visit confirmation/finalization if safe.
10. Mention admin supervision, reports, logs, and background jobs instead of demoing everything live.

Must-show features:

- public dog browsing;
- dog details page;
- Adoption Copilot;
- recommendations or adopter dashboard;
- adoption request tracking/review;
- one admin/shelter management page if time allows.

Mention-only features:

- background jobs;
- CSV import/export;
- report history;
- activity logs;
- full registration flow;
- full resource-stock workflow unless specifically asked.

---

## 22. Known Practical Issues and Recent Fix Context

This section is useful if the new assistant sees odd behavior or old screenshots.

### Adopter dashboard load time

The adopter dashboard loads many pieces of data sequentially, including recommendations. The heavier recommendation pipeline can make the dashboard take a few seconds, especially on first app startup or if OpenAI is configured. A possible future improvement is to lazy-load recommendations separately.

### Dog details opened in a new tab

Dog details previously failed if optional adopter-side state such as recently viewed/favorite/pending request loading failed. The page was changed so optional adopter state failures are logged as warnings and do not prevent the dog details page from loading.

Relevant file:

- `Components/Pages/DogDetails.razor`

### Older dog Copilot query

The prompt:

```text
I want a dog that will behave around an older dog
```

should mean compatibility with an older resident dog. It should not filter candidate dogs to `Age >= 7`.

Recent change:

- `AdoptionCopilotService.cs` expanded existing/resident dog context detection.
- `AdoptionCopilotToolService.cs` guards against OpenAI/tool arguments accidentally applying `MinAgeYears = 7` for this resident-dog compatibility phrasing.

---

## 23. OpenAI Configuration and Safety

Configuration model:

- `Services/OpenAiSettings.cs`

Configuration section:

```json
"OpenAI": {
  "Enabled": true,
  "ApiKey": "",
  "Model": "gpt-5.4-mini",
  "ChatModel": "gpt-5.4-mini",
  "EmbeddingModel": "text-embedding-3-small"
}
```

Important:

- No API key should be committed.
- If `ApiKey` is empty, OpenAI features should safely fall back.
- OpenAI is optional for the demo.
- Normal tests use fake clients, not live OpenAI calls.

How to explain safely:

> OpenAI is not the source of truth. PawConnect retrieves and filters real dogs in backend services, sends only sanitized public-safe candidate data, validates returned dog IDs, and falls back to deterministic matching when OpenAI is unavailable.

---

## 24. Most Important Files to Know

| Topic | File |
|---|---|
| Startup/DI | `Program.cs` |
| Database | `Data/ApplicationDbContext.cs` |
| Seed/demo data | `Data/IdentitySeedData.cs` |
| User model | `Data/ApplicationUser.cs` |
| Dog entity | `Entities/Dog.cs` |
| Breed entity | `Entities/DogBreed.cs` |
| Adoption request entity | `Entities/AdoptionRequest.cs` |
| Shelter entity | `Entities/Shelter.cs` |
| Dog service | `Services/DogService.cs` |
| Adoption request workflow | `Services/AdoptionRequestService.cs` |
| Recommendations | `Services/DogRecommendationService.cs` |
| Recommendation OpenAI client | `Services/OpenAiRecommendationClient.cs` |
| Copilot orchestrator | `Services/AdoptionCopilotService.cs` |
| Copilot tool service | `Services/AdoptionCopilotToolService.cs` |
| Copilot OpenAI client | `Services/OpenAiAdoptionCopilotClient.cs` |
| Semantic search | `Services/SemanticDogSearchService.cs` |
| Search documents | `Services/DogSearchDocumentService.cs` |
| Embedding refresh | `Services/DogSearchEmbeddingService.cs` |
| Email | `Services/SmtpEmailService.cs` |
| PDF | `Services/PdfReportService.cs` |
| Public dogs UI | `Components/Pages/Dogs.razor` |
| Dog details UI | `Components/Pages/DogDetails.razor` |
| Copilot UI | `Components/Pages/Adopter/AdoptionCopilot.razor` |
| Recommendations UI | `Components/Pages/Adopter/Recommendations.razor` |
| Shelter requests UI | `Components/Pages/Shelter/ShelterAdoptionRequests.razor` |
| Admin requests UI | `Components/Pages/Admin/AdminAdoptionRequests.razor` |
| Testing overview | `docs/PawConnect_Testing_Processes_Explained.md` |
| Demo plan | `docs/PawConnect_Live_Demo_Plan.md` |

---

## 25. Thesis Narrative

Possible thesis framing:

> PawConnect is a web platform that supports the adoption of stray dogs by connecting adopters, shelters, and administrators. It does not only store dog profiles; it models the adoption process from discovery to request review, visit confirmation, notifications, reports, and final adoption. The project also introduces an AI-assisted Adoption Copilot that transforms natural-language adopter needs into safe, evidence-backed dog suggestions while preserving backend validation, role-based access control, and public-safe filtering.

Strong technical contributions:

- role-based adoption platform;
- service-layer business rules;
- adoption request lifecycle;
- dog status history;
- public-safe filtering;
- breed lookup and educational breed information;
- image gallery/lightbox and image URL validation;
- reports, email, calendar attachments, notifications;
- CSV import/export with validation;
- resource stock and low-stock logic;
- maps/location with Leaflet/OpenStreetMap;
- recommendations;
- Adoption Copilot with fallback, tool-based safety, semantic search, and validation;
- automated service-level tests.

---

## 26. Questions the New Assistant Should Be Ready For

General:

- Why Blazor Server?
- Why EF Core?
- Why SQL Server?
- How are roles implemented?
- How does the adoption request workflow work?
- How does the app prevent shelters from editing each other's data?
- How does the public dog filter work?
- What happens when an email/PDF fails?

AI/Copilot:

- Why add an Adoption Copilot?
- Does the AI access the database directly?
- Can the AI invent dogs?
- What data is sent to OpenAI?
- What private data is excluded?
- What happens when OpenAI is disabled?
- What are embeddings?
- Why use semantic search?
- How are tags/scores explained?
- What are Copilot limitations?

Testing:

- Why are there many tests?
- Are they unit or integration tests?
- Why use EF Core InMemory?
- Why no browser E2E tests?
- How is OpenAI tested without real API calls?

Limitations:

- no full browser E2E suite;
- external image URLs can break;
- Copilot depends on quality of dog descriptions;
- AI output is advisory, not a real compatibility guarantee;
- OpenAI is optional and may be unavailable;
- semantic embeddings need rebuild when data changes;
- local demo database must be restored/seeded correctly.

---

## 27. Instructions for Continuing Thesis Writing

When writing thesis content:

- Use clear academic English or Romanian-friendly English.
- Do not oversell AI as making adoption decisions.
- Present Copilot as an advisory search/recommendation aid.
- Emphasize that shelter confirmation and adoption workflow remain necessary.
- Emphasize privacy and validation.
- Use file/class references when explaining implementation.
- Separate actual implemented features from future improvements.
- Avoid saying OpenAI has direct database access.
- Avoid saying breed notes diagnose health conditions.
- Avoid saying maps use Google Maps API for embedded maps.
- Avoid saying tests are full E2E browser tests.

Good wording:

> The AI component does not replace backend rules. It helps interpret natural language, while PawConnect remains responsible for filtering, retrieving, validating, and displaying real dog records.

Good wording:

> The automated tests focus mainly on service-level business rules because these rules protect ownership, visibility, validation, and workflow transitions regardless of how the UI is changed.

---

## 28. Existing Documentation to Reuse

Useful docs already in the repository:

- `docs/PawConnect_Technical_Context.md`
- `docs/PawConnect_Presentation_Study_Guide.md`
- `docs/PawConnect_Live_Demo_Plan.md`
- `docs/PawConnect_Demo_Accounts.md`
- `docs/PawConnect_Testing_Processes_Explained.md`
- `docs/database-diagram.md`
- `docs/pawconnect teorie/thesis-defense/00-project-overview.md`
- `docs/pawconnect teorie/thesis-defense/01-architecture-and-code-map.md`
- `docs/pawconnect teorie/thesis-defense/02-copilot-deep-dive.md`
- `docs/pawconnect teorie/thesis-defense/03-main-feature-flows.md`
- `docs/pawconnect teorie/thesis-defense/04-database-and-ef-core.md`
- `docs/pawconnect teorie/thesis-defense/05-authentication-authorization-security.md`
- `docs/pawconnect teorie/thesis-defense/06-testing-strategy.md`
- `docs/pawconnect teorie/thesis-defense/07-presentation-defense-qa.md`
- `docs/pawconnect teorie/thesis-defense/08-code-walkthrough-cheatsheet.md`
- `docs/pawconnect teorie/thesis-defense/09-limitations-and-future-work.md`
- `docs/pawconnect teorie/thesis-defense/10-adoption-copilot-thesis-subsection-brief.md`
- `docs/pawconnect teorie/thesis-defense/11-calendar-email-attachment-brief.md`

If the new assistant needs a specific subsection, it should first check whether one of these docs already contains the needed material.

---

## 29. Suggested Prompt for a New ChatGPT

You can paste this after uploading/including this file:

```text
You are helping me continue my bachelor thesis, presentation, and live demo preparation for PawConnect.

Use the attached PawConnect context file as project memory. Do not invent features. When writing thesis subsections, keep the style clear and academic but practical. Pay extra attention to Adoption Copilot, recommendations, OpenAI safety/fallback, semantic search/embeddings, service-layer architecture, database design, authorization, and testing.

When you make claims, base them on the described files/classes/routes. If something is unclear, ask me or mark it as unclear instead of guessing.

First, summarize what you understand about PawConnect in 10 bullets, then ask me what thesis/presentation/demo task I want to continue.
```

---

## 30. Quick One-Paragraph Summary

PawConnect is a Blazor Server and EF Core web platform for stray dog adoption and shelter management. It has public dog/shelter browsing, adopter profiles, favorites, recommendations, Adoption Copilot, adoption request tracking, shelter request review, dog/resource management, admin supervision, maps, notifications, email/PDF reports, CSV import/export, status history, and scheduled jobs. Its most important thesis contribution is the combination of real adoption workflows with an AI-assisted but backend-safe Copilot: the AI can help interpret natural-language needs, but PawConnect applies public-safe filtering, service-level validation, candidate retrieval, evidence-based scoring, and final dog ID validation so the system still shows real eligible dogs and preserves privacy.
