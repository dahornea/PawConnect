# PawConnect Technical Project Context

## 1. Application Overview

PawConnect is a C# ASP.NET Core Blazor Server web application for stray dog adoption and shelter management. The application connects public visitors and adopter users with shelter dogs, while also giving shelters operational tools for managing dog profiles, adoption requests, medical records, dog images, resource stock, and low-stock warnings.

The project is designed as a multi-role, database-backed web system suitable for a bachelor thesis. It demonstrates real application concerns such as authentication, authorization, entity relationships, business rules, email communication, PDF report generation, UI feedback, and automated tests.

The main user groups are:

- Public visitors who browse available or reserved dogs and read adoption success stories.
- Adopters who create profiles, save favorite dogs, track recently viewed dogs, and submit adoption requests.
- Shelter users who manage their own dogs, resources, and adoption request review workflow.
- Admin users who review platform-wide users, shelters, dogs, and adoption requests.

## 2. Technology Stack

- **C#**: Main programming language for domain entities, services, Blazor components, and tests.
- **ASP.NET Core**: Hosts the web application, dependency injection container, middleware pipeline, Identity endpoints, and Razor component infrastructure.
- **Blazor Server**: Provides the interactive UI through server-rendered Razor components with SignalR-backed interactivity.
- **Entity Framework Core**: Handles database persistence through `ApplicationDbContext`, migrations, DbSet mappings, relationships, and LINQ queries.
- **SQL Server**: Production/development database provider configured through the `DefaultConnection` connection string. The default connection points to LocalDB database `PawConnect`.
- **ASP.NET Core Identity**: Handles authentication, Identity users, roles, cookies, password management, and account pages.
- **MudBlazor**: Main UI component library for layout, navigation, cards, tables, forms, dialogs, snackbars, chips, alerts, and icons.
- **Leaflet**: Client-side JavaScript map library used to render read-only shelter maps inside Blazor pages.
- **OpenStreetMap**: Public map tile provider used by the Leaflet shelter map integration. The map feature does not require a paid Google Maps API key.
- **OpenStreetMap Nominatim**: Used through `NominatimGeocodingService` for manual address-to-coordinate lookup and optional coordinate-to-address suggestions. The app does not call it on every keystroke and does not use it for route planning or nearby search.
- **MailKit and MimeKit**: Used by `SmtpEmailService` for SMTP email sending with plain text bodies and optional attachments.
- **Mailtrap SMTP**: The active SMTP-style testing target in configuration. Credentials are configuration-based and should be stored safely outside source control for real use.
- **QuestPDF**: Used by `PdfReportService` to generate PDF reports for adoption requests, adoption status updates, and low-stock resources.
- **xUnit**: Test framework used by the `PawConnect.Tests` project.
- **EF Core InMemory**: Test database provider used by service and integration-style tests.
- **coverlet.collector**: Test coverage collector package included in the test project.

Important project package references in `PawConnect.csproj` include:

