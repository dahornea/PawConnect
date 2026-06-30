# PawConnect Essential Features for Presentation and Demo

This document summarizes the PawConnect features that are most worth explaining in the bachelor thesis presentation and showing during the short live demo.

The goal is not to show every page or every CRUD operation. The goal is to show that PawConnect is a complete adoption and shelter-management workflow with role-based access, real business rules, AI-assisted search, safety checks, reports, notifications, and tested backend logic.

## 1. Executive Summary

PawConnect is a Blazor Server web application for stray dog adoption and shelter management. It supports public dog discovery, adopter profiles, adoption requests, shelter operations, admin supervision, notifications, reports, CSV import/export, maps, recommendations, semantic search, and an Adoption Copilot.

For a 15-minute thesis defense, the live demo should focus on a coherent story:

1. A public visitor discovers dogs.
2. The user opens a rich dog details page.
3. An adopter uses Adoption Copilot or recommendations to find a suitable dog.
4. The adopter tracks adoption requests.
5. A shelter representative reviews and manages adoption requests.
6. An admin can supervise platform-level data.

The strongest technical message is:

> PawConnect is not only a CRUD app. It models a real adoption workflow with role-based responsibilities, service-layer business rules, public-safe filtering, AI-assisted discovery with fallback behavior, reporting, notifications, and automated tests around important workflows.

## 2. Feature Selection Criteria

Features were selected for the presentation/demo using these criteria:

| Criterion | Meaning for the demo |
| --- | --- |
| Thesis relevance | Shows a meaningful contribution, not just basic UI. |
| Workflow value | Helps explain the real adoption process from discovery to shelter decision. |
| Technical depth | Demonstrates services, EF Core, Identity roles, validation, reports, AI, or background logic. |
| Demo stability | Can be shown quickly without depending too much on external services. |
| Visual clarity | Looks good in screenshots and during a live presentation. |
| Anti-CRUD value | Shows business rules, state transitions, security, or AI-assisted reasoning. |

Features like login/register, simple create/edit forms, or every admin table should be mentioned only when needed. They should not consume live demo time.

## 3. Must-Have Presentation Features

These features should appear in the slides even if not all are shown live.

