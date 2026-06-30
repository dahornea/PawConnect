# Main Feature Flows

## Note About Controllers and Endpoints

PawConnect is primarily a Blazor Server application. The main feature flows below do not go through custom REST controllers. Razor components call injected services directly in the same ASP.NET Core app process. ASP.NET Core Identity endpoints are mapped in `Program.cs`, but dog browsing, adoption requests, resources, Copilot, and admin/shelter workflows are implemented through Blazor pages plus services.

## User Registration and Login

### User goal

Create an account or sign in to access adopter, shelter, or admin features.

### Step-by-step

1. User opens an Identity page under `Components/Account/Pages`.
2. ASP.NET Core Identity handles login, register, confirmation, and account state.
3. Identity stores users in `AspNetUsers` using `ApplicationUser`.
4. Roles are assigned from seeded roles in `IdentitySeedData`.
5. Role-protected pages use `[Authorize(Roles = "...")]`.

### Files/components

- `Components/Account/*`
- `Data/ApplicationUser.cs`
- `Data/IdentitySeedData.cs`
- `Program.cs`

### Services/entities

- `UserManager<ApplicationUser>`
- `SignInManager<ApplicationUser>`
- `ApplicationUser`
- `IdentityRole`

### Validation

Handled by ASP.NET Core Identity pages and Identity options.

### Authorization

Role-based with `Adopter`, `Shelter`, and `Admin`.

### Edge cases

- A logged-in user may not have the right role for a page.
- Shelter accounts are normally created through shelter application approval.

### Defense explanation

"Authentication is handled by ASP.NET Core Identity. I extended the default user with `ApplicationUser`, seeded roles, and used role-based authorization on Razor components."

## Viewing Dog Listings

### User goal

Browse adoptable dogs.

### Step-by-step

1. User opens `/dogs`.
2. `Dogs.razor` calls `DogService.GetAvailableDogsAsync`.
3. Only `Available` and `Reserved` dogs are loaded.
4. Filters can be applied through `DogService.SearchDogsAsync`.
5. Cards display formatted breed, age, status, shelter, location, description, and image fallback.

### Files/components

- `Components/Pages/Dogs.razor`
- `Components/Shared/DogCardImage.razor`
- `Services/DogService.cs`
- `Services/DogBreedFormatter.cs`
- `Services/DogImageUrlValidator.cs`

### Database entities

- `Dog`
- `Shelter`
- `DogImage`
- `DogBreed`

### Validation/authorization

Public page. Public-safe filter is applied in the service query.

### Edge cases

- Dogs with no valid image show a UI placeholder.
- Reserved dogs can still appear, but with status indication.
- Adopted/InTreatment dogs are not shown in normal public browse.

### Defense explanation

"Public dog browsing is not just `select all dogs`; the service only returns public-safe statuses and includes related shelter, breed, and image data."

## Filtering and Searching Dogs

### User goal

Find dogs by criteria such as name, breed, age, size, location, shelter, neighborhood, status, or coat color.

### Step-by-step

1. User enters filters on `/dogs`.
2. `Dogs.razor` calls `DogService.SearchDogsAsync`.
3. The query starts with public-safe dogs.
4. Additional filters are applied.
5. Results are sorted using `DogSortOption`.

### Files/services

- `Components/Pages/Dogs.razor`
- `Services/DogService.cs`
- `Services/DogCoatColorOptions.cs`
- `Entities/DogSortOption.cs`

### Important rules

- Coat color is normalized through `DogCoatColorOptions.Normalize`.
- Breed search checks legacy `Breed`, `CustomBreedName`, primary breed, secondary breed, and formatted mixed breed names.
- Neighborhood filter uses `Shelter.Neighborhood`.

### Defense explanation

"Search is implemented as EF Core filtering over public-safe dog records. Breed handling supports old text, lookup breeds, custom breeds, and mixed-breed display."

## Viewing Dog Details

### User goal

Inspect one dog and decide whether to save or request adoption.

### Step-by-step

1. User opens `/dogs/{Id:int}`.
2. `DogDetails.razor` calls `DogService.GetDogDetailsAsync`.
3. The page displays dog identity, breed, age, size, coat color, description, behavior, medical status, shelter, food, images, medical records, and breed information.
4. For adopters, it can save favorites and submit adoption requests.
5. For shelter/admin contexts, it can show status history/admin messages depending on role.