- `MailKit`
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Tools`
- `MudBlazor`
- `QuestPDF`

## 3. User Roles and Permissions

The application uses ASP.NET Core Identity roles. The seeded roles are defined in `IdentitySeedData`:

- `Adopter`
- `Shelter`
- `Admin`

### Anonymous/Public Users

Public visitors can:

- View the home page.
- Browse public dog listings at `/dogs`.
- View dog details at `/dogs/{id:int}`.
- View public shelter listings at `/shelters`.
- View shelter profile/details pages at `/shelters/{id:int}`, including location information and a read-only map when coordinates are available.
- View adoption success stories at `/success-stories`.
- Register or log in through Identity account pages.
- Submit a shelter application at `/shelters/apply` if they represent a shelter. This creates a pending shelter registration request instead of directly creating a shelter account.

Public dog visibility is intentionally limited to dogs with status `Available` or `Reserved`. Dogs with status `Adopted` or `InTreatment` are excluded from the public dog list.

### Adopter

Adopter users can:

- Access `/adopter/dashboard`.
- Access and edit `/adopter/profile`.
- Browse public dogs.
- Add and remove favorite dogs.
- View `/favorites`.
- Track recently viewed public-safe dogs.
- Submit adoption requests for available or reserved dogs.
- View and cancel their own pending adoption requests at `/my-adoption-requests`.

The favorite and adoption request services validate that the current user is in the `Adopter` role before allowing adopter-only actions.

### Shelter

Shelter users can:

- Access `/shelter/dashboard`.
- Manage only their own dogs through `/shelter/dogs`, `/shelter/dogs/create`, and `/shelter/dogs/edit/{id:int}`.
- Manage dog image URL records and main image selection for their own non-adopted dogs.
- Manage medical records for their own non-adopted dogs.
- View status history for dogs belonging to their shelter.
- Manage their own resource stock at `/shelter/resources`.
- View and manage adoption requests for dogs belonging to their shelter at `/shelter/adoption-requests`.
- Accept or reject pending adoption requests.
- Edit private internal shelter notes on adoption requests.

Shelter services check the `ShelterId` before changing dog, resource, image, medical record, and adoption request data.

### Admin

Admin users can:

- Access `/admin/dashboard`.
- View and edit basic user profile/contact information at `/admin/users`.
- View and edit basic shelter contact/profile information at `/admin/shelters`.
- View all dogs at `/admin/dogs`.
- View dog status history and success story summaries from admin dog pages.
- Delete dogs when deletion is allowed by business rules.
- View all adoption requests at `/admin/adoption-requests`.

Admin pages are protected with `[Authorize(Roles = "Admin")]`. Advanced role editing, password management, and account deletion are intentionally not implemented in the admin UI.

## 4. Main Functionalities

### Public/Anonymous Features

- **Home page**: Presents the platform, how adoption works, featured public dogs, and shelter-oriented features.
- **Dog browsing**: Public users can browse dogs with public-safe statuses: `Available` and `Reserved`.
- **Dog details**: Public users can view dog information, images, shelter information, medical summary, and food information where available.
- **Shelter listing and details**: Public users can view shelter cards and shelter profile/details pages. Shelter details include address/city information and, when coordinates exist, a read-only Leaflet map using OpenStreetMap tiles. If coordinates are missing, the UI shows a friendly fallback message instead of a broken map.
- **Success stories**: Public page showing adopted dogs, success story text, adoption dates, and shelter information.
- **Authentication**: Users can register and log in through Identity account pages. New registrations are assigned the `Adopter` role by default in the register flow.
- **Shelter applications**: Shelter representatives apply through `/shelters/apply`. Public registration remains adopter-only. Admin and Shelter users are not prompted to apply from public CTAs and cannot submit public shelter applications.

### Adopter Features

- **Adopter profile**: Stores stable adopter information such as full name, profile image URL, city, phone, housing type, yard, pets, children, and dog experience.
- **Favorites**: Adopters can save and remove favorite dogs. Duplicate favorites are prevented.
- **Recently viewed dogs**: When an adopter opens a public-safe dog details page, the app tracks or updates a `RecentlyViewedDog` record.
- **Adoption request questionnaire**: Adoption requests include request-specific fields: reason for adoption, hours alone per day, and additional information.
- **Request tracking**: Adopters can view their own requests and cancel pending requests.
- **Adopter dashboard**: Shows adopter profile context, summary cards, recent adoption requests, favorites preview, and recently viewed dogs.

### Shelter Features

- **Shelter dashboard**: Shows shelter operational summary such as dog counts, pending requests, and low-stock resources.
- **Dog management**: Shelters can create, edit, and delete dogs belonging to their own shelter, subject to deletion rules.
- **Dog images**: Shelters can add image URLs, delete image records, and set one main image.
- **Medical records**: Shelters can add, edit, and delete medical records for their own non-adopted dogs.
- **Read-only adopted dogs**: Adopted dogs are read-only for shelter users in the dog edit flow.
- **Dog status history**: Status changes are recorded and displayed for shelter-owned dogs.
- **Resource stock**: Shelters can manage resources, categories, optional food type, quantity, unit, and low-stock threshold.
- **Low-stock warnings**: Resources with `Quantity <= LowStockThreshold` are shown as low stock and can trigger email/PDF notifications.
- **Adoption request review**: Shelters can view adopter profile summaries, questionnaire answers, dog information, and internal notes.
- **Accept/reject workflow**: Shelters can accept or reject pending requests for their own dogs.
- **Internal notes**: Private notes are visible to shelters and admins, not adopters or public users.
- **Location coordinates**: Shelter records can store optional latitude and longitude coordinates. These coordinates are used to display the shelter location on the public shelter profile page.
- **Address-based coordinate lookup**: Shelter application and admin shelter edit forms can use manual Nominatim lookup to fill optional coordinates from city/address.
- **Editable coordinate map**: Shelter application and admin shelter edit forms allow users to drag the marker or click the map to adjust optional coordinates after lookup or manual entry.
- **Optional reverse geocoding**: After users move the editable marker, the app can suggest an address from the selected coordinates. The suggestion is shown for review and only updates address fields when the user chooses to apply it.

### Admin Features

- **Admin dashboard**: Shows platform-level counts for users, shelters, dogs, and pending adoption requests, plus secondary metrics.
- **Users page**: Lists users, roles, contact fields, and adopter profile availability/basic info. Allows safe editing of email, phone, and full name where available.
- **Shelters page**: Lists shelters and dog counts. Allows editing shelter profile/contact fields.
- **Shelter coordinates**: Admin shelter editing includes optional latitude and longitude fields so public shelter profile maps can be displayed.
- **Shelter request review**: Admins review pending shelter applications at `/admin/shelter-requests`. Accepting a request creates an `ApplicationUser`, assigns the `Shelter` role, and creates a linked `Shelter` profile. Rejecting a request does not create a user or shelter. Accept/reject actions are restricted to Admin users.
- **Dogs page**: Lists all dogs across shelters, including status, shelter, success story indicator, status history access, and allowed delete action.
- **Adoption requests page**: Lists all adoption requests and request/profile details for admin review.

## 5. Domain Model / Entities

### ApplicationUser

Located in `Data/ApplicationUser.cs`. Extends `IdentityUser`.

Important fields and relationships:

- `FullName`: Optional display/profile name.
- `FavoriteDogs`: Favorite dog records linked to this adopter.
- `RecentlyViewedDogs`: Recently viewed dog records linked to this adopter.
- `AdoptionRequests`: Requests submitted by this adopter.
- `DogStatusHistories`: Status history records where this user is the changer.
- `Shelter`: Optional one-to-one shelter profile for shelter accounts.
- `AdopterProfile`: Optional one-to-one adopter profile.

### Shelter

Represents a shelter organization/profile.

Important fields:

- `Name`
- `Description`
- `Address`
- `City`
- `PhoneNumber`
- `Email`
- `Latitude`
- `Longitude`
- `ApplicationUserId`

Relationships:

- One shelter can belong to one `ApplicationUser`.
- One shelter has many `Dogs`.
- One shelter has many `ResourceStocks`.

`Latitude` and `Longitude` are optional coordinate fields used by the public shelter details page to render a read-only map. Existing shelters can still work without coordinates; when either coordinate is missing, the UI shows a location-unavailable fallback instead of rendering a broken map. In coordinate editing forms, these values can be filled manually, by address lookup, by dragging the map marker, or by clicking the editable map. Marker movement can also trigger an optional reverse geocoding suggestion, but the application does not overwrite address/city fields without explicit user confirmation.

### ShelterRegistrationRequest

Represents a public shelter application submitted before a shelter account exists.

Important fields:

- `ShelterName`
- `ContactPersonName`
- `Email`
- `PhoneNumber`
- `City`
- `Address`
- `Description`
- `Website`
- `OpeningHours`
- `ReasonForJoining`
- `Latitude`
- `Longitude`
- `Status`
- `SubmittedAt`
- `ReviewedAt`
- `ReviewedByUserId`
- `CreatedUserId`
- `CreatedShelterId`

Relationships:

- Can optionally reference the admin `ApplicationUser` who reviewed it.
- Can optionally reference the created `Shelter` after approval.

The request starts as `Pending`. If accepted by an admin, the application creates a shelter Identity account, assigns the `Shelter` role, and creates a linked `Shelter` profile. If rejected, no user or shelter profile is created. Coordinates are optional; missing coordinates do not block application submission or approval. Admin and Shelter users are blocked from submitting public shelter applications because admins review requests and shelter users already have active shelter accounts.

### Dog

Central domain entity for adoptable and managed dogs.

Important fields:

- `Name`
- `Breed`
- `Age`
- `AgeYears`
- `AgeMonths`
- `Size`
- `Location`
- `Description`
- `BehaviorDescription`
- `MedicalStatus`
- `Status`
- `SuccessStoryText`
- `AdoptedAt`
- `PreferredFoodTypeId`
- `DailyFoodAmountGrams`
- `ShelterId`

Relationships:

- Belongs to one `Shelter`.
- Optionally references one `FoodType` as preferred food type.
- Has many `DogImages`.
- Has many `MedicalRecords`.
- Has many `AdoptionRequests`.
- Has many `FavoriteDogs`.
- Has many `RecentlyViewedDogs`.
- Has many `DogStatusHistory` records.

The old `Age` integer still exists and is synchronized to `AgeYears` for backward compatibility. UI and formatting use `AgeYears` and `AgeMonths`.

### DogImage

Stores URL-based dog images.

Important fields:

- `DogId`
- `ImageUrl`
- `IsMainImage`

The application does not implement real image uploads. It uses image URLs only. The image service validates HTTP/HTTPS URLs and ensures only one main image when setting a main image.

### MedicalRecord

Stores basic medical notes for a dog.

Important fields:

- `DogId`
- `VaccineName`
- `TreatmentDescription`
- `RecordDate`
- `Notes`

Shelter users can manage medical records for their own non-adopted dogs.

### AdoptionRequest

Represents an adopter's request to adopt a specific dog.

Important fields:

- `DogId`
- `AdopterId`
- `Status`
- `Message`
- `ReasonForAdoption`
- `HoursAlonePerDay`
- `AdditionalInformation`
- `ShelterInternalNotes`
- `CreatedAt`
- `UpdatedAt`

Relationships:

- Belongs to one dog.
- Belongs to one adopter (`ApplicationUser`).

Business role:

- Stores request-specific questionnaire answers.
- Does not duplicate stable adopter profile information such as housing type or city.
- Stores private shelter notes separately in `ShelterInternalNotes`.

### FavoriteDog

Join entity representing an adopter saving a dog as a favorite.

Important fields:

- `DogId`
- `AdopterId`
- `CreatedAt`

There is a unique index on `AdopterId + DogId` to prevent duplicate favorites.

### ResourceStock

Represents shelter inventory/resource stock.

Important fields:

- `Name`
- `Quantity`
- `Unit`
- `LowStockThreshold`
- `LastUpdatedAt`
- `ShelterId`
- `ResourceCategoryId`
- `FoodTypeId`

Low stock is detected when `Quantity <= LowStockThreshold`.

### ResourceCategory

Lookup entity for resource categories.

Seeded categories include:

- Food
- Medicine
- Blankets
- Cleaning Supplies
- Accessories
- Other

### FoodType

Lookup entity used for dog preferred food and food-related resource stock.

Seeded food types include:

- Adult dry food
- Puppy food
- Senior food
- Wet food
- Medical diet food

### AdopterProfile

Stores adopter profile information that helps shelters evaluate adoption requests.

Important fields:

- `ApplicationUserId`
- `FullName`
- `ProfileImageUrl`
- `Address`
- `City`
- `PhoneNumber`
- `HousingType`
- `HasYard`
- `HasOtherPets`
- `HasChildren`
- `ExperienceWithDogs`
- `AdditionalNotes`

There is a unique one-to-one relationship with `ApplicationUser`.

### DogStatusHistory

Tracks dog status changes.

Important fields:

- `DogId`
- `OldStatus`
- `NewStatus`
- `ChangedAt`
- `ChangedByUserId`
- `Notes`

Status history is created when status changes through shelter dog updates and adoption request acceptance. No history record is created when the old and new statuses are the same.

### RecentlyViewedDog

Tracks recently viewed dogs for adopter users.

Important fields:

- `AdopterId`
- `DogId`
- `ViewedAt`

The service updates an existing record instead of creating duplicates. It trims old records and keeps up to 20 recent views per adopter.

### EmailAttachment

Small service model used by the email system.

Important fields:

- `FileName`
- `ContentType`
- `Content`

Used for PDF report attachments.

### EmailSettings

Configuration model bound from `EmailSettings`.

Important fields:

- `SmtpHost`
- `SmtpPort`
- `SmtpUser`
- `SmtpPassword`
- `SenderEmail`
- `SenderName`
- `EnableSsl`

## 6. Enums and Business States

### DogStatus

Values:

- `Available`
- `Reserved`
- `Adopted`
- `InTreatment`

Behavior:

- Public dog browsing only includes `Available` and `Reserved`.
- Adoption requests can only be submitted for `Available` and `Reserved` dogs.
- `Adopted` and `InTreatment` dogs are hidden from public listing.
- Adopted dogs are read-only for shelter users.
- Success stories display dogs with `Adopted` status.
- Accepting an adoption request sets the dog status to `Reserved` and records status history if it changed.

### AdoptionRequestStatus

Values:

- `Pending`
- `Accepted`
- `Rejected`
- `Cancelled`

Behavior:

- Only pending requests can be accepted, rejected, or cancelled.
- Duplicate pending requests from the same adopter for the same dog are blocked by service logic and a filtered database index.
- Accepting a request updates it to `Accepted` and rejects other pending requests for the same dog.
- Rejecting a request updates it to `Rejected`.
- Cancelling a request updates it to `Cancelled`.

### DogSize

Values:

- `Small`
- `Medium`
- `Large`

Used for dog filtering and display.

### HousingType

Values:

- `Apartment`
- `House`
- `Other`

Used in adopter profiles and shown to shelters during adoption request review.

### DogSortOption

Values:

- `NameAsc`
- `NameDesc`
- `AgeAsc`
- `AgeDesc`
- `BreedAsc`
- `LocationAsc`
- `Status`
- `NewestFirst`

Used by public dog search/sorting logic.

### ShelterRegistrationRequestStatus

Values:

- `Pending`
- `Accepted`
- `Rejected`

Behavior:

- Public shelter applications start as `Pending`.
- Admins can accept or reject pending applications at `/admin/shelter-requests`.
- Accepting a request creates the shelter user account, assigns the `Shelter` role, and creates the linked `Shelter` profile.
- Rejecting a request does not create a user or shelter.

## 7. Database and Entity Relationships

`ApplicationDbContext` extends `IdentityDbContext<ApplicationUser, IdentityRole, string>`, so Identity tables and PawConnect domain tables share one EF Core context.

Important relationships:

- One `ApplicationUser` can have one `Shelter`.
- One `ApplicationUser` can have one `AdopterProfile`.
- One `Shelter` has many `Dogs`.
- One `Shelter` has many `ResourceStocks`.
- One `Dog` has many `DogImages`.
- One `Dog` has many `MedicalRecords`.
- One `Dog` has many `AdoptionRequests`.
- One `Dog` has many `FavoriteDogs`.
- One `Dog` has many `RecentlyViewedDogs`.
- One `Dog` has many `DogStatusHistory` records.
- One `ApplicationUser` can have many `AdoptionRequests`.
- One `ApplicationUser` can have many `FavoriteDogs`.
- One `ApplicationUser` can have many `RecentlyViewedDogs`.
- One `ApplicationUser` can be referenced by many `DogStatusHistory` records as `ChangedByUser`.
- One `ApplicationUser` can review many `ShelterRegistrationRequest` records as an admin reviewer.
- One `ResourceCategory` has many `ResourceStocks`.
- One `FoodType` has many `ResourceStocks`.
- One `FoodType` has many `Dogs` through `PreferredFoodType`.

Shelter location coordinates are stored directly on the `Shelter` entity as optional `Latitude` and `Longitude` values. These fields do not create additional relationships and do not affect shelter creation or existing shelter records when they are empty.

Shelter registration requests are stored separately from approved shelter profiles. A pending request can exist before any `ApplicationUser` or `Shelter` is created. Duplicate pending requests for the same email are blocked by service logic and a filtered unique index.

Important delete behavior:

- `DogImage` and `MedicalRecord` cascade when a dog is deleted.
- `DogStatusHistory` cascades when a dog is deleted.
- `Dog` deletion from `Shelter` is restricted at the database relationship level.
- `AdoptionRequest` to `Dog` uses restricted delete, preserving adoption request history.
- `AdoptionRequest` to adopter uses restricted delete.
- `FavoriteDog` to dog and adopter uses restricted delete.
- `RecentlyViewedDog` to dog and adopter uses restricted delete.
- `DogStatusHistory.ChangedByUser` uses `SetNull` if the user is deleted.
- Resource relationships use restricted delete.

Important indexes:

- `FavoriteDog` has a unique index on `AdopterId + DogId`.
- `RecentlyViewedDog` has a unique index on `AdopterId + DogId`.
- `AdoptionRequest` has a filtered unique index on `AdopterId + DogId` for pending requests (`Status = 0`).
- `AdopterProfile` has a unique index on `ApplicationUserId`.

## 8. Architecture and Code Organization

The solution contains:

- `PawConnect.csproj`: Main ASP.NET Core Blazor Server application.
- `PawConnect.Tests/PawConnect.Tests.csproj`: Test project.
- `PawConnect.sln`: Solution file referencing both projects.

Main folders:

- `Components`: Razor components, layouts, pages, shared dialogs, account pages.
- `Components/Layout`: Main layout, sidebar navigation, reconnect modal.
- `Components/Pages`: Public pages plus role-specific Adopter, Shelter, and Admin page folders.
- `Components/Account`: Identity account pages and supporting Identity components.
- `Components/Shared`: Shared UI components and dialogs such as `ShelterMap.razor`, confirmation dialogs, and success story details.
- `Data`: `ApplicationDbContext`, `ApplicationUser`, seed data, and EF Core migrations.
- `Entities`: Domain entities and enums.
- `Services`: Application service interfaces and implementations.
- `Repositories`: Generic repository interface and EF implementation.
- `wwwroot`: Static CSS, JavaScript, favicon, and Leaflet JavaScript interop helpers in `app.js`/`app.css`.
- `docs`: Existing documentation such as database diagram and this technical context file.
- `PawConnect.Tests`: Unit and service-flow tests.

General architecture:

1. Blazor pages/components handle UI rendering and user interaction.
2. Components inject service interfaces such as `IDogService`, `IAdoptionRequestService`, and `IResourceStockService`.
3. Services contain business rules, validation, ownership checks, query includes, and persistence logic.
4. Services use `ApplicationDbContext` directly in most cases.
5. A generic repository exists and is registered, but much of the core logic is implemented in services rather than repository-specific classes.
6. EF Core maps entities to SQL Server tables and handles migrations.
7. ASP.NET Core Identity handles users, roles, authentication cookies, and account pages.

Map-related organization:

- `Components/Shared/ShelterMap.razor`: Reusable Blazor component for displaying one shelter map when coordinates are available.
- `Components/Shared/ShelterMap.razor.css`: Component-level map frame styling.
- `wwwroot/app.js`: Contains `pawConnect.renderShelterMap` and `pawConnect.disposeShelterMap`, which initialize and clean up Leaflet map instances through JavaScript interop.
- `wwwroot/app.css`: Contains global Leaflet sizing/fallback styles and the custom SVG-style shelter marker styling.
- `Components/App.razor`: References Leaflet CSS and JavaScript CDN assets.

Shelter onboarding/geocoding organization:

- `Components/Pages/ShelterApply.razor`: Public shelter application form at `/shelters/apply`.
- `Components/Pages/Admin/AdminShelterRequests.razor`: Admin review page at `/admin/shelter-requests`.
- `Services/ShelterRegistrationRequestService.cs`: Handles application submission, duplicate pending email checks, admin notifications, accept/reject workflow, and creating approved shelter accounts/profiles.
- `Services/NominatimGeocodingService.cs`: Performs manual address-based coordinate lookup and optional reverse coordinate-to-address lookup through OpenStreetMap Nominatim.
- `Services/IGeocodingService.cs`: Interface used by public/admin forms so geocoding can be faked in tests.

## 9. Main Application Flows

### Public Dog Browsing Flow

1. The user opens `/dogs`.
2. `Dogs.razor` loads public dog data through `IDogService`.
3. `DogService.GetAvailableDogsAsync` or `SearchDogsAsync` returns only dogs whose status is `Available` or `Reserved`.
4. The page displays filters, sorting, dog cards, images/placeholders, status chips, and view details actions.
5. Clicking a dog image or View Details navigates to `/dogs/{id:int}`.
6. `DogDetails.razor` loads detailed dog data through `GetDogDetailsAsync`, including shelter, images, medical records, and preferred food type.
7. Public and non-adopter users see only view-safe dog information and login/register prompts where relevant.

### Shelter Location Map Flow

1. A shelter record may store optional `Latitude` and `Longitude` coordinates.
2. A public user opens a shelter profile/details page at `/shelters/{id:int}`.
3. `ShelterDetails.razor` loads public shelter information and passes the shelter coordinates to `ShelterMap.razor`.
4. If both coordinates are available, `ShelterMap.razor` renders a map container and calls the Leaflet JavaScript interop function in `wwwroot/app.js`.
5. Leaflet creates a read-only interactive map centered on the shelter coordinates.
6. OpenStreetMap tiles are loaded as the map background.
7. A marker is placed at the shelter latitude/longitude.
8. The marker popup shows the shelter name and address/city when available.
9. If coordinates are missing, the map component shows a friendly "Map location is not available for this shelter" fallback instead of trying to initialize Leaflet.

The public map is read-only. Address lookup and reverse address suggestions are limited to editable shelter coordinate forms; the app does not implement route planning, distance search, browser geolocation, or automatic typing autocomplete.

### Shelter Registration Request Flow

1. A public shelter representative opens `/shelters/apply`. Anonymous users can submit applications, and logged-in adopters may submit if they are acting as shelter representatives.
2. The public application form collects shelter name, contact person, email, phone, city, address, description, and optional website/opening hours/reason.
3. Latitude and longitude are optional. The user may click "Find coordinates" to run a manual Nominatim lookup from address + city + Romania.
4. If Nominatim returns a result, the form fills `Latitude` and `Longitude` and the editable map marker moves to that location.
5. If the marker needs adjustment, the user can drag the marker or click the map to update `Latitude` and `Longitude`.
6. After the marker is moved, the form may call reverse Nominatim lookup once and show a suggested address panel.
7. The suggested address is optional. The current address/city fields are changed only when the user clicks "Use suggested address".
8. If Nominatim fails, the user can submit without coordinates or enter/set them manually.
9. `ShelterRegistrationRequestService.SubmitRequestAsync` validates the form, blocks Admin/Shelter users from submitting public applications, and blocks duplicate pending applications for the same email.
10. The service saves a `ShelterRegistrationRequest` with `Pending` status before sending any email.
11. The service attempts to notify admin users by email and attach `ShelterRegistrationRequest.pdf`. Email/PDF failure is logged and does not delete or cancel the request.
12. Admins review applications at `/admin/shelter-requests`.
13. Accepting a pending request is admin-only and creates an `ApplicationUser`, assigns the `Shelter` role, creates a linked `Shelter` profile, and copies optional coordinates when present.
14. Rejecting a pending request is admin-only, marks it as `Rejected`, and does not create a user or shelter profile.

### Adoption Request Flow

1. An authenticated adopter opens a public-safe dog details page.
2. The UI shows adopter-only actions such as favorite and submit adoption request.
3. The adoption request form collects request-specific questionnaire data:
   - Reason for adoption.
   - Hours alone per day.
   - Additional information.
4. `AdoptionRequestService.CreateRequestAsync` verifies:
   - The user is an adopter.
   - The dog exists.
   - The dog is not `Adopted` or `InTreatment`.
   - The adopter does not already have a pending request for the dog.
   - The questionnaire is valid.
5. The service creates a pending `AdoptionRequest`.
6. The service attempts to notify the owning shelter by email and attach `AdoptionRequestReport.pdf`.
7. Shelter users review requests at `/shelter/adoption-requests`.
8. A shelter can accept or reject pending requests only for its own dogs.
9. Accepting a request:
   - Sets request status to `Accepted`.
   - Sets dog status to `Reserved`.
   - Creates dog status history if the dog status changed.
   - Rejects other pending requests for the same dog.
   - Sends adopter notification with `AdoptionStatusReport.pdf`.
10. Rejecting a request:
   - Sets request status to `Rejected`.
   - Sends adopter notification with `AdoptionStatusReport.pdf`.
11. Adopters can cancel their own pending requests from `/my-adoption-requests`.

### Shelter Dog Management Flow

1. A shelter user opens `/shelter/dogs`.
2. The page resolves the current shelter through `IShelterService.GetShelterForUserAsync`.
3. `IDogService.GetDogsForShelterAsync` loads only dogs belonging to that shelter.
4. The shelter can create a dog at `/shelter/dogs/create`.
5. `DogService.CreateDogAsync(dog, shelterId)` validates required fields, age, daily food amount, and assigns the shelter automatically.
6. Optional image URLs can be added through `IDogImageService`.
7. The shelter can edit a dog at `/shelter/dogs/edit/{id:int}`.
8. `DogService.UpdateDogAsync` checks shelter ownership, validates fields, blocks edits to adopted dogs, and records status history when status changes.
9. Dog images can be added, set as main image, or deleted for non-adopted dogs.
10. Medical records can be added, edited, or deleted for non-adopted dogs.
11. Dog deletion is allowed only when there is no adoption request history.
12. Favorites and recently viewed records are removed safely when deleting a dog without adoption requests.

### Resource Stock Flow

1. A shelter user opens `/shelter/resources`.
2. The page resolves the current shelter.
3. `ResourceStockService.GetResourcesForShelterAsync` returns only that shelter's resources.
4. The shelter creates or updates a resource with name, category, quantity, unit, threshold, and optional food type.
5. `ResourceStockService` validates required fields and ownership.
6. If the selected category is not `Food`, `FoodTypeId` is cleared.
7. `LastUpdatedAt` is updated on create/update.
8. If `Quantity <= LowStockThreshold`, the resource is treated as low stock.
9. The service attempts to notify the shelter by email with `LowStockResourceReport.pdf`.

### Email and PDF Report Flow

1. A business service completes the main database action first.
2. The service tries to generate a PDF attachment through `IPdfReportService`.
3. If PDF generation succeeds, it creates an `EmailAttachment` with content type `application/pdf`.
4. The service calls `IEmailService.SendEmailAsync`.
5. `SmtpEmailService` builds a MimeKit email, adds attachments if provided, and sends through MailKit SMTP.
6. Email or PDF failures are logged as warnings and do not roll back the main database action.

### Admin Management Flow

1. Admin users access `/admin/dashboard`.
2. The dashboard loads counts from Identity, shelter, dog, adoption request, and resource services.
3. Admin users can navigate to:
   - `/admin/users`
   - `/admin/shelters`
   - `/admin/dogs`
   - `/admin/adoption-requests`
4. Admin user and shelter pages allow editing basic profile/contact fields.
5. Admin dog page lists dogs from all shelters and supports viewing status history, success story details, and deleting dogs when deletion rules allow.
6. Admin adoption request page displays request data, including questionnaire and private/internal shelter notes for review.

## 10. Email Notification System

Email abstractions:

- `IEmailService`
- `SmtpEmailService`
- `MockEmailService`
- `EmailSettings`
- `EmailAttachment`

`Program.cs` registers:

- `IEmailService` as `SmtpEmailService`.
- `EmailSettings` through `builder.Configuration.GetSection("EmailSettings")`.

The SMTP implementation:

- Uses MailKit `SmtpClient`.
- Uses MimeKit `MimeMessage`, `TextPart`, and `BodyBuilder`.
- Sends plain text email bodies.
- Supports optional attachments.
- Uses `SecureSocketOptions.StartTls` when `EnableSsl` is true.
- Logs and returns if the recipient is empty.
- Logs and returns if SMTP host or sender email is incomplete.
- Catches exceptions and logs warnings instead of throwing them back to the business flow.

Configured email events:

- New adoption request submitted: sends shelter notification.
- Adoption request accepted: sends adopter notification.
- Adoption request rejected: sends adopter notification.
- Resource stock created/updated and low stock: sends shelter notification.

The current `appsettings.json` uses Mailtrap-style sandbox settings with placeholder password text. Real credentials should be stored in `appsettings.Development.json`, .NET User Secrets, or environment variables.

## 11. PDF Report System

PDF abstractions:

- `IPdfReportService`
- `PdfReportService`

`PdfReportService` uses QuestPDF and sets the QuestPDF license to `LicenseType.Community`.

PDF reports generated:

### Adoption Request Report

Method:

- `GenerateAdoptionRequestReportAsync(int adoptionRequestId)`

Includes:

- Dog name, breed, formatted age, size, current status, shelter name.
- Adopter full name, email, phone, city, housing type, yard/pets/children flags, dog experience.
- Request reason, hours alone per day, additional information, request date.

Attached when an adopter submits a new adoption request.

### Adoption Status Report

Method:

- `GenerateAdoptionStatusReportAsync(int adoptionRequestId)`

Includes:

- Friendly summary.
- Dog name and new request status.
- Status update date.
- Dog information.
- Shelter contact information.
- Next steps depending on accepted/rejected status.

Attached when a shelter accepts or rejects a request.

### Low Stock Resource Report

Method:

- `GenerateLowStockResourceReportAsync(int resourceStockId)`

Includes:

- Shelter name, city, email.
- Resource name, category, optional food type, quantity, unit, low-stock threshold.
- Recommendation to review and update inventory.

Attached when a resource reaches low stock after create/update.

### Shelter Registration Request Report

Method:

- `GenerateShelterRegistrationRequestReportAsync(int shelterRegistrationRequestId)`

Includes:

- Shelter name.
- Contact person.
- Email and phone.
- City and address.
- Description.
- Optional website, opening hours, and reason for joining.
- Optional latitude/longitude.
- Submitted date and request status.

Attached when a public shelter application is submitted and admin notification email is sent.

The reports use a clean text/table layout with PawConnect header, section headings, rows, and generated-date footer. The code does not implement charts, graphs, scheduled reports, or database storage of generated PDFs.

## 11.5 Shelter Map Integration

The application includes a simple third-party map integration for public shelter profile pages.

Implemented behavior:

- Leaflet handles client-side interactive map rendering.
- OpenStreetMap provides the map tiles.
- No Google Maps API key or paid maps API is required.
- Public shelter maps are read-only. Shelter application/admin coordinate forms use an editable mode for coordinate adjustment.
- Shelter coordinates are stored in the database as optional `Latitude` and `Longitude` fields on the `Shelter` entity.
- Address information is the primary location input; coordinates can be derived through a manual OpenStreetMap Nominatim lookup, entered manually, or adjusted through the editable map marker.
- Reverse Nominatim lookup can suggest an address after a marker click/drag. The app displays the suggestion and waits for the user to apply it instead of overwriting address fields automatically.
- `ShelterMap.razor` receives the coordinates and passes them to JavaScript interop for Leaflet initialization.
- In editable form mode, `ShelterMap.razor` exposes latitude/longitude callbacks so marker dragging and map clicks update the bound coordinate fields and allow the form to request an optional address suggestion.
- Marker popups show the shelter name and address/city when available.
- The map uses a custom inline SVG-style marker, so it does not depend on Leaflet marker PNG files loading correctly.

Not implemented:

- Automatic address geocoding while typing.
- Automatic address replacement after moving the marker.
- Route planning or directions.
- "Near me" search.
- Distance calculations or distance-based filtering.
- Browser geolocation or user location tracking.

## 12. UI/UX Design

The UI is implemented with MudBlazor components and custom CSS files next to many Razor pages.

Important UI structure:

- `MainLayout.razor` defines the MudBlazor theme, app bar, drawer, main content container, dialog provider, snackbar provider, and popover provider.
- `NavMenu.razor` defines role-based sidebar navigation.
- `ShelterMap.razor` defines a reusable responsive map component for shelter profile/details pages.
- `ConfirmationDialog.razor` provides a reusable confirmation dialog.
- `SuccessStoryDetailsDialog.razor` provides an admin success story preview dialog.

Visual style:

- Main primary color: green/teal (`#2F6F5E`).
- Warm accent color: orange (`#C9812D`).
- Soft off-white/green-tinted application background.
- Rounded cards and surfaces.
- Subtle borders and shadows.
- MudBlazor icons for navigation and actions.

