# PawConnect CV Extension Context

This document is meant to be pasted into ChatGPT or another planning tool so it understands what PawConnect already contains and can suggest realistic, CV-worthy new features without repeating existing work.

## 1. Project Summary

PawConnect is a C# ASP.NET Core Blazor Server web application for stray dog adoption and shelter management. It supports public dog discovery, adopter workflows, shelter workflows, admin supervision, adoption requests, dog recommendations, an Adoption Copilot, resource stock management, maps, notifications, email/PDF reporting, CSV import/export, scheduled jobs, and role-based authorization.

The project is not just CRUD. It includes multi-role workflows, business rules, AI-assisted matching, public-safe filtering, report generation, notifications, scheduling, geocoding/maps, and a fairly broad service/test architecture.

## 2. Technology Stack

| Area | Current implementation |
|---|---|
| Backend/frontend | ASP.NET Core Blazor Server, C# |
| UI | MudBlazor components |
| Database | SQL Server through EF Core |
| Auth | ASP.NET Core Identity with roles |
| Roles | Public visitor, Adopter, Shelter, Admin |
| Email | SMTP through `SmtpEmailService`, local testing with smtp4dev |
| PDF | `PdfReportService` |
| CSV | `CsvImportService`, `ExportService`, browser download service |
| Jobs | Quartz.NET for scheduled reports and visit reminders |
| Maps | Leaflet/OpenStreetMap, geocoding through `NominatimGeocodingService` |
| AI | Optional OpenAI integration for Copilot, recommendations, embeddings |
| Search | Dog search documents, semantic search, embeddings, deterministic fallback |

Important startup/configuration file:

- `Program.cs`

Important registrations:

- `IDogService -> DogService`
- `IAdoptionRequestService -> AdoptionRequestService`
- `IDogRecommendationService -> DogRecommendationService`
- `IAdoptionCopilotService -> AdoptionCopilotService`
- `IAdoptionCopilotToolService -> AdoptionCopilotToolService`
- `IOpenAiAdoptionCopilotClient -> OpenAiAdoptionCopilotClient`
- `ISemanticDogSearchService -> SemanticDogSearchService`
- `IDogSearchEmbeddingService -> DogSearchEmbeddingService`
- `IEmailService -> SmtpEmailService`
- `IPdfReportService -> PdfReportService`
- `IExportService -> ExportService`
- `ICsvImportService -> CsvImportService`
- Quartz jobs: `ShelterSummaryReportJob`, `VisitReminderJob`

## 3. Main Code Areas

| Path | Purpose |
|---|---|
| `Components/Pages` | Main Blazor pages for public, adopter, shelter, and admin workflows |
| `Components/Pages/Adopter` | Adopter dashboard, recommendations, Copilot, favorites, profile, adoption requests |
| `Components/Pages/Shelter` | Shelter dashboard, dog management, adoption request review, resources |
| `Components/Pages/Admin` | Admin dashboards, users, shelters, dogs, requests, activity/report history |
| `Components/Shared` | Shared UI components, dialogs, maps, image preview, notification bell |
| `Services` | Main business logic and external integrations |
| `Entities` | EF Core domain entities |
| `Data` | `ApplicationDbContext`, `ApplicationUser`, seed/demo data, migrations |
| `Jobs` | Quartz background jobs |
| `Repositories` | Generic repository abstraction |
| `wwwroot` | Static assets and client scripts |
| `PawConnect.Tests` | xUnit service/integration-style test suite |

## 4. Important Routes

### Public

- `/` - home page
- `/dogs` - public dog listing
- `/dogs/{Id:int}` - dog details
- `/shelters` - shelter listing
- `/shelters/{Id:int}` - shelter details
- `/success-stories` - adopted dog success stories
- `/shelters/apply` - shelter registration/application

### Adopter

- `/adopter/dashboard`
- `/adopter/profile`
- `/adopter/recommendations`
- `/adopter/copilot`
- `/favorites`
- `/my-adoption-requests`
- `/notifications`

### Shelter

- `/shelter/dashboard`
- `/shelter/dogs`
- `/shelter/dogs/create`
- `/shelter/dogs/edit/{Id:int}`
- `/shelter/adoption-requests`
- `/shelter/resources`