### Files/components

- `Components/Pages/DogDetails.razor`
- `Components/Shared/DogImagePreviewDialog.razor`
- `Components/Shared/DogStatusHistoryDialog.razor`
- `Services/DogService.cs`
- `Services/DogBreedInformationFormatter.cs`

### Edge cases

- No image: polished placeholder.
- Invalid image URLs are ignored for display.
- Breed notes are educational, not medical diagnosis.

### Defense explanation

"Dog details combines public profile data with related entities. It also demonstrates role-aware behavior and safe fallbacks for missing images or breed notes."

## Creating and Editing Dog Profiles

### User goal

Shelter representatives create and maintain dogs assigned to their shelter.

### Step-by-step

1. Shelter opens `/shelter/dogs` or `/shelter/dogs/create`.
2. `CreateDog.razor` uses breed autocomplete, mixed breed fields, coat color, age, size, location, description, behavior, food, and image URL input.
3. `DogService.CreateDogAsync(dog, shelterId)` validates and saves the dog.
4. `EditDog.razor` loads the dog with `DogService.GetDogForShelterAsync`.
5. Shelter edits profile fields, images, medical records, and status.
6. `DogService.UpdateDogAsync` validates ownership and creates status history if status changed.

### Files/services

- `Components/Pages/Shelter/ManageDogs.razor`
- `Components/Pages/Shelter/CreateDog.razor`
- `Components/Pages/Shelter/EditDog.razor`
- `Services/DogService.cs`
- `Services/DogImageService.cs`
- `Services/MedicalRecordService.cs`
- `Services/DogBreedFormatter.cs`

### Authorization

Shelter role only. Service methods require shelter ID and verify ownership.

### Edge cases

- Adopted dogs are read-only for shelter users.
- Dogs with adoption request history cannot be deleted by shelter/admin deletion methods.
- Image URLs are validated before saving.

### Defense explanation

"The shelter UI calls service methods that require the shelter ID, so the service layer prevents shelters from editing dogs they do not own."

## Adoption Request Flow

### User goal

Adopter requests to adopt a dog and shelter manages the request.

### Step-by-step

1. Adopter opens `DogDetails.razor`.
2. Adopter fills questionnaire: reason, hours alone, preferred visit time, additional information.
3. `AdoptionRequestService.CreateRequestAsync` validates adopter role, dog status, duplicate pending request, and visit time.
4. Request is saved as `Pending` with `VisitStatus.Requested`.
5. Shelter sees request in `/shelter/adoption-requests`.
6. Shelter can confirm visit. Request becomes `VisitConfirmed`, visit becomes `Confirmed`, dog becomes `Reserved`.
7. Shelter can mark as adopted after confirmed visit. Request becomes `Accepted`, visit becomes `Completed`, dog becomes `Adopted`.
8. Shelter can reject or adopter can cancel pending requests.

### Files/services

- `Components/Pages/DogDetails.razor`
- `Components/Pages/Shelter/ShelterAdoptionRequests.razor`
- `Components/Pages/Adopter/MyAdoptionRequests.razor`
- `Services/AdoptionRequestService.cs`
- `Services/VisitSchedulingHelper.cs`

### Entities

- `AdoptionRequest`
- `Dog`
- `DogStatusHistory`
- `Notification`
- `ReportHistory`

### Validation rules

- Only adopters can submit.
- Dog must be Available or Reserved.
- Duplicate pending/visit-confirmed requests are blocked.
- Preferred visit time is validated against shelter scheduling.
- Only pending requests can be cancelled.
- Only visit-confirmed requests can be marked adopted.

### Defense explanation

"The adoption request flow is a state machine. The service changes request status, visit status, and dog status together, and records status history, notifications, emails, and reports."

## Shelter Dashboard Flow

### User goal

Shelter gets an overview of dogs, requests, resources, and reports.

### Files/components