Navigation:

- Public links: Home, Dogs, Shelters, Success Stories.
- Adopter-only links: Adopter Dashboard, My Profile, Browse Dogs, Favorite Dogs, My Adoption Requests.
- Shelter-only links: Shelter Dashboard, Manage Dogs, Adoption Requests, Resources.
- Admin-only links: Admin Dashboard, Users, Shelters, Dogs, Adoption Requests.

Top bar:

- Brand icon/text links to home.
- Logged-in users show a compact display with role/profile-aware name and optional email.
- Logout uses a confirmation dialog before submitting the existing Identity logout form.

Common UI patterns:

- MudCards for dogs, dashboards, summaries, feature areas.
- MudTables for admin and management lists.
- MudForms and MudTextField/MudSelect/MudNumericField for forms.
- MudDialog for confirmations and details.
- MudSnackbar for user feedback.
- MudAlert for empty, warning, and error states.
- Status chips for dog and adoption request states.
- Shelter profile pages include a Location card that displays a responsive read-only Leaflet/OpenStreetMap map with rounded corners when coordinates are available.
- Shelter application and admin shelter edit forms use editable map mode so users can click to place the pin or drag the marker to refine coordinates, then optionally apply a suggested address found from the selected location.
- Shelter map fallback states use MudBlazor alerts/messages when coordinates are unavailable, keeping the page usable instead of showing an empty or broken map.

