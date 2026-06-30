# Presentation Defense Q&A

## General Project Motivation

| Likely question | What they are testing | Strong answer | Code files to reference | What not to say |
| --- | --- | --- | --- | --- |
| Why did you build PawConnect? | Whether the project solves a real problem. | PawConnect organizes stray dog adoption workflows for public users, adopters, shelters, and admins. It combines dog discovery, shelter management, adoption requests, resources, notifications, reports, and AI-assisted matching. | `Components/Pages/Dogs.razor`, `Components/Pages/Shelter/*`, `Components/Pages/Adopter/*` | Do not say it is only a CRUD app. |
| What is the main contribution? | Originality. | The strongest contribution is a complete role-based adoption workflow plus an AI Copilot that maps natural language adopter needs to real public-safe dog data with fallback and validation. | `Services/AdoptionCopilotService.cs`, `Services/AdoptionCopilotToolService.cs` | Do not overclaim that AI guarantees compatibility. |

## Architecture

| Likely question | What they are testing | Strong answer | Code files to reference | What not to say |
| --- | --- | --- | --- | --- |
| How is the application structured? | Layering and maintainability. | Blazor components handle UI, services handle business logic, EF Core handles data access, Identity handles users/roles, and external integrations are wrapped in services. | `Program.cs`, `Data/ApplicationDbContext.cs`, `Services/*` | Do not say everything is in pages. |
| Is there a REST API? | Understanding Blazor Server. | Core workflows are Blazor Server components calling injected services directly. Identity endpoints are mapped, but the main app is not API-first. | `Program.cs`, `.razor` pages | Do not invent controllers. |
| Where is dependency injection configured? | Startup knowledge. | In `Program.cs`, where DbContext, Identity, MudBlazor, services, OpenAI clients, and Quartz jobs are registered. | `Program.cs` | Do not answer vaguely with "in ASP.NET". |

## Backend

| Likely question | What they are testing | Strong answer | Code files to reference | What not to say |
| --- | --- | --- | --- | --- |
| Where is business logic located? | Separation of concerns. | Mostly in services such as `DogService`, `AdoptionRequestService`, `ResourceStockService`, `DogRecommendationService`, and Copilot services. | `Services/*.cs` | Do not say Razor pages handle all logic. |
| How do you handle adoption status transitions? | Workflow correctness. | `AdoptionRequestService` changes request status, visit status, dog status, creates status history, sends notifications/emails, and refreshes embeddings best-effort. | `Services/AdoptionRequestService.cs`, `Entities/AdoptionRequest.cs` | Do not say status is changed manually in UI only. |

## Frontend / Blazor

| Likely question | What they are testing | Strong answer | Code files to reference | What not to say |
| --- | --- | --- | --- | --- |
| Why Blazor Server? | Technical choice. | It allows building the UI and backend in C#, with direct service injection, fast development, and consistent validation/business types for a thesis project. | `Components/Pages/*`, `Program.cs` | Do not claim it is always best for all scale scenarios. |
| How is UI consistency handled? | UI design awareness. | The app uses MudBlazor components such as cards, tables, dialogs, buttons, chips, and snackbars, plus shared components for images/dialogs. | `Components/Shared/*`, `.razor.css` files | Do not say no UI framework was used. |

## Database / EF Core

| Likely question | What they are testing | Strong answer | Code files to reference | What not to say |
| --- | --- | --- | --- | --- |
| What are the main tables? | Data model understanding. | Users/roles, shelters, dogs, dog breeds, images, medical records, adoption requests, favorites, recently viewed dogs, adopter profiles, resources, notifications, report history, audit logs, dog search embeddings. | `Data/ApplicationDbContext.cs`, `Entities/*` | Do not list only Dogs and Users. |
| Why use migrations? | Schema management. | Migrations version schema changes such as dog breeds, embeddings, secondary breed, and coat color. | `Migrations/*` | Do not say migrations are only for seeding. |
| How do you prevent duplicate pending adoption requests? | Constraints. | The service checks for pending requests and the database has a filtered unique index on adopter and dog for pending status. | `Services/AdoptionRequestService.cs`, `Data/ApplicationDbContext.cs` | Do not rely only on UI. |

## Copilot / AI

