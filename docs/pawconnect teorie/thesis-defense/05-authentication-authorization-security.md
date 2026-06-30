# Authentication, Authorization, and Security

## Authentication

PawConnect uses ASP.NET Core Identity.

Important files:

- `Program.cs`
- `Data/ApplicationUser.cs`
- `Data/IdentitySeedData.cs`
- `Components/Account/*`

`ApplicationUser` extends the Identity user with:

- `FullName`
- navigation collections for favorites, recently viewed dogs, adoption requests, notifications
- optional `Shelter`
- optional `AdopterProfile`

Identity is configured in `Program.cs` with:

- cookie authentication
- Identity roles
- EF Core stores
- token providers

## Roles

Roles are seeded in `Data/IdentitySeedData.cs`:

- `Adopter`
- `Shelter`
- `Admin`

| Role | Main abilities |
| --- | --- |
| Public visitor | Browse public pages and submit shelter application. |
| Adopter | Profile, favorites, recommendations, Copilot, adoption requests. |
| Shelter | Manage own shelter dogs/resources/adoption requests. |
| Admin | Platform-wide management, applications, reports, logs, search index. |

## Page-Level Authorization

Blazor pages use `[Authorize]` attributes.

Examples:

| Page | File | Rule |
| --- | --- | --- |
| Copilot | `Components/Pages/Adopter/AdoptionCopilot.razor` | `Adopter` only |
| Recommendations | `Components/Pages/Adopter/Recommendations.razor` | `Adopter` only |
| Manage dogs | `Components/Pages/Shelter/ManageDogs.razor` | `Shelter` only |
| Shelter adoption requests | `Components/Pages/Shelter/ShelterAdoptionRequests.razor` | `Shelter` only |
| Admin dogs | `Components/Pages/Admin/AdminDogs.razor` | `Admin` only |

## Service-Level Ownership Checks

Page authorization is not the only protection. Important service methods also check ownership.

Examples:

| Service method | Security rule |
| --- | --- |
| `DogService.GetDogForShelterAsync(dogId, shelterId)` | Shelter can load only its own dog. |
| `DogService.UpdateDogAsync(dog, shelterId, changedByUserId)` | Throws if dog does not belong to shelter. |
| `DogService.DeleteDogAsync(dogId, shelterId)` | Throws if dog does not belong to shelter. |
| `AdoptionRequestService.CancelRequestAsync(requestId, adopterId)` | Adopter can cancel only own request. |
| `AdoptionRequestService.ConfirmVisitAsync(requestId, shelterId, userId)` | Shelter can manage only requests for its own dog. |
| `ResourceStockService.GetResourcesForShelterAsync(shelterId)` | Resource data is shelter-scoped. |
| `MedicalRecordService.*(shelterId, ...)` | Medical record edits require shelter ownership. |

## Public-Safe Dog Filtering

Public dog queries normally show only:

- `DogStatus.Available`
- `DogStatus.Reserved`

Examples:

- `DogService.GetAvailableDogsAsync`
- `DogService.SearchDogsAsync`
- `DogRecommendationService.GetRuleBasedRecommendationsAsync`
- `AdoptionCopilotToolService.SearchDogsAsync`
- `DogSearchEmbeddingService.RebuildDogSearchIndexAsync`

This prevents adopted and in-treatment dogs from being shown in normal public/adopter discovery flows.

## AI Safety

AI safety is especially important because Copilot and recommendations call OpenAI.

Important files:

- `Services/OpenAiSettings.cs`
- `Services/OpenAiAdoptionCopilotClient.cs`
- `Services/AdoptionCopilotService.cs`
- `Services/AdoptionCopilotToolService.cs`
- `Services/OpenAiRecommendationClient.cs`

Safety rules implemented by design:

- OpenAI is optional.
- No API key means fallback behavior.
- AI does not directly query SQL.
- Copilot tools are predefined.
- Tool outputs use sanitized DTOs.
- Unknown dog IDs from OpenAI are ignored.
- OpenAI cannot add dogs that were not backend candidates.
- OpenAI candidate data is public dog information, not private internal state.

## Input Validation

Validation appears in:

- Data annotations on entities.
- MudBlazor form validation.
- Service-level validation.
- Database constraints.

Examples:

| Area | Validation |
| --- | --- |
| Dog images | `DogImageUrlValidator` rejects invalid URLs. |
| Dogs | `DogService.ValidateAndNormalizeDogAsync` requires name, breed, age, location. |
| Adoption requests | `AdoptionRequestService.ValidateQuestionnaire` validates reason and hours alone. |
| Visit time | `VisitSchedulingHelper.ValidatePreferredVisitTime`. |
| Resources | `ResourceStockService.PrepareResourceAsync` and entity ranges. |
| Local redirects | `LocalReturnUrlHelper` tests reject external return URLs. |

## Security Risks Handled

| Risk | Mitigation |
| --- | --- |
| Shelter editing another shelter's dog | Service methods require and verify `shelterId`. |
| Adopter cancelling someone else's request | `CancelRequestAsync` checks `AdopterId`. |
| Duplicate pending request | Service check and filtered unique index. |
| OpenAI hallucinated dog | Backend validates dog ID against candidates. |
| Private data sent to AI | AI DTOs are sanitized and public-safe. |
| External redirect | `LocalReturnUrlHelper` validates local return URLs. |
| Invalid image URLs | `DogImageUrlValidator` and display filtering. |

## Security Limitations

Be ready to explain these honestly:

- This is a thesis/demo application, not a fully hardened production deployment.
- No advanced rate limiting was observed in the inspected code.
- The app depends on correct deployment configuration for HTTPS, secrets, and SMTP/OpenAI keys.
- External image URLs can still break after they are saved if the remote host changes.
- OpenAI is minimized and validated, but still an external dependency.
- UI/component authorization is strong, but production systems should also include deeper penetration/security testing.

## Committee Questions

| Question | Strong answer |
| --- | --- |
| How do you separate adopters, shelters, and admins? | ASP.NET Core Identity roles protect pages, and services perform ownership checks using user/shelter IDs. |
| Can a shelter edit another shelter's dog? | No. `DogService.UpdateDogAsync` and related methods require the shelter ID and throw if the dog is not owned by that shelter. |
| Can public users see adopted dogs? | Normal public search uses Available/Reserved filters. Adopted dogs may appear only in specific success-story contexts, not adoption discovery. |
| Can OpenAI access private data? | No direct database access. It receives sanitized candidate data and backend tool outputs only. |
| What happens if OpenAI returns invalid data? | The backend ignores unknown IDs and falls back to safe rule-based results if needed. |

## What to Say If Asked About Security

"I used defense in depth: Identity roles protect pages, service methods enforce ownership, EF Core constraints protect important data rules, and AI output is validated before it reaches the UI. For a production deployment I would add more operational security such as rate limiting, stronger monitoring, and full secret management."