## 13. Validation and Error Handling

Validation is implemented at several levels:

- Data annotations on entities, such as `[Required]`, `[StringLength]`, `[Range]`, `[EmailAddress]`, `[Phone]`, and `[Url]`.
- Service-level validation for business rules and ownership.
- MudBlazor form validation and UI checks in pages.
- Database indexes for uniqueness, such as pending adoption requests and favorites.

Examples of service validation:

- `DogService` validates dog name, breed, location, age years/months, and daily food amount.
- `DogImageService` validates image URLs and shelter ownership.
- `MedicalRecordService` validates record date and shelter ownership.
- `AdoptionRequestService` validates adopter role, dog status, duplicate pending request, pending-only status changes, and questionnaire fields.
- `FavoriteDogService` validates adopter role and public-safe dog status.
- `ResourceStockService` validates name, category, quantity, threshold, unit, and shelter ownership.
- `AdopterProfileService` validates full name, city, phone number, and profile image URL.
- `ShelterService` validates shelter profile contact fields, duplicate shelter email, and optional latitude/longitude ranges.
- `ShelterRegistrationRequestService` validates shelter application required fields, duplicate pending emails, optional latitude/longitude ranges, role restrictions for public submission, and admin-only pending accept/reject behavior.
- `NominatimGeocodingService` returns `null` for missing/failed forward or reverse geocoding results so forms can show friendly messages without blocking application submission.

