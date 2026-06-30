# Database and EF Core

## EF Core Overview

PawConnect uses Entity Framework Core with SQL Server.

Main file:

- `Data/ApplicationDbContext.cs`

Startup registration:

- `Program.cs`

Migrations:

- `Migrations/*`

The context inherits from:

- `IdentityDbContext<ApplicationUser, IdentityRole, string>`

This means the PawConnect database includes both ASP.NET Core Identity tables and application-specific tables.

## Main Entities

| Entity | File | Purpose | Important fields | Relationships |
| --- | --- | --- | --- | --- |
| `ApplicationUser` | `Data/ApplicationUser.cs` | Identity user extended with profile fields. | `FullName`, `Favorites`, `RecentlyViewedDogs`, `AdoptionRequests`, `Shelter`, `AdopterProfile` | One user can be adopter, shelter account, admin through roles. |
| `Shelter` | `Entities/Shelter.cs` | Shelter profile. | `Name`, `Address`, `City`, `Neighborhood`, `Latitude`, `Longitude`, visit settings, `ApplicationUserId` | One shelter has many dogs/resources; optional one-to-one with user. |
| `Dog` | `Entities/Dog.cs` | Main adoptable dog record. | `Name`, `Breed`, `DogBreedId`, `SecondaryBreedId`, `IsMixedBreed`, `CustomBreedName`, `CoatColor`, `AgeYears`, `AgeMonths`, `Size`, `Status`, descriptions. | Many dogs belong to one shelter; has images, medical records, requests, favorites, history, embedding. |
| `DogBreed` | `Entities/DogBreed.cs` | Breed lookup table and educational notes. | `Name`, `IsActive`, `GeneralDescription`, `TypicalTraits`, `CareNotes`, `CommonHealthConsiderations` | Referenced by `Dog.DogBreedId` and `Dog.SecondaryBreedId`. |
| `DogImage` | `Entities/DogImage.cs` | Dog image URL. | `DogId`, `ImageUrl`, `IsMainImage` | Many images per dog. |
| `MedicalRecord` | `Entities/MedicalRecord.cs` | Dog medical record. | `DogId`, `VaccineName`, `TreatmentDescription`, `RecordDate`, `Notes` | Many medical records per dog. |
| `AdoptionRequest` | `Entities/AdoptionRequest.cs` | Adoption request and visit lifecycle. | `DogId`, `AdopterId`, `Status`, `PreferredVisitDateTime`, `VisitStatus`, questionnaire fields, notes. | Belongs to dog and adopter; optional visit confirmer. |
| `FavoriteDog` | `Entities/FavoriteDog.cs` | Join between adopter and dog. | `AdopterId`, `DogId`, `CreatedAt` | Many-to-many style join. |
| `RecentlyViewedDog` | `Entities/RecentlyViewedDog.cs` | Tracks recently viewed dogs. | `AdopterId`, `DogId`, `ViewedAt` | Many-to-many style join. |
| `AdopterProfile` | `Entities/AdopterProfile.cs` | Adopter preferences/profile. | `HousingType`, `HasYard`, `HasOtherPets`, `HasChildren`, `ExperienceWithDogs`, `City` | One-to-one with `ApplicationUser`. |
| `ResourceStock` | `Entities/ResourceStock.cs` | Shelter stock/resource item. | `ShelterId`, `ResourceCategoryId`, `FoodTypeId`, `Name`, `Quantity`, `Unit`, `LowStockThreshold` | Belongs to shelter/category/optional food type. |
| `ResourceCategory` | `Entities/ResourceCategory.cs` | Resource category lookup. | `Name` | Referenced by resources. |
| `FoodType` | `Entities/FoodType.cs` | Food type lookup. | `Name` | Referenced by dogs and resources. |
| `DogStatusHistory` | `Entities/DogStatusHistory.cs` | Status transition history. | `DogId`, `OldStatus`, `NewStatus`, `ChangedAt`, `ChangedByUserId`, `Notes` | Many history rows per dog. |
| `Notification` | `Entities/Notification.cs` | In-app notifications. | `UserId`, `Title`, `Message`, `Category`, `Type`, `IsRead`, `Link` | Many notifications per user. |
| `ReportHistory` | `Entities/ReportHistory.cs` | Report generation/send audit. | `ReportType`, `RecipientEmail`, `FileName`, `WasSuccessful`, `ShelterId`, `AdminUserId` | Optional links to shelter/admin/context. |
| `AuditLog` | `Entities/AuditLog.cs` | Admin activity log. | `UserId`, `Action`, `EntityName`, `EntityId`, `Description`, `CreatedAt` | Logical reference to actions/entities. |
| `ShelterRegistrationRequest` | `Entities/ShelterRegistrationRequest.cs` | Public shelter application. | `CreatedByUserId`, `CreatedShelterId`, `ReviewedByUserId`, `Status`, contact fields. | Links applicant/admin/shelter where available. |
| `DogSearchEmbedding` | `Entities/DogSearchEmbedding.cs` | Semantic search vector data. | `DogId`, `Content`, `ContentHash`, `EmbeddingJson`, `EmbeddingModel`, `UpdatedAt` | One-to-one with dog. |

## Relationship Summary

Important relationships configured in `ApplicationDbContext.cs`:

- `Shelter` has many `Dogs`.
- `Dog` has many `DogImages`.
- `Dog` has many `MedicalRecords`.
- `Dog` has many `AdoptionRequests`.
- `Dog` has many `DogStatusHistories`.
- `Dog` has one `DogSearchEmbedding`.
- `ApplicationUser` has one `AdopterProfile`.
- `ApplicationUser` may have one `Shelter`.
- `ApplicationUser` has many `FavoriteDogs`.
- `ApplicationUser` has many `RecentlyViewedDogs`.
- `ApplicationUser` has many `Notifications`.
- `ResourceStock` belongs to `Shelter`, `ResourceCategory`, and optional `FoodType`.
- `Dog` references `DogBreed` twice: primary and secondary breed.

## Important Constraints and Indexes

From `ApplicationDbContext.cs`:

| Constraint/index | Purpose |
| --- | --- |
| Unique `DogBreed.Name` | Prevent duplicate breed lookup names. |
| Unique `AdopterProfile.ApplicationUserId` | One adopter profile per user. |
| Unique `DogSearchEmbedding.DogId` | One embedding document per dog. |
| Unique `FavoriteDog(AdopterId, DogId)` | Prevent duplicate favorites. |
| Unique `RecentlyViewedDog(AdopterId, DogId)` | One recent-view row per adopter/dog. |
| Filtered unique pending adoption request `(AdopterId, DogId)` | Prevent duplicate active requests for same dog/adopter. |
| Filtered unique pending shelter application email | Prevent duplicate pending shelter applications for same email. |
| Notification indexes | Efficient unread notification loading. |
| ReportHistory indexes | Efficient report history filtering. |
| AuditLog indexes | Efficient admin activity log filtering. |

## Cascade and Delete Behavior

The model uses careful delete behavior:

- Dog images, medical records, status history, and dog search embeddings cascade when a dog is deleted.
- Many important user/shelter relationships use restrict or set null to avoid accidental data loss.
- Adoption request relationships are restricted so historical requests are preserved.
- Dog deletion is blocked in `DogService.DeleteDogAsync` and `DeleteDogForAdminAsync` if adoption request history exists.

## Dog Breed Data Model

The breed system is more flexible than a simple enum:

- `DogBreedId` stores the primary known breed.
- `SecondaryBreedId` stores an optional second likely breed.
- `IsMixedBreed` indicates a mixed-breed dog.
- `CustomBreedName` stores an unlisted breed.
- Legacy `Breed` text is still present for compatibility and display.

Important files:

- `Entities/DogBreed.cs`
- `Data/DogBreedSeedData.cs`
- `Services/DogBreedFormatter.cs`
- `Services/DogBreedInformationFormatter.cs`

Example display:

- `Labrador Retriever`
- `Labrador Retriever Mix`
- `Labrador Retriever x Border Collie Mix`
- `Mixed Breed`
- `Unknown`

## Dog Search Embeddings

The `DogSearchEmbedding` table stores semantic search data:

- `DogId`: dog being indexed.
- `Content`: public-safe text document.
- `ContentHash`: detects whether content changed.
- `EmbeddingJson`: vector stored as JSON.
- `EmbeddingModel`: model used, from `OpenAiSettings`.
- `UpdatedAt`: refresh timestamp.

Related files:

- `Entities/DogSearchEmbedding.cs`
- `Services/DogSearchDocumentService.cs`
- `Services/DogSearchEmbeddingService.cs`
- `Services/OpenAiEmbeddingService.cs`
- `Services/SemanticDogSearchService.cs`
- `Components/Pages/Admin/AdminDogs.razor`

Only searchable/public-safe dog data should be indexed. Tests verify unavailable dogs are removed from the semantic index.

## EF Core Patterns Used

| Pattern | Example |
| --- | --- |
| `Include`/`ThenInclude` for related data | `DogService.GetDogDetailsAsync`, `AdoptionRequestService.GetAllAsync` |
| `AsNoTracking` for read-only queries | Public dog listing, admin lists, request lists |
| Service-level validation before save | `DogService.ValidateAndNormalizeDogAsync`, `ResourceStockService.PrepareResourceAsync` |
| Best-effort side effects after save | Embedding refresh, notification/report logging |
| Migrations for schema changes | `Migrations/*` |
| Seed data | `IdentitySeedData`, `DogBreedSeedData` |

## Why the Database Model Fits the Domain

The model reflects real shelter/adoption workflows:

- Dogs belong to shelters.
- Dogs have images, medical records, status history, food needs, and public behavior descriptions.
- Adopters have profiles, favorites, recently viewed dogs, and adoption requests.
- Adoption requests connect adopter, dog, visit status, and shelter decision.
- Shelters manage both dogs and resources.
- Admins need audit logs, report history, and shelter application review.
- AI/search features store embeddings separately from core dog data.

## Possible Database Questions

| Question | Suggested answer |
| --- | --- |
| Why use EF Core migrations? | Migrations version the schema as the application evolves, for example adding dog breeds, embeddings, secondary breed, and coat color. |
| How do you prevent duplicate pending requests? | The database has a filtered unique index, and the service also checks `HasPendingRequestAsync`. |
| Why is `FavoriteDog` a separate table? | It models a many-to-many relationship between adopters and dogs and can store metadata like `CreatedAt`. |
| Why store embeddings separately? | Embeddings are derived search data, so they belong in `DogSearchEmbeddings`, not directly in the `Dogs` table. |
| Why not use an enum for breeds? | A lookup table allows active/inactive breeds, notes, custom data, and future extension without changing code for every breed. |