| Feature | Why it matters | Role | Main files/routes |
| --- | --- | --- | --- |
| Public dog browsing and filtering | Shows the core adoption discovery experience and public-safe visibility rules. | Public / Adopter | `/dogs`, `Components/Pages/Dogs.razor`, `Services/DogService.cs`, `Entities/Dog.cs` |
| Dog details page | Shows rich dog information: images, breed, medical status, food, shelter, and adoption actions. | Public / Adopter | `/dogs/{Id:int}`, `Components/Pages/DogDetails.razor`, `Components/Shared/DogImagePreviewDialog.razor`, `Services/DogBreedFormatter.cs` |
| Breed information system | Shows that breed is database-backed and formatted carefully, including mixed breeds and educational notes. | Public / Shelter | `Entities/DogBreed.cs`, `Entities/Dog.cs`, `Services/DogBreedFormatter.cs`, `Services/DogBreedInformationFormatter.cs` |
| Image gallery and lightbox | Makes the dog profile visually polished and demonstrates careful fallback behavior for missing images. | Public / Adopter | `DogDetails.razor`, `DogImagePreviewDialog.razor`, `Services/DogImageUrlValidator.cs`, `Services/DogImageSelector.cs` |
| Adoption request workflow | Central domain workflow: request, visit confirmation, reservation, acceptance/rejection, status changes. | Adopter / Shelter | `/my-adoption-requests`, `/shelter/adoption-requests`, `Services/AdoptionRequestService.cs`, `Entities/AdoptionRequest.cs` |
| Shelter request review | Shows the shelter-side operational workflow and role separation. | Shelter | `Components/Pages/Shelter/ShelterAdoptionRequests.razor`, `Services/AdoptionRequestService.cs` |
| Recommendations | Shows personalized matching from adopter profile, favorites/recent views, and public-safe dogs. | Adopter | `/adopter/recommendations`, `Services/DogRecommendationService.cs`, `Services/OpenAiRecommendationClient.cs` |
| Adoption Copilot | Strongest AI feature: natural-language search, intent interpretation, evidence-based ranking, OpenAI optional path, fallback behavior. | Adopter | `/adopter/copilot`, `Components/Pages/Adopter/AdoptionCopilot.razor`, `Services/AdoptionCopilotService.cs`, `Services/AdoptionCopilotToolService.cs`, `Services/OpenAiAdoptionCopilotClient.cs` |
| Semantic dog search / embeddings | Shows meaning-based matching beyond simple keyword search when embeddings are available. | Adopter / Admin | `Services/SemanticDogSearchService.cs`, `Services/DogSearchDocumentService.cs`, `Services/DogSearchEmbeddingService.cs`, `Services/OpenAiEmbeddingService.cs`, `Entities/DogSearchEmbedding.cs` |
| Public-safe AI filtering | Important safety point: AI only receives sanitized/public-safe candidate data and cannot invent dogs. | Adopter | `AdoptionCopilotService.cs`, `AdoptionCopilotToolService.cs`, `OpenAiAdoptionCopilotClient.cs`, `DogSearchDocumentService.cs` |
| Shelter resources and low stock | Shows shelter operations beyond adoption requests. | Shelter | `/shelter/resources`, `Components/Pages/Shelter/Resources.razor`, `Services/ResourceStockService.cs` |
| Notifications, emails, PDFs, calendar invite | Shows workflow communication and report generation. | Adopter / Shelter / Admin | `Services/NotificationService.cs`, `Services/SmtpEmailService.cs`, `Services/PdfReportService.cs`, `Services/EmailMimeBuilder.cs`, `Services/VisitReminderService.cs` |
| Admin supervision | Shows platform-level visibility: users, shelters, dogs, adoption requests, audit logs, reports. | Admin | `/admin/dashboard`, `/admin/adoption-requests`, `/admin/activity-log`, `/admin/report-history` |
| Map/location support | Shows shelter discovery using Leaflet/OpenStreetMap and stored coordinates. | Public / Shelter | `/shelters`, `/shelters/{Id:int}`, `Components/Shared/ShelterMap.razor`, `Services/NominatimGeocodingService.cs`, `Services/DistanceService.cs` |
| Automated tests | Shows reliability and validates service-layer business rules. | Technical | `PawConnect.Tests`, `Helpers/TestDbContextFactory.cs`, service test classes |

## 4. Must-Show Demo Features

The live demo should be about 5-6 minutes. These are the best features to show because they are visual, meaningful, and connected.

| Demo step | Feature | Role | Page/route | Demo data | Duration | Risk | What to show |
| --- | --- | --- | --- | --- | ---: | --- | --- |
| 1 | Public dog browsing | Public | `/dogs` | Any seeded public dogs | 35-45s | Low | Show public list, filters, images, statuses, and that only adoptable dogs appear. |
| 2 | Dog details page | Public / Adopter | Open a strong dog card such as Mira, Bella, or Nala | Use a dog with image, breed info, medical/food/shelter data | 60s | Low | Show image gallery/lightbox, breed information, medical status, food, shelter/location. |
| 3 | Adoption Copilot | Adopter | `/adopter/copilot` | Prompt: `I have a sick dog recovering at home` | 90s | Medium | Show natural-language query, intent chips, evidence-backed result cards, caution tags, fallback note. |
| 4 | Recommendations | Adopter | `/adopter/recommendations` | Seeded adopter account | 45s | Low/Medium | Show personalized recommendation cards with match reasons. |
| 5 | My adoption requests | Adopter | `/my-adoption-requests` | Existing request such as Bella if seeded | 45s | Low | Show request tracking, compact status chips, visit status, dates, cancel button only if allowed. |
| 6 | Shelter adoption request review | Shelter | `/shelter/adoption-requests` | Existing shelter request queue | 60-75s | Medium | Show shelter-side management, details, status/visit workflow. |
| 7 | Admin overview | Admin | `/admin/dashboard` or `/admin/adoption-requests` | Admin account | 30-45s | Low | Show platform supervision, not detailed CRUD. |