Error handling patterns:

- UI pages generally show user-friendly MudSnackbar or MudAlert messages.
- Missing shelter coordinates do not break shelter profile pages; the map component displays a friendly fallback message instead.
- Leaflet map initialization is handled through JavaScript interop with unique map element IDs, explicit map sizing, resize invalidation, marker drag/click callbacks in editable mode, and disposal to avoid broken map rendering during Blazor Server navigation.
- Expected business rule violations use clear `InvalidOperationException` messages.
- Email and PDF failures are logged and do not fail the main business action.
- Confirmation dialogs are used before important/destructive actions such as delete, cancel, accept, reject, and logout.
- `UseStatusCodePagesWithReExecute("/not-found")` provides a friendly not-found route.
- `AuthorizeRouteView` redirects unauthorized route access through the account redirect component.

Important delete restrictions:

- Dogs with adoption request history cannot be hard deleted.
- Favorites and recently viewed records do not block dog deletion and are removed when deleting a dog that has no adoption requests.
- Adoption requests are preserved when dog deletion is blocked.

## 14. Testing

The solution includes a test project:

- `PawConnect.Tests/PawConnect.Tests.csproj`

Test framework and packages:

- xUnit
- xUnit Visual Studio runner
- EF Core InMemory provider
- coverlet collector