### Admin

- `/admin/dashboard`
- `/admin/users`
- `/admin/shelters`
- `/admin/dogs`
- `/admin/adoption-requests`
- `/admin/shelter-requests`
- `/admin/report-history`
- `/admin/activity-log`

## 5. Domain Model

Important entities:

- `ApplicationUser`
- `Shelter`
- `Dog`
- `DogBreed`
- `DogImage`
- `MedicalRecord`
- `AdoptionRequest`
- `FavoriteDog`
- `RecentlyViewedDog`
- `AdopterProfile`
- `ResourceStock`
- `ResourceCategory`
- `FoodType`
- `DogStatusHistory`
- `Notification`
- `ReportHistory`
- `AuditLog`
- `ShelterRegistrationRequest`
- `DogSearchEmbedding`

Important entity files:

- `Data/ApplicationUser.cs`
- `Data/ApplicationDbContext.cs`
- `Entities/Dog.cs`
- `Entities/DogBreed.cs`
- `Entities/Shelter.cs`
- `Entities/AdoptionRequest.cs`
- `Entities/DogSearchEmbedding.cs`

## 6. Existing Feature Inventory

### 6.1 Public Dog Browsing

Users can browse Available and Reserved dogs through `/dogs`. Public listing uses public-safe filtering and dog cards with images, status chips, breed, shelter/location, description, and detail links.

Relevant files:

- `Components/Pages/Dogs.razor`
- `Components/Pages/DogDetails.razor`
- `Services/DogService.cs`
- `Services/DogImageUrlValidator.cs`
- `Services/DogBreedFormatter.cs`

### 6.2 Dog Details Page

The dog details page includes:

- dog profile information
- image gallery and lightbox
- formatted breed display
- breed information card
- common health considerations
- behavior description
- medical status
- medical records
- food details
- shelter information
- adoption request form when allowed
- role-aware return navigation

Relevant files:

- `Components/Pages/DogDetails.razor`
- `Components/Pages/DogDetails.razor.css`
- `Components/Shared/DogImagePreviewDialog.razor`
- `Services/DogBreedInformationFormatter.cs`
- `Services/DogImageUrlValidator.cs`

### 6.3 Dog Breed System

Dog breed handling uses a lookup table instead of only free text. Dogs can have:

- primary breed
- optional secondary breed
- mixed breed flag
- custom breed name
- unknown/mixed breed handling
- breed notes and health considerations

Relevant files:

- `Entities/DogBreed.cs`
- `Entities/Dog.cs`
- `Services/DogBreedService.cs`
- `Services/DogBreedFormatter.cs`
- `Services/DogBreedInformationFormatter.cs`
- `Components/Pages/Shelter/CreateDog.razor`
- `Components/Pages/Shelter/EditDog.razor`

### 6.4 Dog Image Handling

The app supports dog image URLs, main image selection, image validation, public card image fallback, details gallery, and lightbox preview.

Important behavior:

- invalid image URLs are rejected
- placeholder is UI fallback only
- real dog images are preferred over placeholders
- main image selection uses valid non-placeholder images

Relevant files:

- `Entities/DogImage.cs`
- `Services/DogImageService.cs`
- `Services/DogImageUrlValidator.cs`
- `Components/Shared/DogImagePreviewDialog.razor`
- `Components/Pages/DogDetails.razor`
- `Components/Pages/Shelter/EditDog.razor`

### 6.5 Adopter Profile, Favorites, Recently Viewed

Adopters can manage their profile, favorite dogs, and have recently viewed dogs tracked.

Relevant files:

- `Components/Pages/Adopter/MyAdopterProfile.razor`
- `Components/Pages/Adopter/Favorites.razor`
- `Services/AdopterProfileService.cs`
- `Services/FavoriteDogService.cs`
- `Services/RecentlyViewedDogService.cs`
- `Entities/AdopterProfile.cs`
- `Entities/FavoriteDog.cs`
- `Entities/RecentlyViewedDog.cs`

### 6.6 Recommended Dogs