Recommended live sequence:

1. Start as public user on `/dogs`.
2. Open one polished dog details page.
3. Log in as adopter and run Adoption Copilot.
4. Show recommendations or My Adoption Requests.
5. Switch to shelter and show adoption request review.
6. Briefly show admin dashboard if time remains.

## 5. Mention-Only Features

These features are useful and technically relevant, but they should usually be explained in slides rather than demonstrated live.

| Feature | Why mention it | Main files/routes |
| --- | --- | --- |
| CSV import/export | Useful operational feature, but risky and slow to demo live. | `Services/CsvImportService.cs`, `Services/ExportService.cs` |
| PDF reports | Shows reporting with QuestPDF, but opening PDFs can waste demo time. | `Services/PdfReportService.cs` |
| Email notifications | Important workflow communication, but depends on SMTP/dev catcher. | `Services/SmtpEmailService.cs`, `Services/EmailMimeBuilder.cs` |
| Calendar `.ics` attachment | Good technical detail for visit confirmations; mention as standards-based integration. | `EmailMimeBuilder.cs`, `AdoptionRequestService.cs` if used in visit confirmation flow |
| Background jobs | Shows scheduling architecture, but not visually useful live. | `Jobs/ShelterSummaryReportJob.cs`, `Jobs/VisitReminderJob.cs`, Quartz setup in `Program.cs` |
| Audit logs | Good for traceability and admin supervision, but not central to the live story. | `/admin/activity-log`, `Services/AuditLogService.cs` |
| Report history | Shows generated report metadata and accountability. | `/admin/report-history`, `Services/ReportHistoryService.cs` |
| Shelter application approval | Important onboarding flow, but too long for a 5-minute demo. | `/shelters/apply`, `/admin/shelter-requests`, `ShelterRegistrationRequestService.cs` |
| Resource stock management | Good operational feature; show only if you skip recommendations or admin. | `/shelter/resources`, `ResourceStockService.cs` |
| Search index rebuild | Useful technical/admin feature, but should not be run live unless needed. | `/admin/dogs`, `DogSearchEmbeddingService.cs` |

## 6. Skip or Deprioritize

These should not be shown live unless specifically asked.

| Feature | Reason to skip in short demo |
| --- | --- |
| Register/login flow | Basic framework functionality; mention Identity instead. |
| Creating every type of entity live | Looks like CRUD and risks validation/state issues. |
| Full dog creation form | Useful, but too long and less central than adoption/Copilot workflow. |
| Deleting dogs/resources/users | Risky in demo database and not central. |
| Full CSV import with invalid rows | Valuable but time-consuming and easy to derail. |
| Rebuilding embeddings/search index live | Depends on OpenAI/settings and can take time. |
| Background job execution | Not immediate or visual enough. |
| Every admin management page | Admin should be shown as supervision, not exhaustive CRUD. |
| Every filter combination | Show one or two filters only; explain the rest. |

## 7. Anti-CRUD Explanation

If the committee asks why PawConnect is more than a CRUD application, use these points.