Testing approach:

- Tests use isolated EF Core InMemory databases.
- `TestDbContextFactory` creates fresh database contexts and seeds roles, users, shelters, lookup data, and helper dogs.
- `TestEmailService` captures sent emails in memory.
- `TestPdfReportService` returns fake PDF bytes for notification-flow tests.
- Tests do not require SQL Server, Mailtrap, a running web server, or browser UI automation.

Current test organization:

- `DogServiceTests`
- `DogImageServiceTests`
- `AdoptionRequestServiceTests`
- `FavoriteDogServiceTests`
- `ResourceStockServiceTests`
- `ShelterRegistrationRequestServiceTests`
- `NominatimGeocodingServiceTests`
- `PdfReportServiceTests`
- `Integration/ServiceFlowIntegrationTests`
- `Helpers/TestDbContextFactory`
- `Helpers/TestDoubles`

Current test coverage includes:

- Public dog visibility includes `Available` and `Reserved`, excludes `Adopted` and `InTreatment`.
- Dog deletion with no relationships.
- Dog deletion with favorites but no adoption requests.
- Dog deletion blocked by adoption request history.
- Favorites are not duplicated.
- Favorites are private to each adopter.
- Non-adopters cannot favorite dogs.
- Dog image add/delete and shelter ownership checks.
- Dog age formatting, including puppies such as `7 months old`.
- Invalid dog age months validation.
- Adoption request creation and questionnaire persistence.
- Non-adopters blocked from submitting adoption requests.
- Duplicate pending adoption requests blocked.
- Shelter accept/reject behavior.
- Shelter ownership restrictions for adoption requests.
- Cancellation rules.
- Dog status history creation when accepting a request changes dog status.
- No duplicate status history when status does not change.
- Resource create/update/delete and ownership.
- Low-stock detection and non-low-stock detection.
- Clearing `FoodTypeId` for non-food resources.
- Shelter registration request submission with optional coordinates.
- Anonymous shelter registration request submission.
- Blocking Admin and Shelter users from submitting public shelter applications.
- Duplicate pending shelter registration request blocking.
- Admin notification email/PDF attachment capture for shelter applications.
- Admin accept/reject shelter application behavior.
- Blocking non-admin users from accepting or rejecting shelter applications.
- Creating shelter users, assigning the `Shelter` role, and linked shelter profile creation after approval.
- Nominatim geocoding response parsing and failure handling using fake HTTP responses.
- PDF report generation returns non-empty bytes.
- Integration-style service flows for public visibility, favorites/deletion, adoption notifications, dog image/age, resources, and fake PDF/email attachment behavior.