The recommendation feature suggests dogs based on adopter profile, favorites, recently viewed dogs, public-safe dog data, and optional OpenAI enhancement.

Important behavior:

- Available/Reserved dogs only
- Adopted/InTreatment excluded
- rule-based recommendations work without OpenAI
- OpenAI cannot add unknown dog IDs
- DTOs are sanitized

Relevant files:

- `Components/Pages/Adopter/Recommendations.razor`
- `Components/Pages/Adopter/AdopterDashboard.razor`
- `Services/DogRecommendationService.cs`
- `Services/OpenAiRecommendationClient.cs`
- `Services/DogRecommendationResult.cs`
- `Services/RecommendationOpenAiRequest.cs`

### 6.7 Adoption Copilot

The Adoption Copilot is a natural-language dog search assistant at `/adopter/copilot`.

High-level flow:

1. The adopter enters a prompt.
2. `AdoptionCopilot.razor` calls `AdoptionCopilotService.AskAsync`.
3. PawConnect detects deterministic constraints such as size, coat color, status, city, neighborhood, home type, activity, and compatibility.
4. PawConnect runs a safe fallback search using `AdoptionCopilotToolService.SearchDogsAsync`.
5. If OpenAI is enabled, `OpenAiAdoptionCopilotClient.AskWithToolsAsync` sends prompt + constraints + tool definitions.
6. OpenAI may request `search_dogs`.
7. PawConnect executes the tool, not OpenAI.
8. PawConnect sends real dog candidates back as tool output.
9. OpenAI ranks/explains from those candidates.
10. PawConnect validates returned dog IDs against approved candidates.
11. PawConnect builds final result cards with scores, labels, tags, caution tags, and summary chips.

Important safety behavior:

- OpenAI does not access SQL directly.
- OpenAI can only use predefined tools.
- Dogs must come from backend candidates.
- Unknown dog IDs are ignored.
- Public-safe filters remain enforced.
- Sensitive adopter/private shelter data is not sent.

Relevant files:

- `Components/Pages/Adopter/AdoptionCopilot.razor`
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
- `Entities/DogSearchEmbedding.cs`

### 6.8 Semantic Search and Embeddings

Dog search documents are built from public-safe dog data. Embeddings can be used for semantic similarity so natural-language queries can match meaning, not only exact keywords.

Relevant files:

- `Services/DogSearchDocumentService.cs`
- `Services/DogSearchEmbeddingService.cs`
- `Services/SemanticDogSearchService.cs`
- `Services/OpenAiEmbeddingService.cs`
- `Entities/DogSearchEmbedding.cs`
- `Components/Pages/Admin/AdminDogs.razor` for search index rebuild behavior

### 6.9 Adoption Request Workflow

Adopters can submit adoption requests. Shelters review requests, confirm visits, reject requests, and finalize adoption.

Important states:

- `Pending`
- `VisitConfirmed`
- `Accepted`
- `Rejected`
- `Cancelled`

Important dog status flow:

- `Available -> Reserved -> Adopted`

Relevant files:

- `Entities/AdoptionRequest.cs`
- `Entities/AdoptionRequestStatus.cs`
- `Entities/AdoptionVisitStatus.cs`
- `Services/AdoptionRequestService.cs`
- `Components/Pages/Adopter/MyAdoptionRequests.razor`
- `Components/Pages/Shelter/ShelterAdoptionRequests.razor`
- `Components/Pages/Admin/AdminAdoptionRequests.razor`

### 6.10 Visit Confirmation, Email, Calendar Invite

When a shelter confirms a visit, the system can send email notification and an iCalendar `.ics` attachment. The app does not integrate directly with external calendar APIs; it relies on standard iCalendar attachments.

Relevant files:

- `Services/AdoptionRequestService.cs`
- `Services/EmailMimeBuilder.cs`
- `Services/SmtpEmailService.cs`
- `Services/VisitSchedulingHelper.cs`

### 6.11 Notifications

The app has in-app notifications, notification bell, unread count, mark as read, and user-scoped notification access.

Relevant files:

- `Entities/Notification.cs`
- `Services/NotificationService.cs`
- `Components/Shared/NotificationBell.razor`
- `Components/Pages/Notifications.razor`