| Area | Why it is more than CRUD |
| --- | --- |
| Adoption lifecycle | Requests move through business states: Pending, VisitConfirmed, Accepted, Rejected, Cancelled. Dog status also changes from Available to Reserved to Adopted. |
| Role-based workflows | Public visitors, adopters, shelters, and admins see and can do different things. Access is enforced in services, not only hidden in the UI. |
| Ownership rules | Shelters manage only their own dogs/resources/requests. Adopters manage only their own favorites and adoption requests. |
| Public-safe visibility | Public/adopter pages show Available and Reserved dogs, while Adopted/InTreatment dogs are excluded from public discovery and Copilot results. |
| AI-assisted discovery | Copilot interprets natural language, retrieves safe candidates, validates IDs, and falls back to deterministic logic. |
| Evidence-based matching | Copilot and recommendations use dog descriptions, behavior, size, status, location, breed, coat color, and adopter context. |
| Notifications and reports | Important workflow events create notifications, emails, PDFs, report history, and sometimes calendar attachments. |
| Search and embeddings | Semantic search supports meaning-based matching when embeddings are available, with fallback behavior when they are not. |
| Testing | Service tests verify business rules, transitions, safety, ownership, imports/exports, notifications, and AI fallback. |

Short answer:

> The CRUD pages exist because shelters need to manage data, but the main contribution is the workflow around that data: adoption request state transitions, role-based ownership, public-safe filtering, AI-assisted search, notifications, reports, and tests that validate the rules.

## 8. Feature-to-Slide Mapping

Suggested slide flow for a 9-10 minute presentation before the live demo:

| Slide | Topic | Feature focus | Time |
| --- | --- | --- | ---: |
| 1 | Title and motivation | Stray dog adoption and shelter coordination problem | 20s |
| 2 | Problem statement | Fragmented discovery, manual shelter work, weak matching | 45s |
| 3 | Objectives | Adoption platform, shelter tools, AI-assisted matching, traceability | 45s |
| 4 | User roles | Public, Adopter, Shelter, Admin | 45s |
| 5 | Architecture | Blazor Server, EF Core, Identity, services, SQL Server | 60s |
| 6 | Database model | Dogs, shelters, users, adoption requests, resources, notifications, embeddings | 60s |
| 7 | Public/adopter experience | Browse dogs, details, breed info, image gallery, favorites | 60s |
| 8 | Adoption workflow | Request, visit confirmation, reservation, final adoption | 75s |
| 9 | Shelter/admin operations | Request review, resources, reports, audit/admin supervision | 60s |
| 10 | Recommendations and Copilot | Personalized recommendations and natural-language search | 90s |
| 11 | AI safety and fallback | Public-safe candidates, sanitized DTOs, validated dog IDs, no direct SQL access | 60s |
| 12 | Testing | xUnit, EF Core InMemory, service-flow tests, AI fallback tests | 60s |
| 13 | Demo transition | Explain the live story you will show | 20s |
| 14 | Conclusion/future work | Value, limitations, future improvements | 45s |

## 9. Demo Script Draft

### Step 1: Public discovery

Open: `/dogs`

Say:

> This is the public discovery page. It only shows dogs that are available or reserved for adoption, not dogs that are adopted or in treatment. The cards use validated real images when available and fall back to a clean placeholder when needed.

Show:

- Dog list
- Status chips
- Filter panel briefly
- Image fallback only if relevant

### Step 2: Dog details

Open: a polished dog profile such as Mira, Bella, Nala, or another dog with a good image and complete data.

Say:

> The dog details page combines adopter-facing information from several entities: dog profile, breed lookup, images, medical records, food information, and shelter data. Breed information is educational only; the actual behavior and medical records remain the source of truth.

Show:

- Main image
- Lightbox if image is good
- Breed Information section
- Medical Status / Medical Records
- Shelter card and location/map if available

### Step 3: Adoption Copilot

Open: `/adopter/copilot`

Use prompt:

```text
I have a sick dog recovering at home
```

Say:

> The Copilot converts natural language into adoption criteria. Here it should understand that the user needs a calm dog that will not overwhelm another sensitive or recovering dog. The backend retrieves only public-safe dog candidates, extracts evidence from public dog data, ranks them, and validates final dog IDs before display.

Show:

- Intent/summary chips
- Result cards
- Match scores or labels
- Direct evidence tags and caution tags

Backup prompt:

```text
I live in an apartment and want a dog that does not need too much activity.
```

### Step 4: Recommendations or request tracking