The README documents running tests with:

```bash
dotnet test
```

The previously observed test suite contained 42 passing tests. This document does not rerun tests; it records the current test setup found in the codebase.

## 15. Security and Authorization

Identity and authentication:

- ASP.NET Core Identity is configured with cookie authentication.
- `ApplicationUser` extends `IdentityUser`.
- Roles are configured through `AddRoles<IdentityRole>()`.
- Identity endpoints are mapped through `MapAdditionalIdentityEndpoints()`.
- Confirmed accounts are not required for sign-in (`RequireConfirmedAccount = false`).

Role-based authorization:

- Role-protected pages use `[Authorize(Roles = "...")]`.
- Sidebar navigation uses `AuthorizeView` with role filters.
- Public pages remain accessible without authentication.
- Public registration creates adopter accounts only; shelter accounts are created after admin approval of a shelter registration request.
- Admin users review shelter applications from `/admin/shelter-requests` and are not allowed to submit public shelter applications.
- Shelter users already have active shelter accounts and are not allowed to submit public shelter applications.
- Accepting/rejecting shelter applications is enforced as an Admin-only service operation, not only hidden in the UI.
- Unauthorized route access is handled through `AuthorizeRouteView` and the account redirect component.

Service-level security/ownership checks:

- Adoption request creation requires an adopter account.
- Favorite management requires an adopter account.
- Shelter dog management checks dog `ShelterId`.
- Shelter image and medical record management checks dog ownership.
- Shelter resource management checks resource `ShelterId`.
- Shelter adoption request review checks the request dog belongs to the shelter.
- Adopter request cancellation checks request ownership.
- Recently viewed data is keyed by adopter ID and returned only for that adopter.
- Admin pages are role-protected and can access platform-wide data.