### 6.12 Shelter Dog Management

Shelters can create/edit dogs, manage dog images, status, medical records, breed data, coat color, food details, and status history.

Relevant files:

- `Components/Pages/Shelter/ManageDogs.razor`
- `Components/Pages/Shelter/CreateDog.razor`
- `Components/Pages/Shelter/EditDog.razor`
- `Services/DogService.cs`
- `Services/DogImageService.cs`
- `Services/MedicalRecordService.cs`
- `Entities/DogStatusHistory.cs`

### 6.13 Shelter Resources

Shelters manage resource stock, categories, food types, quantities, units, and low-stock thresholds.

Relevant files:

- `Components/Pages/Shelter/Resources.razor`
- `Services/ResourceStockService.cs`
- `Services/ResourceCategoryService.cs`
- `Services/FoodTypeService.cs`
- `Entities/ResourceStock.cs`
- `Entities/ResourceCategory.cs`
- `Entities/FoodType.cs`

### 6.14 Admin Workflows

Admins can view/manage users, shelters, dogs, adoption requests, shelter applications, reports, and activity logs.

Relevant files:

- `Components/Pages/Admin/AdminDashboard.razor`
- `Components/Pages/Admin/AdminUsers.razor`
- `Components/Pages/Admin/AdminShelters.razor`
- `Components/Pages/Admin/AdminDogs.razor`
- `Components/Pages/Admin/AdminAdoptionRequests.razor`
- `Components/Pages/Admin/AdminShelterRequests.razor`
- `Components/Pages/Admin/AdminReportHistory.razor`
- `Components/Pages/Admin/AdminActivityLog.razor`

### 6.15 Reports, CSV, PDF

The app supports CSV/PDF export and report history. Reports can be generated for shelter/admin workflows and resource/adoption data.

Relevant files:

- `Services/ExportService.cs`
- `Services/CsvImportService.cs`
- `Services/PdfReportService.cs`
- `Services/ReportHistoryService.cs`
- `Entities/ReportHistory.cs`
- `Components/Pages/Admin/AdminReportHistory.razor`

### 6.16 Shelter Applications

Public users can submit shelter applications. Admin can approve/reject them, creating shelter users/profiles when approved.

Relevant files:

- `Components/Pages/ShelterApply.razor`
- `Components/Pages/Admin/AdminShelterRequests.razor`
- `Services/ShelterRegistrationRequestService.cs`
- `Entities/ShelterRegistrationRequest.cs`

### 6.17 Maps and Location

Shelter records can store coordinates. The app uses Leaflet/OpenStreetMap for map display and Nominatim for geocoding.

Relevant files:

- `Components/Shared/ShelterMap.razor`
- `Components/Pages/ShelterDetails.razor`
- `Components/Pages/Shelters.razor`
- `Services/NominatimGeocodingService.cs`
- `Services/DistanceService.cs`

## 7. Existing Strengths To Preserve

Any new feature proposal should preserve:

- Role-based authorization and ownership checks.
- Public-safe dog visibility.
- Adoption request business rules.
- Available/Reserved-only public dog discovery.
- OpenAI safety: no private data, no invented dog IDs.
- Service-layer business logic instead of Razor-only logic.
- Existing MudBlazor style.
- Existing seed/demo data quality.
- Current test coverage for core business rules.

## 8. Good CV-Worthy Extension Ideas

The following ideas build naturally on PawConnect and would be strong for a CV. They are grouped by type.

### 8.1 Real-Time Shelter-Adopter Messaging

Add a secure messaging system between adopters and shelters after an adoption request is submitted.

Why it is CV-worthy:

- Real-time communication with SignalR.
- Role and ownership rules.
- Message read/unread states.
- Attachment moderation could be added later.

Possible implementation:

- Entities: `Conversation`, `Message`, `MessageReadReceipt`
- Pages: adopter request conversation, shelter request conversation
- Services: `ConversationService`, `MessageService`
- SignalR hub: `AdoptionChatHub`
- Notifications when new messages arrive

Important rules:

- Only adopter who owns the request and shelter that owns the dog can access the conversation.
- Admin can optionally view/report conversations for moderation.

### 8.2 Adoption Pipeline / Kanban Board

Add a visual shelter workflow board for adoption requests.

Columns:

- Pending
- Visit requested
- Visit confirmed
- Accepted
- Rejected/cancelled

Why it is CV-worthy:

- Workflow visualization.
- Drag-and-drop UI.
- Service-level state transition rules.
- Fits existing adoption request system.

Possible files:

- `Components/Pages/Shelter/ShelterAdoptionPipeline.razor`
- `Services/AdoptionRequestService.cs`
- `Entities/AdoptionRequest.cs`

### 8.3 Advanced Analytics Dashboard

Add charts and analytics for admins/shelters.

Examples:

- adoption conversion rate
- average time from request to adoption
- dogs by status
- requests by shelter
- low-stock trends
- Copilot queries by intent, without storing private data

Why it is CV-worthy:

- Data aggregation.
- Dashboard design.
- Charting library integration.
- Useful management insights.

Possible implementation:

- `AnalyticsService`
- DTOs for dashboard metrics
- MudBlazor charts or a chart library
- Optional cached metrics table later

### 8.4 Public REST API Layer

Add a versioned REST API for selected public-safe data.

Endpoints:

- `GET /api/v1/dogs`
- `GET /api/v1/dogs/{id}`
- `GET /api/v1/shelters`
- `GET /api/v1/shelters/{id}`

Why it is CV-worthy:

- Shows API design skills.
- Swagger/OpenAPI documentation.
- DTO-based security.
- Rate limiting/versioning can be added.

Important:

- Public-safe DTOs only.
- No private adopter/shelter/internal notes.
- Keep Blazor pages intact.

### 8.5 Foster Home Module

Add foster care workflows for dogs not ready for adoption or needing temporary homes.

Features:

- foster applicant profiles
- foster suitability checks
- foster placement requests
- foster status on dogs
- foster reports/check-ins

Why it is CV-worthy:

- New domain workflow.
- Complex permissions.
- Extends beyond adoption CRUD.

### 8.6 Volunteer Management

Add volunteer accounts and scheduling.

Features:

- volunteer role
- volunteer availability
- shelter task sign-up
- dog walking appointments
- completed volunteer hours

Why it is CV-worthy:

- Scheduling.
- Role-based workflows.
- Calendar/event management.
- Useful real-world shelter feature.

### 8.7 Donation and Sponsorship System

Add dog/resource sponsorship or donation tracking.

Features:

- sponsor a dog
- donate resources
- donation receipts
- shelter needs list
- public donation goal progress

Why it is CV-worthy:

- Payment provider integration if added.
- Financial/reporting workflow.
- Public engagement feature.

Possible staged approach:

1. Internal pledge/donation tracking without payments.
2. Later add Stripe or another payment gateway.

### 8.8 Copilot Conversation History and Feedback

Improve the Adoption Copilot with user feedback.

Features:

- save previous Copilot sessions
- thumbs up/down result feedback
- "why this match?" panel
- feedback analytics for admin
- prompt categories/intents dashboard

Why it is CV-worthy:

- AI product UX.
- Feedback loop.
- Explainable AI.
- Responsible AI tracking.

Privacy note:

- Avoid storing raw private prompts if unnecessary.
- Store sanitized intent metadata where possible.

### 8.9 Improved AI Matching Evaluation

Add an admin-facing AI evaluation tool.

Features:

- test prompt set
- expected result notes
- score distribution
- drift detection after seed/model changes
- compare rule-based vs OpenAI output

Why it is CV-worthy:

- AI evaluation discipline.
- Shows understanding of model uncertainty.
- Good portfolio talking point.

### 8.10 Dog Compatibility Structured Fields

Add optional structured compatibility fields to dogs.

Examples:

- good with cats: unknown/yes/slow introductions/no
- good with dogs: unknown/yes/calm dogs only/slow introductions/only dog
- good with children: unknown/yes/older children only/no
- activity level: low/medium/high
- experience needed: beginner/some experience/experienced

Why it is CV-worthy:

- Improves matching quality.
- Reduces reliance on free-text descriptions.
- Makes Copilot more explainable.

Important:

- Keep free-text description too.
- Existing Copilot should combine structured fields and natural text evidence.

### 8.11 Multi-Shelter Collaboration

Allow shelters to transfer dogs/resources or collaborate on capacity.

Features:

- transfer dog to another shelter
- transfer resource stock
- admin approval of transfers
- transfer history

Why it is CV-worthy:

- Multi-tenant/ownership complexity.
- Audit trails.
- Transactional workflows.

### 8.12 Audit and Observability Upgrade

Add production-style observability.

Features:

- structured logs
- request correlation IDs
- admin health page
- job execution history
- email delivery status
- OpenAI/API call status without secrets

Why it is CV-worthy:

- Backend engineering maturity.
- Reliability focus.
- Useful for production readiness.

### 8.13 Mobile/PWA Support

Add Progressive Web App features.

Features:

- installable app
- offline-friendly public dog browsing cache
- mobile-first adopter dashboard
- push notifications if feasible

Why it is CV-worthy:

- Frontend/mobile UX.
- Offline caching.
- Service worker concerns.

### 8.14 Enhanced Search UX

Improve `/dogs` with advanced search and saved filters.

Features:

- saved searches
- alert when new matching dog appears
- multi-select filters
- map-based dog/shelter search
- filter chips with clear actions

Why it is CV-worthy:

- Product polish.
- State management.
- Background notifications.

### 8.15 Appointment Calendar UI

Build a real in-app calendar for shelter visits.

Features:

- shelter availability slots
- adopter selects available slot
- conflict prevention
- calendar view
- ICS email attachments remain supported

Why it is CV-worthy:

- Scheduling logic.
- Conflict handling.
- Calendar UI.

## 9. Highest-Impact Feature Roadmap

If the goal is to make the project stronger for a CV, a good roadmap would be:

1. Real-time shelter-adopter messaging
2. Adoption pipeline board
3. Advanced analytics dashboard
4. Public REST API with Swagger
5. Copilot feedback/history and explainability
6. Appointment calendar with availability slots
7. Structured dog compatibility fields

This combination demonstrates:

- backend architecture
- real-time communication
- AI/product thinking
- analytics
- API design
- workflow design
- security/authorization

## 10. Suggestions To Avoid Or Deprioritize

These are less impressive or too similar to existing work:

- another simple CRUD page with no workflow
- cosmetic-only changes
- adding more seed data as a main CV feature
- adding more filters without improving the search model
- replacing the app with a new frontend
- rewriting the Copilot from scratch
- adding payment integration before donation/business rules are clear

## 11. Constraints For Future ChatGPT Suggestions

When asking ChatGPT for new feature ideas, include these constraints:

- Do not rebuild the app from scratch.
- Preserve Blazor Server, EF Core, Identity, MudBlazor, and the service layer.
- Keep changes scoped and incremental.
- Do not bypass role/ownership checks.
- Keep public-safe dog filtering.
- Keep OpenAI optional and validated.
- Add service-level tests for important business rules, but avoid excessive low-value tests.
- Prefer features that demonstrate backend/business complexity, not only UI polish.

## 12. Suggested Prompt To Give ChatGPT

Use this prompt after pasting the context above:

```text
Based on the PawConnect context above, propose 10 CV-worthy feature extensions.

For each feature, include:
- why it is useful for the product
- why it is impressive for a CV
- affected pages/routes
- affected services/entities
- database changes needed
- security/authorization rules
- estimated implementation difficulty
- suggested implementation phases
- risks or things to avoid

Prioritize features that build naturally on the existing architecture and demonstrate backend engineering, AI safety, workflow design, analytics, real-time features, API design, or production readiness.
Do not suggest generic CRUD pages unless they support a meaningful workflow.
```

## 13. One-Line Project Pitch For CV

PawConnect is a Blazor Server and EF Core stray-dog adoption platform with role-based shelter/adopter/admin workflows, AI-assisted dog matching, semantic search, adoption request processing, notifications, email/PDF/CSV reporting, scheduled jobs, maps, and public-safe data controls.