Open one:

- `/adopter/recommendations`
- `/my-adoption-requests`

Say for recommendations:

> Recommendations use the adopter profile, preferences, favorites, recently viewed dogs, and public-safe dog data. The score is only a ranking aid, not a guarantee of compatibility.

Say for My Adoption Requests:

> The adopter can track request status and visit status. The UI avoids raw enum labels and shows the current workflow state clearly.

### Step 5: Shelter request review

Open: `/shelter/adoption-requests`

Say:

> On the shelter side, the request becomes an operational workflow. The shelter can review details, confirm a visit, reject a request, or finalize adoption according to the allowed state transitions.

Show:

- Requests queue
- Details button/dialog
- Status chips
- Manage actions if safe

### Step 6: Admin overview

Open: `/admin/dashboard` or `/admin/adoption-requests`

Say:

> Admin pages give platform-level supervision: users, shelters, dogs, adoption requests, reports, and audit logs. Admin is not replacing shelter work; it supervises the platform.

Show briefly only if time remains.

## 10. Strongest 5 Features to Emphasize

### 1. Adoption Copilot

Why it is strong:

- Natural-language dog search.
- Public-safe candidate retrieval.
- Evidence-based scoring and tags.
- OpenAI optional path plus deterministic fallback.
- Backend validation prevents invented dogs.

Code to reference:

- `Components/Pages/Adopter/AdoptionCopilot.razor`
- `Services/AdoptionCopilotService.cs`
- `Services/AdoptionCopilotToolService.cs`
- `Services/OpenAiAdoptionCopilotClient.cs`
- `Services/SemanticDogSearchService.cs`
- `Entities/DogSearchEmbedding.cs`

### 2. Adoption request lifecycle

Why it is strong:

- Models real adoption workflow, not only a form submission.
- Includes visit confirmation, reservation, final adoption, rejection, cancellation.
- Updates dog status and creates notifications/reports where appropriate.

Code to reference:

- `Entities/AdoptionRequest.cs`
- `Services/AdoptionRequestService.cs`
- `Components/Pages/Adopter/MyAdoptionRequests.razor`
- `Components/Pages/Shelter/ShelterAdoptionRequests.razor`

### 3. Role-based shelter/admin workflow

Why it is strong:

- Different user roles have different responsibilities.
- Shelter ownership checks protect data.
- Admin can supervise platform-wide data.

Code to reference:

- `Program.cs`
- `Data/ApplicationUser.cs`
- `Components/Pages/Shelter/ManageDogs.razor`
- `Components/Pages/Admin/AdminDashboard.razor`
- `Services/ShelterService.cs`
- `Services/DogService.cs`

### 4. Rich dog profile and public discovery

Why it is strong:

- Public dog cards and details pages are polished and domain-specific.
- Uses image selection, breed lookup, mixed-breed formatting, medical/food/shelter data.
- Helps evaluators understand the real user experience.

Code to reference:

- `Components/Pages/Dogs.razor`
- `Components/Pages/DogDetails.razor`
- `Services/DogImageSelector.cs`
- `Services/DogBreedFormatter.cs`
- `Entities/Dog.cs`
- `Entities/DogBreed.cs`

### 5. Testing and reliability

Why it is strong:

- Tests focus on service-layer business rules.
- Covers adoption workflow, visibility, ownership, resources, reports, CSV, recommendations, Copilot safety, and semantic fallback.
- Shows engineering discipline without needing a live browser automation suite.

Code to reference:

- `PawConnect.Tests`
- `PawConnect.Tests/Helpers/TestDbContextFactory.cs`
- `PawConnect.Tests/ServiceFlowIntegrationTests.cs`
- `PawConnect.Tests/AdoptionRequestServiceTests.cs`
- `PawConnect.Tests/DogRecommendationServiceTests.cs`
- `PawConnect.Tests/SemanticDogSearchServiceTests.cs`

## 11. Possible Committee Questions