Sensitive operations:

- Password changes and security-sensitive Identity fields are not exposed through admin profile editing.
- User role editing is not implemented in the admin UI.
- Real SMTP credentials are not hardcoded in code and should be supplied through configuration/user secrets/environment variables.

## 16. Thesis-Relevant Contributions

PawConnect is suitable for a bachelor thesis because it demonstrates:

- A real-world social/operational problem: stray dog adoption and shelter coordination.
- A multi-role platform with public, adopter, shelter, and admin workflows.
- A relational database model with meaningful domain entities and relationships.
- Identity-based authentication and role-based authorization.
- Business workflows such as adoption requests, dog status transitions, shelter resource management, and low-stock detection.
- Approval-based shelter onboarding with admin review.
- Third-party map/location integration using Leaflet, OpenStreetMap, and manual Nominatim geocoding.
- Service-layer validation and ownership checks.
- Email communication using SMTP.
- PDF report generation with structured report content.
- MudBlazor UI with dashboards, forms, tables, dialogs, cards, snackbars, and empty/loading states.
- Automated service/domain tests and integration-style flow tests.
- Seed/demo data for presentation and testing.

## 17. Limitations and Future Improvements

Based on the current codebase, realistic future improvements include:

- Real image file upload instead of URL-only dog images.
- Cloud or local file storage for uploaded images.
- Address-based geocoding from city/street/number to stored coordinates.
- "Nearby shelters" search.
- Route/directions integration.
- Distance-based shelter or dog filtering.
- Better mobile map interactions.
- Map-based dog and shelter location search.
- More advanced dog recommendations based on adopter profile and preferences.
- Full shelter messaging/chat between adopters and shelters.
- Scheduled reminder emails or scheduled reports.
- More detailed medical record categories and vaccination schedules.
- Adoption finalization workflow beyond `Reserved`.
- Archive/inactive status for dogs instead of relying only on current statuses.
- Advanced admin moderation and audit logs.
- Production deployment configuration.
- More complete mobile responsiveness testing.
- Browser-based end-to-end tests if the thesis scope later requires UI automation.

Features intentionally not present:

- No real image upload.
- No external login providers.
- No passkey-focused custom login flow beyond template account support.
- No complex analytics/charts.
- No scheduled background jobs.
- No geocoding, route planning, distance search, or browser geolocation.
- No public comments, likes, or social features for success stories.
- No complex role management from admin UI.

## 18. Suggested Thesis Chapter Mapping

### Introduction

Describe the stray dog adoption problem, shelter coordination needs, and the motivation for PawConnect.

### Requirements Analysis

Map requirements by role:

- Public dog discovery.
- Adopter profile, favorites, recently viewed dogs, adoption requests.
- Shelter dog/resource/request management.
- Admin review/moderation.
- Email and PDF communication.

### Technologies Used

Explain ASP.NET Core, Blazor Server, EF Core, SQL Server, Identity, MudBlazor, Leaflet/OpenStreetMap, MailKit/MimeKit, QuestPDF, and xUnit.

### System Design

Describe the multi-role architecture, service layer, Identity integration, and major flows.

### Database Design

Use the entity descriptions and relationship notes from this document. Include diagrams if available, such as the existing database diagram documentation.

### Implementation

Discuss the Blazor pages, services, business rules, dashboards, dog management, adoption request flow, resource stock flow, shelter map integration, email notifications, and PDF reports.

### Testing

Summarize unit and integration-style tests, test helpers, fake email/PDF services, and important business rules covered.

### Conclusions and Future Work

Evaluate project outcomes and list future improvements such as real uploads, deployment, geocoding/directions, messaging, scheduled reports, and advanced recommendations.

## 19. Important Notes for Thesis Writing

- The application uses role names exactly as `Adopter`, `Shelter`, and `Admin`.
- Public dog visibility is based on `DogStatus.Available` and `DogStatus.Reserved`.
- `Adopted` dogs are used for success stories, not public adoption listing.
- `InTreatment` dogs are hidden from public adoption listing.
- `AdoptionRequest` stores only request-specific questionnaire answers; stable adopter information is stored in `AdopterProfile`.
- Public registration is adopter-only; shelter representatives submit approval-based applications at `/shelters/apply`.
- Admins review shelter applications at `/admin/shelter-requests`; only accepted applications create shelter users/profiles.
- Apply-as-shelter CTAs are intended for anonymous/public and general public-facing use, not for Admin or Shelter users.
- `ShelterInternalNotes` are private to shelter/admin views and are not shown to adopters or public users.
- `Age` still exists on `Dog`, but the current age display uses `AgeYears` and `AgeMonths` through `DogAgeFormatter`.
- Dog deletion is blocked by adoption request history, not favorites.
- Favorites and recently viewed records are removed when deleting a dog with no adoption requests.
- Dog status history records track only status changes, not all dog edits.
- Shelter maps use optional stored coordinates on `Shelter`; public maps are read-only and coordinate forms can use manual Nominatim lookup plus optional reverse address suggestions.
- Nominatim geocoding is manual/user-triggered only, not autocomplete, continuous dragging lookup, or automatic background geocoding.
- Email/PDF failures are intentionally non-blocking and logged.
- PDF reports are generated dynamically and are not stored in the database.
- Demo seed data includes test users, demo shelters with approximate Cluj-Napoca coordinates, lookup data, dog records, images, medical records, resources, adopter profile data, and adopted success story examples.
- The README currently still uses some "skeleton/planned features" wording even though many features are implemented; thesis writing should rely on the current code and this context document for accuracy.
- The test suite is service/domain-focused, not browser UI-focused.
- The generic repository exists, but core services mainly use `ApplicationDbContext` directly for richer business queries and ownership checks.
- Real SMTP credentials should not be committed. Mailtrap sandbox configuration is suitable for development/testing.