| Likely question | What they are testing | Strong answer | Code files to reference | What not to say |
| --- | --- | --- | --- | --- |
| How does Copilot work? | AI architecture. | The UI sends a natural-language query to `AdoptionCopilotService`. The backend parses deterministic constraints, retrieves public-safe candidates through `AdoptionCopilotToolService`, extracts evidence and scores dogs, then optionally lets OpenAI use predefined tools and explain/rerank only backend candidates. | `AdoptionCopilot.razor`, `AdoptionCopilotService.cs`, `AdoptionCopilotToolService.cs`, `OpenAiAdoptionCopilotClient.cs` | Do not say "OpenAI searches the database". |
| Does the AI access SQL? | Security. | No. OpenAI can request predefined tool calls, but PawConnect executes them through services and returns sanitized results. | `OpenAiAdoptionCopilotClient.cs`, `AdoptionCopilotService.cs` | Do not say OpenAI has database credentials. |
| Can the AI invent dogs? | Hallucination control. | Unknown dog IDs are ignored. Final results are validated against candidate dogs returned by backend tools. | `AdoptionCopilotService.cs`, `DogRecommendationService.cs` | Do not say hallucination is impossible in general. |
| What happens without OpenAI? | Reliability. | Copilot and recommendations use deterministic/rule-based fallback. Semantic search also falls back when embeddings/API are unavailable. | `AdoptionCopilotService.cs`, `SemanticDogSearchService.cs`, `DogRecommendationService.cs` | Do not say the feature stops working. |
| What are embeddings? | Understanding semantic search. | Embeddings convert dog search text and user queries into vectors so the app can compare meaning, not only exact keywords. | `OpenAiEmbeddingService.cs`, `DogSearchEmbeddingService.cs`, `SemanticDogSearchService.cs` | Do not make it sound like magic. |

## Testing

| Likely question | What they are testing | Strong answer | Code files to reference | What not to say |
| --- | --- | --- | --- | --- |
| Why 281 tests? | Testing maturity. | The app has many workflows, states, roles, validation rules, AI fallback cases, filters, imports, and reports. The tests are focused, not inflated. | `PawConnect.Tests/Tests/*` | Do not say it was just to reach a number. |
| What are your most important tests? | Risk prioritization. | Copilot unknown ID/fallback tests, adoption lifecycle tests, public-safe dog filtering, CSV validation, resource low-stock tests, and service flow integration tests. | `SemanticDogSearchServiceTests.cs`, `AdoptionRequestServiceTests.cs` | Do not mention only simple formatting tests. |

## Security

| Likely question | What they are testing | Strong answer | Code files to reference | What not to say |
| --- | --- | --- | --- | --- |
| How do you restrict shelters? | Access control. | Pages require the Shelter role and service methods verify `shelterId` ownership before modifying dogs/resources/requests. | `DogService.cs`, `AdoptionRequestService.cs`, `ResourceStockService.cs` | Do not say hiding buttons is enough. |
| How is private AI data protected? | AI privacy. | The AI receives sanitized public dog DTOs. It does not receive passwords, tokens, raw SQL, private internal notes, audit logs, or connection strings. | `AdoptionCopilotToolModels.cs`, `OpenAiAdoptionCopilotClient.cs` | Do not say "all data" is sent. |

## UX/UI

| Likely question | What they are testing | Strong answer | Code files to reference | What not to say |
| --- | --- | --- | --- | --- |
| How did you make the app usable for different roles? | UX thinking. | Navigation and pages are role-specific. Public visitors browse, adopters manage requests/recommendations, shelters manage operations, admins manage platform data. | `Components/Layout/NavMenu.razor`, role pages | Do not say one dashboard fits all. |

## Limitations and Future Work

| Likely question | What they are testing | Strong answer | Code files to reference | What not to say |
| --- | --- | --- | --- | --- |
| What are the limitations? | Honesty. | UI tests are limited, OpenAI depends on external availability, image URLs are external, descriptions influence AI quality, and production deployment would need stronger operational security. | `09-limitations-and-future-work.md` | Do not claim the project is production-perfect. |
| What would you improve next? | Engineering judgment. | Add structured compatibility fields, browser end-to-end tests, managed image storage, production monitoring, stronger AI evaluation datasets, and deployment hardening. | Relevant services/pages | Do not propose unrelated features only. |

## Personal Contribution

| Likely question | Strong answer |
| --- | --- |
| What part was hardest? | The hardest part was making AI useful but controlled: natural-language interpretation, evidence-backed scoring, fallback when OpenAI is disabled, and backend validation against real dogs. |
| What are you most proud of? | The adoption workflow and Copilot safety design, because they show full-stack engineering, domain modeling, and responsible AI integration. |