- `Components/Pages/Shelter/ShelterDashboard.razor`
- `Services/ShelterService.cs`
- `Services/DogService.cs`
- `Services/AdoptionRequestService.cs`
- `Services/ResourceStockService.cs`
- `Services/ShelterSummaryReportService.cs`
- `Services/ReportHistoryService.cs`

### Defense explanation

"The shelter dashboard summarizes operational data for the currently logged-in shelter account, not for all shelters."

## Admin Dashboard Flow

### User goal

Admin monitors and manages platform-wide data.

### Files/components

- `Components/Pages/Admin/AdminDashboard.razor`
- `AdminDogs.razor`
- `AdminAdoptionRequests.razor`
- `AdminUsers.razor`
- `AdminShelters.razor`
- `AdminShelterRequests.razor`
- `AdminActivityLog.razor`
- `AdminReportHistory.razor`

### Important admin actions

- View all dogs and adoption requests.
- View status history.
- Rebuild dog search index through `IDogSearchEmbeddingService.RebuildDogSearchIndexAsync`.
- Review shelter applications.
- Export reports.
- View audit/report history.

### Defense explanation

"Admin pages are role-protected and mostly use the same service layer, but with platform-wide queries."

## Email and Notification Flow

### User goal

Users receive updates about adoption requests, visits, reports, and resource events.

### Files/services

- `Services/SmtpEmailService.cs`
- `Services/PawConnectEmailTemplate.cs`
- `Services/EmailMimeBuilder.cs`
- `Services/NotificationService.cs`
- `Services/PdfReportService.cs`
- `Services/ReportHistoryService.cs`
- `Entities/Notification.cs`
- `Entities/ReportHistory.cs`

### Examples

- New adoption request email to shelter.
- Visit confirmation email with calendar invite.
- Adoption status email with PDF.
- Low-stock resource notification.
- Shelter summary report.

### Edge cases

Email/PDF failures are caught and logged so they do not roll back the main business action.

### Defense explanation

"The business action is primary. Email and PDF are side effects, so failures are logged instead of blocking the adoption workflow."

## Resource and Stock Management Flow

### User goal

Shelter tracks resources like food, medicine, blankets, cleaning supplies.

### Files/services/entities

- `Components/Pages/Shelter/Resources.razor`
- `Services/ResourceStockService.cs`
- `Entities/ResourceStock.cs`
- `Entities/ResourceCategory.cs`
- `Entities/FoodType.cs`

### Validation rules

- Name required.
- Category required.
- Quantity must be valid.
- Unit required.
- Low-stock threshold must be valid.
- Food category can be associated with a `FoodType`.

### Edge cases

- Low-stock resources trigger notifications and optional report generation.
- CSV import preview shows row errors before import.

### Defense explanation

"Resource management supports shelter operations beyond adoption. It includes validation, ownership scoping, low-stock detection, exports, and imports."

## Dog Status History Flow

### User goal

Track when a dog status changes.

### Files/services/entities

- `Services/DogService.cs`
- `Entities/DogStatusHistory.cs`
- `Components/Shared/DogStatusHistoryDialog.razor`
- `Components/Pages/Admin/AdminDogs.razor`
- `Components/Pages/Shelter/EditDog.razor`

### Explanation

When a dog status changes, `DogService` adds a `DogStatusHistory` row containing old status, new status, changed time, changed by user, and notes.

### Defense explanation

"Status history is important because adoption systems need traceability. It helps explain when a dog became reserved, adopted, or returned to available."

## Image and Gallery Flow

### User goal

Display dog photos cleanly and manage image URLs.

### Files/services

- `Entities/DogImage.cs`
- `Services/DogImageService.cs`
- `Services/DogImageUrlValidator.cs`
- `Components/Shared/DogCardImage.razor`
- `Components/Shared/DogImagePreviewDialog.razor`
- `Components/Pages/DogDetails.razor`
- `Components/Pages/Shelter/EditDog.razor`

### Rules

- Valid real images are preferred over placeholders.
- Placeholder is a UI fallback, not a real `DogImage` record.
- Main image is selected from `IsMainImage` if valid, otherwise first valid real image.
- Invalid URLs are rejected at service/input level and filtered out for display.

### Defense explanation

"The image system separates stored real image URLs from UI fallback placeholders. That prevents placeholder records from polluting the database."