### What are the most important features?

Strong answer:

> The most important features are dog discovery, dog details, adoption request workflow, shelter request management, recommendations, Adoption Copilot, and admin supervision. These together cover the whole adoption process from discovery to decision.

### Why is this more than a CRUD application?

Strong answer:

> CRUD is only the data-management layer. The project also implements role-based workflows, ownership checks, adoption state transitions, public-safe filtering, notifications, reports, semantic search, AI-assisted Copilot matching, and automated tests for business rules.

### Why did you choose the demo features?

Strong answer:

> I chose features that tell the complete user story in a short time: public discovery, detailed dog profile, AI-assisted matching, adoption request tracking, shelter review, and admin supervision. They are also the most visually clear and technically representative.

### How does the Copilot avoid inventing dogs?

Strong answer:

> The AI is not the database source of truth. PawConnect retrieves candidates through backend services, sends only sanitized public-safe dog data, and validates returned dog IDs against the real candidate list. Unknown IDs are ignored.

### What happens if OpenAI is unavailable?

Strong answer:

> The feature still works through deterministic parsing, public-safe filtering, semantic or keyword/rule-based search, and local scoring. OpenAI improves natural-language interpretation, but the application does not depend on it to function.

### Does the AI see private data?

Strong answer:

> No sensitive adopter or shelter data is needed for Copilot candidate search. The AI request uses public-safe dog information and sanitized DTOs. It does not include passwords, tokens, audit logs, SMTP credentials, private adopter contact data, or internal notes.

### Why not show account registration live?

Strong answer:

> Registration is standard Identity functionality. For a short thesis demo, it is better to use seeded accounts and focus on the domain-specific adoption workflow and Copilot.

### Why not show CSV import/export live?

Strong answer:

> CSV import/export is implemented, but it is slower and less visual. It is better to mention it in the operational features slide and keep the live demo focused on the adoption story.

### What are the main limitations?

Strong answer:

> The system still depends on the quality of shelter-provided descriptions. AI suggestions are advisory, not final decisions. Real production deployment would need stronger monitoring, possibly image storage instead of external URLs, and browser end-to-end tests.

## 12. Final Cheat Sheet

### Must show live

- `/dogs` public dog browsing.
- One polished `/dogs/{id}` details page.
- `/adopter/copilot` with a strong prompt.
- `/adopter/recommendations` or `/my-adoption-requests`.
- `/shelter/adoption-requests`.
- `/admin/dashboard` only if time remains.

### Best Copilot prompts

```text
I have a sick dog recovering at home
```

```text
I live in an apartment and want a dog that does not need too much activity.
```

```text
I have a cat at home.
```

```text
I want an active dog for a house with a yard.
```

### Mention in slides, not live

- CSV import/export.
- PDF reports.
- Email/calendar attachments.
- Background jobs.
- Audit logs.
- Report history.
- Shelter application approval.
- Search index rebuild.

### Avoid during live demo

- Registering new accounts.
- Deleting data.
- Running long imports.
- Rebuilding embeddings.
- Depending on real SMTP/OpenAI unless already verified.
- Showing every CRUD page.

### Demo risks and backups

| Risk | Backup |
| --- | --- |
| OpenAI is disabled or unavailable | Explain fallback and run Copilot with deterministic results. |
| Images fail to load | Use a dog with known working image; mention placeholder fallback. |
| Email server is not running | Explain email/calendar behavior from slides instead of sending live. |
| Adoption request already processed | Use existing request tracking page or reset database before demo. |
| Wrong role/session active | Open separate browser profiles or log out before the demo. |
| Dog IDs differ after database restore | Navigate through cards instead of typing numeric URLs. |
| Search embeddings are stale | Use fallback-friendly prompts and mention admin rebuild as a maintenance feature. |

### One-sentence thesis message

> PawConnect combines a traditional adoption platform with shelter operations, workflow automation, safe AI-assisted dog discovery, and tested service-layer business rules.

