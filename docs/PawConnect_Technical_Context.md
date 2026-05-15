# PawConnect Technical Project Context

## 1. Application Overview

PawConnect is a C# ASP.NET Core Blazor Server web application for stray dog adoption and shelter management. The application connects public visitors and adopter users with shelter dogs, while also giving shelters operational tools for managing dog profiles, adoption requests, medical records, dog images, resource stock, and low-stock warnings. It also includes persistent in-app notifications and report history metadata so important events and generated reports are visible inside the platform as well as through email/snackbar feedback.

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
- **OpenStreetMap Nominatim**: Used through `NominatimGeocodingService` for manual address-to-coordinate lookup. The app does not call it on every keystroke and does not use it for route planning or nearby search.
- **MailKit and MimeKit**: Used by `SmtpEmailService` for SMTP email sending with plain text bodies, branded HTML bodies, optional attachments, and raw MIME calendar invite parts.
- **Generic SMTP / local SMTP catcher**: Email delivery can be configured through `EmailSettings`. Development can use a local SMTP catcher such as Mailpit or smtp4dev so emails are captured locally and are not delivered to real users.
- **QuestPDF**: Used by `PdfReportService` and admin export logic to generate PDF reports for adoption requests, adoption status updates, low-stock resources, shelter registration requests, shelter summary reports, and formatted admin exports.
- **Quartz.NET**: Used for in-process scheduled shelter summary report jobs and adoption visit reminder checks. The implementation uses simple in-memory scheduling, not an external cron job, Hangfire, a Quartz dashboard, or a persistent Quartz job store.
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
- `Quartz.Extensions.Hosting`

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
- View private in-app notifications for adoption request updates.
- Choose a preferred shelter visit date/time when submitting an adoption request.

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
- Confirm adopter-selected shelter visits, reject requests, and later mark a confirmed request/dog as adopted after the visit.
- Edit private internal shelter notes on adoption requests.
- Export their own shelter dogs, adoption requests, and resource stock as CSV/PDF where available.
- Review recent report/export metadata for their own shelter on the Shelter Dashboard.
- View private in-app notifications for new adoption requests, cancelled requests, low-stock resources, and sent shelter summary reports.

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
- View private admin notifications such as newly submitted shelter applications.

Admin pages are protected with `[Authorize(Roles = "Admin")]`. Advanced role editing, password management, and account deletion are intentionally not implemented in the admin UI.

## 4. Main Functionalities

### Public/Anonymous Features

- **Home page**: Presents the platform, how adoption works, featured public dogs, and shelter-oriented features.
- **Dog browsing**: Public users can browse dogs with public-safe statuses: `Available` and `Reserved`, see compact shelter name/city information on each dog card, and filter the public dog list by shelter.
- **Dog details**: Public users can view dog information, images, shelter information, medical summary, and food information where available.
- **Shelter listing and details**: Public users can browse approved shelter cards with public contact/location information, public dog counts, and search by shelter name/city/address. The listing includes a compact application CTA pointing to `/shelters/apply`, hidden for Admin and Shelter roles. Shelter details include address/city information and, when coordinates exist, a read-only Leaflet map using OpenStreetMap tiles. If coordinates are missing, the UI shows a friendly fallback message instead of a broken map. The Location card also includes an external "Open in Google Maps" link for easier navigation.
- **Success stories**: Public page showing adopted dogs, success story text, adoption dates, and shelter information.
- **Authentication**: Users can register and log in through Identity account pages. New registrations are assigned the `Adopter` role by default in the register flow.
- **Shelter applications**: Shelter representatives apply through `/shelters/apply`. Public registration remains adopter-only. Admin and Shelter users are not prompted to apply from public CTAs and cannot submit public shelter applications.

### Adopter Features

- **Adopter profile**: Stores stable adopter information such as full name, profile image URL, city, phone, housing type, yard, pets, children, and dog experience.
- **Favorites**: Adopters can save and remove favorite dogs. Duplicate favorites are prevented.
- **Recently viewed dogs**: When an adopter opens a public-safe dog details page, the app tracks or updates a `RecentlyViewedDog` record.
- **Adoption request questionnaire and visit time**: Adoption requests include request-specific fields: reason for adoption, hours alone per day, additional information, and the adopter's preferred visit date/time.
- **Visit scheduling**: Preferred visit times are validated against the shelter's configured visiting hours. Shelter users confirm visits before any final adoption decision. Confirmed visits send adopter email/in-app notifications and an `.ics` calendar attachment; Google Calendar API/OAuth is intentionally not used.
- **Visit reminders**: Optional Quartz reminders send adopter emails and in-app notifications about 24 hours before confirmed visits. `VisitReminderSentAt` prevents duplicate reminder sends.
- **Request tracking**: Adopters can view their own requests and cancel pending requests.
- **In-app notifications**: Adopters receive persistent notifications when visits are confirmed and when adoption requests are finalized or rejected.
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
- **Shelter summary reports**: Shelters can manually email themselves a PDF summary from `/shelter/dashboard`. Automatic scheduled sending is handled by Quartz.NET when enabled in configuration.
- **Adoption request review**: Shelters can view adopter profile summaries, questionnaire answers, dog information, and internal notes.
- **Visit confirmation and final adoption workflow**: Shelters confirm visits for pending requests, which reserves the dog but does not mark it adopted. After the visit, shelters can mark the request/dog as adopted or reject/close the request.
- **Internal notes**: Private notes are visible to shelters and admins, not adopters or public users.
- **Shelter exports**: Shelter users can export only their own operational data. `/shelter/dogs` provides CSV export, while `/shelter/adoption-requests` and `/shelter/resources` provide CSV and formatted PDF exports.
- **Shelter CSV imports**: Shelter users can import resource stock and dog records from `.csv` files through a preview-and-validate workflow. Imported rows are scoped to the authenticated shelter.
- **Location coordinates**: Shelter records can store optional latitude and longitude coordinates. These coordinates are used to display the shelter location on the public shelter profile page.
- **Address-based coordinate lookup**: Shelter application and admin shelter edit forms can use manual Nominatim lookup to fill optional coordinates from city/address.
- **Editable coordinate map**: The public shelter application and admin shelter edit forms hide raw latitude/longitude inputs in normal use and use the editable map marker as the location editor.
- **Address update from pin**: When a marker location exists, users can explicitly update the address/city fields from the selected pin. Moving the marker alone does not overwrite address fields.
- **In-app notifications**: Shelter users receive notifications for new/cancelled adoption requests, low-stock resources, and shelter summary reports.

### Admin Features

- **Admin dashboard**: Shows platform-level counts for users, shelters, dogs, and pending adoption requests, plus secondary metrics.
- **Users page**: Lists users, roles, contact fields, and adopter profile availability/basic info. Allows safe editing of email, phone, and full name where available.
- **Shelters page**: Lists shelters and dog counts. Allows editing shelter profile/contact fields.
- **Shelter coordinates**: Admin shelter editing stores optional latitude and longitude values internally so public shelter profile maps can be displayed.
- **Shelter request review**: Admins review pending shelter applications at `/admin/shelter-requests`. Accepting a request creates an `ApplicationUser`, assigns the `Shelter` role, and creates a linked `Shelter` profile. Rejecting a request does not create a user or shelter. Accept/reject actions are restricted to Admin users.
- **Admin shelter request import**: Admins can import shelter application CSV rows from `/admin/shelters`. Imported rows become pending `ShelterRegistrationRequest` records and still require approval before any user account or shelter profile is created.
- **Dogs page**: Lists all dogs across shelters, including status, shelter, success story indicator, status history access, and allowed delete action.
- **Adoption requests page**: Lists all adoption requests and request/profile details for admin review.
- **Admin exports**: Admin pages provide CSV downloads for users, shelters, dogs, adoption requests, and shelter applications. Adoption request and shelter application pages also provide formatted PDF summary exports.
- **Report history**: Admins review generated/sent report and export metadata at `/admin/report-history`.
- **Activity log**: Admins can review important user and system actions at `/admin/activity-log`.
- **In-app notifications**: Admins receive private notifications for important admin-facing events such as new shelter applications and successful shelter request CSV imports.

## 5. Domain Model / Entities

### ApplicationUser

Located in `Data/ApplicationUser.cs`. Extends `IdentityUser`.

Important fields and relationships:

- `FullName`: Optional display/profile name.
- `FavoriteDogs`: Favorite dog records linked to this adopter.
- `RecentlyViewedDogs`: Recently viewed dog records linked to this adopter.
- `AdoptionRequests`: Requests submitted by this adopter.
- `DogStatusHistories`: Status history records where this user is the changer.
- `Notifications`: Private in-app notifications belonging to this user.
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
- `VisitStartTime`
- `VisitEndTime`
- `VisitsAllowedMonday` through `VisitsAllowedSunday`

Relationships:

- One shelter can belong to one `ApplicationUser`.
- One shelter has many `Dogs`.
- One shelter has many `ResourceStocks`.

`Latitude` and `Longitude` are optional internal coordinate fields used by the public shelter details page to render a read-only map. Existing shelters can still work without coordinates; when either coordinate is missing, the UI shows a location-unavailable fallback instead of rendering a broken map. In the public shelter application form and normal admin shelter edit form, these raw numeric fields are hidden and are updated internally through address lookup, marker dragging, or map clicks. Marker movement does not overwrite address/city fields automatically.

Shelter visiting hours are stored directly on the shelter profile as one visit time range plus allowed weekdays. The default/demo schedule is Monday-Friday, 10:00-17:00. Adopter-selected visit times are validated against these fields when adoption requests are submitted and confirmed.

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
- `PreferredVisitDateTime`
- `VisitStatus`
- `VisitConfirmedAt`
- `VisitReminderSentAt`
- `VisitNotes`
- `VisitConfirmedByUserId`
- `CreatedAt`
- `UpdatedAt`

Relationships:

- Belongs to one dog.
- Belongs to one adopter (`ApplicationUser`).

Business role:

- Stores request-specific questionnaire answers.
- Stores the adopter's preferred shelter visit time and current visit status.
- Stores `VisitReminderSentAt` after the automatic 24-hour visit reminder has been sent.
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
- `IsCalendarInvite`
- `CalendarMethod`
- `IncludeAsAttachmentFallback`

Used for PDF report attachments and generated iCalendar `.ics` visit invitations. Calendar invite attachments are also emitted as inline `text/calendar; method=REQUEST` MIME parts by `SmtpEmailService` so compatible email clients can recognize the invite directly.

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
- `OpenLocalInboxOnStartup`
- `LocalInboxUrl`

The default development configuration uses smtp4dev on `localhost:2525` with empty credentials and `EnableSsl = false`. Mailpit is also supported with `localhost:1025`. Real SMTP providers can still be configured later through the same settings, with credentials supplied through User Secrets or environment variables.

### AuditLog

Represents a lightweight activity record for important user and system actions.

Important fields:

- `Action`
- `EntityName`
- `EntityId`
- `Description`
- `UserId`
- `UserEmail`
- `UserRole`
- `CreatedAt`
- `IpAddress`
- `AdditionalData`

Business role:

- Provides traceability for actions such as dog changes, adoption request status changes, shelter application review, resource changes, reports, and exports.
- Stores only useful accountability metadata, not full entity snapshots or sensitive security values.
- Background actions can be logged with `UserEmail = "System"` and `UserRole = "System"`.

### Notification

Represents a private in-app notification for a single authenticated user.

Important fields:

- `UserId`
- `Title`
- `Message`
- `Category`
- `Type`
- `RelatedEntityName`
- `RelatedEntityId`
- `Link`
- `IsRead`
- `CreatedAt`
- `ReadAt`

Business role:

- Stores important user-facing events that should remain visible after snackbar messages disappear.
- Complements email notifications without replacing them.
- Supports role-relevant categories such as adoption updates, shelter applications, resources, reports, and system messages.
- Enforces ownership through `NotificationService`, so users can only view, mark, or delete their own notifications.
- Avoids sensitive data such as passwords, reset tokens, security stamps, SMTP credentials, and private system secrets.

### ReportHistory

Represents metadata for generated reports, emailed report attachments, and CSV/PDF exports.

Important fields:

- `ReportType`
- `RecipientEmail`
- `Subject`
- `FileName`
- `GeneratedAt`
- `SentAt`
- `WasSuccessful`
- `ErrorMessage`
- `TriggeredBy`
- `TriggeredByUserId`
- `TriggeredByUserEmail`
- `ShelterId`
- `AdminUserId`
- `RelatedEntityName`
- `RelatedEntityId`

Business role:

- Tracks when a report/export was generated or sent without storing PDF/CSV binary content.
- Records metadata for manual shelter reports, Quartz scheduled shelter reports, adoption/resource/shelter application PDF notifications, and Admin/Shelter exports where practical.
- Stores short, user-safe failure messages when a send/generation attempt fails.
- Allows shelters to see only their own report history and admins to review all report history.
- Avoids sensitive values such as SMTP credentials, password reset links, tokens, passwords, and report file contents.

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
- Confirming an adoption visit sets the dog status to `Reserved` and records status history if it changed.
- Final adoption after the visit sets the dog status to `Adopted` and records status history if it changed.

### AdoptionRequestStatus

Values:

- `Pending`
- `VisitConfirmed`
- `Accepted`
- `Rejected`
- `Cancelled`

Behavior:

- `Pending` means the shelter has not reviewed the adoption request yet.
- `VisitConfirmed` means the shelter confirmed the adopter's proposed visit, but final adoption has not happened yet.
- `Accepted` is the final successful adoption state after the visit.
- Duplicate active requests from the same adopter for the same dog are blocked by service logic and the filtered pending-request index.
- Confirming a visit moves a pending request to `VisitConfirmed`, reserves the dog, rejects other pending requests for that dog, and sends an adopter email/notification with a `.ics` calendar attachment.
- Marking a confirmed request as adopted moves it to `Accepted` and marks the dog `Adopted`.
- Rejecting a request updates it to `Rejected`.
- Cancelling a request updates it to `Cancelled`.

### AdoptionVisitStatus

Values:

- `NotScheduled`
- `Requested`
- `Confirmed`
- `Completed`
- `Cancelled`

Behavior:

- New adoption requests with a selected visit time start as `Requested`.
- Shelter visit confirmation changes the visit status to `Confirmed`.
- Final adoption after the visit changes the visit status to `Completed`.
- Rejected or cancelled requests with scheduled visit data use `Cancelled`.

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

### NotificationCategory

Values:

- `Adoption`
- `ShelterApplication`
- `Resource`
- `Report`
- `System`

Behavior:

- Groups notifications in the top-bar dropdown and `/notifications` page.
- Category labels are displayed in user-friendly form, for example `ShelterApplication` is shown as "Shelter Applications".
- Empty categories are not shown in the dropdown because notifications are grouped from the current user's own records.

### NotificationType

Values:

- `Info`
- `Success`
- `Warning`
- `Error`

Behavior:

- Controls notification emphasis and icon/color choices in the UI.
- Examples include success notifications for confirmed shelter visits, completed adoptions, or sent reports; warning notifications for rejected requests or low-stock resources; and info notifications for new shelter applications.

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
- One `ApplicationUser` can be referenced by many `AdoptionRequest` records as the user who confirmed a visit.
- One `ApplicationUser` can review many `ShelterRegistrationRequest` records as an admin reviewer.
- One `ApplicationUser` has many private `Notification` records.
- One `Shelter` can be referenced by many `ReportHistory` records for shelter-scoped report/export metadata.
- `AuditLog` stores user identifiers and emails as denormalized text metadata instead of a required foreign key, so historical activity remains readable even if account details later change.
- `ReportHistory` stores recipient/user identifiers as metadata and only has an optional shelter relationship; it does not store generated PDF/CSV files.
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
- `Notification` cascades when the owning Identity user is deleted.
- `ReportHistory` uses `SetNull` for its optional shelter relationship so metadata can remain readable if a shelter is removed.
- Resource relationships use restricted delete.

Important indexes:

- `FavoriteDog` has a unique index on `AdopterId + DogId`.
- `RecentlyViewedDog` has a unique index on `AdopterId + DogId`.
- `AdoptionRequest` has a filtered unique index on `AdopterId + DogId` for pending requests (`Status = 0`).
- `AdopterProfile` has a unique index on `ApplicationUserId`.
- `Notification` has an index on `UserId + IsRead + CreatedAt` to support unread count and recent notification queries.
- `ReportHistory` has indexes on `GeneratedAt`, `ReportType`, and `ShelterId + GeneratedAt` to support recent admin/shelter history views.

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
- `Jobs`: Quartz.NET background job classes.
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
- `Services/NominatimGeocodingService.cs`: Performs manual address-based coordinate lookup through OpenStreetMap Nominatim.
- `Services/IGeocodingService.cs`: Interface used by public/admin forms so geocoding can be faked in tests.

Scheduled report organization:

- `Jobs/ShelterSummaryReportJob.cs`: Thin Quartz job that calls the shelter summary report service.
- `Jobs/VisitReminderJob.cs`: Thin Quartz job that calls the visit reminder service.
- `Services/IShelterSummaryReportService.cs` and `Services/ShelterSummaryReportService.cs`: Own scheduled report iteration, manual report sending, email body creation, and PDF attachment creation.
- `Services/IVisitReminderService.cs` and `Services/VisitReminderService.cs`: Find due confirmed adoption visits, send reminder emails with `.ics` attachments, mark `VisitReminderSentAt`, and create notification/audit records.
- `Entities/ReportHistory.cs`: Stores metadata for generated/sent reports and exports without storing generated file bytes.
- `Services/IReportHistoryService.cs` and `Services/ReportHistoryService.cs`: Record successful/failed/generated report metadata and query shelter/admin history views.
- `Services/ScheduledReportSettings.cs`: Configuration model for enabling/disabling scheduled reports, startup behavior, and minute interval.
- `Services/VisitReminderSettings.cs`: Configuration model for enabling/disabling visit reminders, check interval, and reminder timing.
- `Services/IPdfReportService.cs` and `Services/PdfReportService.cs`: Generate `ShelterSummaryReport-{date}.pdf` content.
- `Program.cs`: Registers Quartz, the hosted service, and minute-based triggers when `ScheduledReports:Enabled` or `VisitReminders:Enabled` is true.

Export organization:

- `Services/IExportService.cs` and `Services/ExportService.cs`: Generate Admin and Shelter CSV/PDF export bytes and filenames.
- `Services/IBrowserFileDownloadService.cs` and `Services/BrowserFileDownloadService.cs`: Trigger browser downloads through JavaScript interop.
- `Services/IAuditLogService.cs`, `Services/AuditLogService.cs`, and `Services/AuditActions.cs`: Record and query lightweight activity logs for admin monitoring.
- `Services/IReportHistoryService.cs` and `Services/ReportHistoryService.cs`: Track export generation metadata as `CsvExport` or `PdfExport` records.
- `wwwroot/app.js`: Contains `pawConnect.downloadFileFromBase64` for in-browser export downloads.
- `Components/Pages/Admin/*.razor`: Existing Admin pages expose compact export buttons near the page header/table area.
- `Components/Pages/Admin/AdminReportHistory.razor`: Admin-only report history page for all report/export metadata.
- `Components/Pages/Admin/AdminActivityLog.razor`: Admin-only activity log page with action/entity/search filters.
- `Components/Pages/Shelter/ManageDogs.razor`, `Components/Pages/Shelter/ShelterAdoptionRequests.razor`, and `Components/Pages/Shelter/Resources.razor`: Shelter pages expose compact export buttons scoped to the authenticated shelter.
- `Components/Pages/Shelter/ShelterDashboard.razor`: Shows recent report history metadata for the authenticated shelter.

CSV import organization:

- `Services/ICsvImportService.cs` and `Services/CsvImportService.cs`: Parse CSV input, validate headers and row values, produce row-level preview results, import valid shelter resource/dog rows for the current shelter only, and import Admin shelter CSV rows as pending shelter registration requests.
- `Services/CsvImportModels.cs`: Contains `CsvImportResult`, `CsvImportRowResult`, `CsvImportValidationError`, and import action labels used by the preview UI.
- `Components/Pages/Admin/AdminShelters.razor`: Provides shelter request CSV template download, upload preview, row validation display, confirmed import, and approved shelter CSV export.
- `Components/Pages/Shelter/Resources.razor`: Provides resource CSV template download, upload preview, row validation display, and confirmed import.
- `Components/Pages/Shelter/ManageDogs.razor`: Provides dog CSV template download, upload preview, row validation display, and confirmed import.
- `Services/AuditActions.cs`: Includes `ResourceCsvImported`, `DogCsvImported`, and `ShelterRequestsCsvImported` action names for successful import logging.

Notification organization:

- `Entities/Notification.cs`: Stores private in-app notification records.
- `Entities/NotificationCategory.cs` and `Entities/NotificationType.cs`: Categorize notification grouping and visual emphasis.
- `Services/INotificationService.cs` and `Services/NotificationService.cs`: Create notifications, query notifications for a user, count unread items, mark as read, mark all as read, and delete notifications with ownership checks.
- `Components/Shared/NotificationBell.razor`: Top-bar notification bell for authenticated users, including unread count, compact grouped recent notifications, and UI-only collapsing of repeated same-day notifications.
- `Components/Pages/Notifications.razor`: Authenticated page at `/notifications` for viewing, filtering, marking, opening, and deleting the current user's notifications.
- Notification triggers are added inside existing business services such as `AdoptionRequestService`, `ResourceStockService`, `ShelterRegistrationRequestService`, and `ShelterSummaryReportService`.

## 9. Main Application Flows

### Public Dog Browsing Flow

1. The user opens `/dogs`.
2. `Dogs.razor` loads public dog data through `IDogService`.
3. `DogService.GetAvailableDogsAsync` or `SearchDogsAsync` returns only dogs whose status is `Available` or `Reserved`.
4. The page displays filters, shelter filtering, sorting, dog cards with compact public-safe shelter name/city information, images/placeholders, status chips, and view details actions.
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
10. The Location card shows an "Open in Google Maps" link when coordinates or address/city information are available. The link uses coordinates first and falls back to an encoded address query.

The public map is read-only. Address lookup and explicit address updates from the selected pin are limited to editable shelter location forms; the app does not implement route planning, distance search, browser geolocation, or automatic typing autocomplete. Google Maps is only used as an external new-tab link from shelter details and does not require an API key.

### Shelter Registration Request Flow

1. A public shelter representative opens `/shelters/apply`. Anonymous users can submit applications, and logged-in adopters may submit if they are acting as shelter representatives.
2. The public application form collects shelter name, contact person, email, phone, city, address, description, and optional website/opening hours/reason.
3. Latitude and longitude are optional internal fields. The applicant may click "Find location" to run a manual Nominatim lookup from address + city + Romania.
4. If Nominatim returns a result, the form fills `Latitude` and `Longitude` and the editable map marker moves to that location.
5. If the marker needs adjustment, the user can drag the marker or click the map to update `Latitude` and `Longitude` internally. The public form shows friendly selected/missing location messages instead of raw coordinate values.
6. Moving the marker does not automatically overwrite the address/city fields.
7. After the user moves the marker or clicks the map, the app can perform a reverse lookup from the selected marker location and show a suggested address. The address/city fields are updated only when the user clicks "Update address from pin".
8. If Nominatim fails, the user can submit without coordinates or set them with the map marker.
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
   - Preferred shelter visit date and time.
4. `AdoptionRequestService.CreateRequestAsync` verifies:
   - The user is an adopter.
   - The dog exists.
   - The dog is not `Adopted` or `InTreatment`.
   - The adopter does not already have an active pending/visit-confirmed request for the dog.
   - The questionnaire is valid.
   - The preferred visit time is in the future, on an allowed visiting day, and within the shelter's configured visiting hours.
5. The service creates a pending `AdoptionRequest` with `VisitStatus = Requested`.
6. The service attempts to notify the owning shelter by email and attach `AdoptionRequestReport.pdf`.
7. Shelter users review requests at `/shelter/adoption-requests`.
8. A shelter can confirm a visit or reject pending requests only for its own dogs.
9. Confirming a visit:
   - Sets request status to `VisitConfirmed`.
   - Sets visit status to `Confirmed`.
   - Sets dog status to `Reserved`.
   - Creates dog status history if the dog status changed.
   - Rejects other pending requests for the same dog.
   - Sends adopter email/in-app notification with a generated `.ics` calendar invitation.
   - Leaves `VisitReminderSentAt` empty so the reminder job can send one reminder before the visit.
10. After the visit, the owning shelter can mark a confirmed request as adopted:
   - Sets request status to `Accepted`.
   - Sets visit status to `Completed`.
   - Sets dog status to `Adopted`.
   - Creates dog status history if the dog status changed.
   - Sends the final adopter notification with `AdoptionStatusReport.pdf`.
11. Rejecting a request:
   - Sets request status to `Rejected`.
   - Cancels visit status when applicable.
   - Returns a reserved dog to `Available` when a confirmed visit is rejected and the dog has not otherwise changed status.
   - Sends adopter notification with `AdoptionStatusReport.pdf`.
12. Adopters can cancel their own pending requests from `/my-adoption-requests`.

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

### Scheduled Shelter Summary Report Flow

1. Quartz.NET is registered in `Program.cs` with `AddQuartz` and `AddQuartzHostedService`.
2. `ScheduledReports:Enabled` controls whether the automatic Quartz trigger is created. It is `false` by default to avoid development email spam.
3. When enabled, Quartz runs `ShelterSummaryReportJob` using `ScheduledReports:ShelterReportIntervalMinutes`.
4. The job stays thin and calls `IShelterSummaryReportService.SendScheduledShelterSummaryReportsAsync`.
5. For each shelter with an email address, the service asks `IPdfReportService.GenerateShelterSummaryReportAsync` for a PDF.
6. Scheduled summary reports are periodic overviews, not alert-only emails; immediate adoption request and low-stock notifications remain separate flows.
7. The service emails `ShelterSummaryReport-{yyyy-MM-dd}.pdf` to the shelter using `IEmailService`.
8. Scheduled report notification creation uses a simple duplicate guard for the in-app "Summary report sent" notification, so very short demo intervals do not flood the notification dropdown with identical report entries. Email/PDF sending is not suppressed by this guard.
9. Failures for one shelter are logged and do not stop the job from processing other shelters.
10. Shelter users can manually send the same report from `/shelter/dashboard`; this manual action works even when automatic scheduling is disabled and can still create a notification.

### Visit Reminder Flow

1. `VisitReminders:Enabled` controls whether the automatic Quartz trigger is created. It is `false` by default to avoid development email spam.
2. When enabled, Quartz runs `VisitReminderJob` using `VisitReminders:CheckIntervalMinutes`; invalid values fall back to 30 minutes.
3. The job stays thin and calls `IVisitReminderService.SendDueVisitRemindersAsync`.
4. The service finds adoption requests where:
   - request status is `VisitConfirmed`
   - visit status is `Confirmed`
   - `PreferredVisitDateTime` exists
   - the visit is approximately `VisitReminders:ReminderHoursBeforeVisit` hours away, defaulting to 24
   - `VisitReminderSentAt` is null
   - adopter email exists
5. Reminder matching uses a simple one-hour window around the target reminder time, so the job does not need exact-second timing.
6. For each due request, the service sends a branded reminder email with a dynamically generated `text/calendar` `.ics` attachment.
7. After the email send call succeeds, `VisitReminderSentAt` is set and an Adoption notification is created for the adopter.
8. The service writes an audit action `VisitReminderSent` when audit logging is available.
9. Failed reminders are logged per request and do not stop other due reminders from being processed.
10. The app does not use Google Calendar API/OAuth and does not store `.ics` files in the database.

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
7. Admin pages can export page-level platform data as CSV. Adoption requests and shelter applications can also be exported as formatted PDF summaries.

### Admin Export Flow

1. An Admin user opens an Admin page such as `/admin/users`, `/admin/dogs`, `/admin/adoption-requests`, or `/admin/shelter-requests`.
2. The page displays an export action area with `Export CSV` and, where useful, `Export PDF`.
3. The component calls `IExportService` to generate the file content in memory.
4. CSV exports include header rows and table-style platform data suitable for Excel.
5. PDF exports use QuestPDF for clean formatted summaries of adoption requests and shelter applications.
6. `IBrowserFileDownloadService` calls JavaScript interop to download the generated file in the browser.
7. Export files are not stored in the database or permanently written to disk.
8. User CSV exports intentionally omit sensitive Identity fields such as password hashes, security stamps, concurrency stamps, and tokens.
9. `ReportHistoryService` records export metadata as `CsvExport` or `PdfExport`; the file bytes are not persisted.

### Shelter Export Flow

1. A Shelter user opens `/shelter/dogs`, `/shelter/adoption-requests`, or `/shelter/resources`.
2. The page resolves the current shelter from the authenticated Identity user.
3. The page displays compact export actions near the page header or table area.
4. The component calls `IExportService` with the current `ShelterId`.
5. Shelter dog exports return only dogs belonging to that shelter.
6. Shelter adoption request exports return only requests for dogs owned by that shelter and may include private shelter internal notes.
7. Shelter resource exports return only resource stock rows for that shelter and include low-stock status.
8. CSV exports are table-style UTF-8 files suitable for Excel; adoption requests and resources also support QuestPDF-formatted PDFs.
9. `IBrowserFileDownloadService` downloads the generated file in the browser without storing it in the database.
10. `ReportHistoryService` records shelter export metadata with the current `ShelterId`, preserving ownership filtering for history views.

### CSV Import Flows

Shelter-owned operational imports:

1. A Shelter user opens `/shelter/resources` or `/shelter/dogs`.
2. The page resolves the current shelter from the authenticated Identity user.
3. The user can download a CSV template:
   - `pawconnect-resource-import-template.csv`
   - `pawconnect-dog-import-template.csv`
4. The user selects a `.csv` file. The page validates extension and size before reading the file into memory.
5. `ICsvImportService` parses the header row, trims values, supports quoted CSV values, and validates every row.
6. The page shows a preview summary with total, valid, and invalid rows plus row-level errors.
7. The confirm action is available only when the preview has no blocking row errors.
8. Resource imports create or update resource stock for the current shelter based on name, category, and food type. Non-food resources ignore/clear food type values.
9. Dog imports create dog records for the current shelter and add optional semicolon-separated HTTP/HTTPS image URLs as `DogImage` records.
10. Invalid rows are not imported silently. The workflow is all-or-nothing for simplicity and clear user feedback.
11. Successful imports are recorded in `AuditLogs` with the imported row count, without storing full CSV content.

Admin shelter request imports:

1. An Admin user opens `/admin/shelters`.
2. The user can download `pawconnect-shelter-requests-import-template.csv`.
3. The user selects a `.csv` file, previews it, and reviews row-level validation results.
4. `ICsvImportService` validates required application fields, email format, optional website URL, optional latitude/longitude ranges, duplicate emails in the CSV, duplicate pending shelter applications, and existing shelter/user emails.
5. Address and city are normalized separately, so rows such as `Strada Exemplu 10, Cluj-Napoca` store `Address = Strada Exemplu 10` and `City = Cluj-Napoca`.
6. Confirming a valid preview creates `ShelterRegistrationRequest` rows with `Pending` status only.
7. The import does not create `ApplicationUser` accounts, assign roles, or create `Shelter` profiles.
8. Admins review imported requests at `/admin/shelter-requests`; accepting or rejecting uses the existing shelter registration request approval flow.
9. Successful Admin shelter request imports are recorded in `AuditLogs` as `ShelterRequestsCsvImported`, with row counts and without storing full CSV content.

### Report History Flow

1. A report/email/export flow generates report metadata, such as file name, report type, recipient, trigger type, and related shelter/entity information.
2. The report or export continues through the existing email/download path. Generated PDF/CSV bytes are not stored in the database.
3. The service calls `IReportHistoryService` after a successful generated/sent report, or when a send failure can be recorded safely.
4. `ReportHistoryService` stores a `ReportHistory` row with `WasSuccessful`, `GeneratedAt`, optional `SentAt`, and a short error message when failed.
5. If history recording fails, the failure is logged and the original report/email/export action continues.
6. Shelter users see recent records for their own `ShelterId` on `/shelter/dashboard`.
7. Admin users review all records at `/admin/report-history`, with simple report type and status filters.
8. The history view is metadata-only; it does not offer report download because generated files are not persisted.

### In-App Notification Flow

1. A successful business action occurs in an existing service, such as a new adoption request, a confirmed visit, a finalized/rejected adoption request, a low-stock resource update, a shelter application submission, a shelter request CSV import, or a shelter summary report send.
2. The service keeps existing email/snackbar behavior and additionally calls `INotificationService`.
3. `NotificationService` creates a `Notification` row for the intended `ApplicationUser`.
4. The top-bar `NotificationBell` loads the current user's unread count and recent notifications.
5. Notifications are grouped by category in the dropdown: Adoption, Shelter Applications, Resources, Reports, and System.
6. The dropdown stays compact by limiting the displayed items and collapsing repeated notifications that share category, title, message, and local day. For example, multiple same-day "Summary report sent" notifications can appear as one item with a "+N similar today" indicator.
7. The `/notifications` page loads only the current user's notifications and supports all/unread/category filters. It remains the place for full notification history.
8. Mark-as-read, mark-all-as-read, and delete actions filter by `UserId`, so one user cannot alter another user's notifications.
9. When a notification has a link, opening it marks the notification as read and navigates to the relevant page, such as `/my-adoption-requests`, `/shelter/adoption-requests`, `/shelter/resources`, `/shelter/dashboard`, or `/admin/shelter-requests`.

### Audit Log Flow

1. A user or system process performs an important business action.
2. The relevant service completes the main database action first.
3. The service calls `IAuditLogService` with an action name, entity name/id, description, and available user/system metadata.
4. `AuditLogService` enriches the log with current user claims when available and stores an `AuditLog` row.
5. If audit logging fails, the failure is logged as a warning and the original business action is not rolled back.
6. Admin users open `/admin/activity-log` to review recent activity, filter by action/entity, and search by description, user email, or entity id.

## 10. Email Notification System

Email abstractions:

- `IEmailService`
- `SmtpEmailService`
- `MockEmailService`
- `PawConnectIdentityEmailSender`
- `PawConnectEmailTemplate`
- `EmailSettings`
- `EmailAttachment`

`Program.cs` registers:

- `IEmailService` as `SmtpEmailService`.
- `IEmailSender<ApplicationUser>` as `PawConnectIdentityEmailSender`.
- `EmailSettings` through `builder.Configuration.GetSection("EmailSettings")`.

The SMTP implementation:

- Uses MailKit `SmtpClient`.
- Uses MimeKit `MimeMessage`, `TextPart`, `Multipart`, and `MimePart`.
- Sends plain text email bodies.
- Supports optional branded HTML bodies with plain text fallback.
- Supports optional attachments.
- Sends adoption visit `.ics` files as `text/calendar; method=REQUEST; charset=utf-8` MIME parts and also keeps an `.ics` attachment fallback.
- Uses `SecureSocketOptions.StartTls` when `EnableSsl` is true.
- Connects without SMTP authentication when `SmtpUser` and `SmtpPassword` are empty, which supports local SMTP catchers.
- Authenticates with configured `SmtpUser` and `SmtpPassword` if a real SMTP provider is configured.
- In Development, `Program.cs` can open the configured local email inbox URL automatically when `OpenLocalInboxOnStartup` is enabled.
- Logs and returns if the recipient is empty.
- Logs and returns if SMTP host or sender email is incomplete.
- Catches exceptions and logs warnings instead of throwing them back to the business flow.

Configured email events:

- Forgot Password requests: sends an ASP.NET Core Identity password reset link.
- Identity account confirmation and email verification requests: sends Identity account links.
- New adoption request submitted: sends shelter notification.
- Adoption visit confirmed: sends adopter notification with a generated `.ics` calendar attachment.
- Adoption visit reminder: sends adopter notification about 24 hours before the confirmed visit with a generated `.ics` calendar attachment.
- Final adoption completed after a visit: sends adopter notification with `AdoptionStatusReport.pdf`.
- Adoption request rejected: sends adopter notification.
- Resource stock created/updated and low stock: sends shelter notification.
- Shelter summary report manual/scheduled send: sends a PDF summary to the shelter email.

`PawConnectEmailTemplate` provides a reusable branded HTML layout for important emails. The template uses inline CSS only, with a green/teal PawConnect header, centered white content card, optional primary action button, details section, fallback link area, PDF attachment notice, and footer text. It does not depend on external images or external CSS.

For the password reset flow, the Forgot Password page generates an ASP.NET Core Identity reset token, encodes it with `WebEncoders.Base64UrlEncode`, builds an `/Account/ResetPassword` callback URL, and sends it through `PawConnectIdentityEmailSender`. The reset email includes a plain text fallback with the full URL on its own line for local development inbox text views/copy-paste use, and a branded HTML body with a clickable action button. The UI response remains generic so it does not reveal whether an email address belongs to a registered user.

Application notifications for adoption requests, low-stock resources, and shelter applications also use the branded HTML template where practical while preserving the existing plain text bodies and PDF attachments.

Visit confirmation emails are intentionally calendar-provider neutral. They include the visit details in the email body and send a dynamically generated `text/calendar; method=REQUEST` calendar part with a stable request-based UID, a 60-minute event duration, shelter address/city as the location, shelter contact details in the description, `STATUS:CONFIRMED`, and organizer/attendee metadata when shelter/adopter email addresses are available. The `.ics` file is also included as an attachment fallback for clients that do not surface calendar actions automatically. PawConnect does not use Google Calendar API/OAuth or Microsoft Graph.

Visit reminder emails reuse the same `.ics` generation helper as confirmation emails. The reminder service sets `VisitReminderSentAt` only after the email send call succeeds, preventing duplicate reminders for the same confirmed visit.

Scheduled shelter summary report emails also use the existing email infrastructure and attach a generated PDF. The scheduled job is disabled by default through `ScheduledReports:Enabled = false`; the Shelter Dashboard manual send action still works for demo/testing.

The current `appsettings.json` and `appsettings.Development.json` use safe local smtp4dev defaults: `localhost:2525`, empty credentials, and `EnableSsl = false`. Development can inspect generated password reset emails, application notifications, PDF report attachments, and `.ics` calendar invitations in the smtp4dev browser UI at `http://localhost:3000`. Real SMTP credentials should not be committed; if a real provider is configured later, secrets should come from User Secrets or environment variables.

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
- Request reason, hours alone per day, additional information, preferred visit time, visit status, and request date.

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
- Next steps depending on final accepted/rejected status.

Attached when a shelter finalizes an adoption after the visit or rejects a request.

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

### Shelter Summary Report

Method:

- `GenerateShelterSummaryReportAsync(int shelterId, DateTime fromDate, DateTime toDate)`

Includes:

- Shelter name, email, city, report generation date/time, and report period.
- Adoption request summary: new requests in period, pending, accepted, rejected, cancelled, and total requests.
- Dog overview: total dogs, available, reserved, adopted, in treatment, and recently adopted dogs.
- Resource overview: low-stock resources with category, optional food type, quantity, unit, and threshold.
- A note that the report was generated automatically by PawConnect.

Attached when a shelter user clicks "Send Summary Report" from `/shelter/dashboard` or when the Quartz scheduler sends scheduled shelter summary reports.

The reports use a clean text/table layout with PawConnect header, section headings, rows, and generated-date footer. The code does not implement charts, graphs, admin scheduled reports, or database storage of generated PDFs.

## 11.1 Scheduled Report System

Scheduled shelter reports are implemented with Quartz.NET.

Important classes and configuration:

- `ShelterSummaryReportJob`: Quartz job class.
- `IShelterSummaryReportService` / `ShelterSummaryReportService`: Report eligibility, PDF attachment, and email sending logic.
- `ScheduledReportSettings`: Options bound from `ScheduledReports`.
- `ScheduledReports:Enabled`: Enables automatic Quartz scheduling. Default is `false`.
- `ScheduledReports:RunOnStartupInDevelopment`: Runs once at startup only when explicitly true in development.
- `ScheduledReports:ShelterReportIntervalMinutes`: Minute-based interval; invalid values fall back to a safe 5-minute interval.

The scheduler uses Quartz's in-memory scheduling. The project does not include external cron, Hangfire, a Quartz dashboard, or a persistent Quartz job store. Admin scheduled reports are not implemented yet and remain a possible future extension.

Manual and scheduled shelter summary sends create `ReportHistory` metadata records. Manual dashboard sends use `TriggeredBy = Manual`; Quartz sends use `TriggeredBy = Quartz`. Successful records include generated/sent timestamps and the report file name, while failed send attempts store a short error message. The PDF content itself is not stored.

## 11.2 Visit Reminder System

Visit reminders are implemented with Quartz.NET.

Important classes and configuration:

- `VisitReminderJob`: Quartz job class.
- `IVisitReminderService` / `VisitReminderService`: Query due confirmed visits, send reminder emails, mark `VisitReminderSentAt`, and create notification/audit records.
- `VisitReminderSettings`: Options bound from `VisitReminders`.
- `VisitReminders:Enabled`: Enables automatic Quartz scheduling. Default is `false`.
- `VisitReminders:CheckIntervalMinutes`: Minute-based check interval; invalid values fall back to 30 minutes.
- `VisitReminders:ReminderHoursBeforeVisit`: Reminder timing; invalid values fall back to 24 hours.

Reminder emails are sent only for confirmed visits and include the same kind of generated `.ics` calendar attachment as the visit confirmation email. The project does not use Google Calendar API/OAuth, does not store `.ics` files in the database, and does not send reminders for rejected/cancelled/finalized requests.

## 11.5 Shelter Map Integration

The application includes a simple third-party map integration for public shelter profile pages.

Implemented behavior:

- Leaflet handles client-side interactive map rendering.
- OpenStreetMap provides the map tiles.
- No Google Maps API key or paid maps API is required. Google Maps is used only as an optional external link from public shelter details, not as the embedded map provider.
- Public shelter maps are read-only. Shelter application/admin coordinate forms use an editable mode for coordinate adjustment.
- Shelter coordinates are stored in the database as optional `Latitude` and `Longitude` fields on the `Shelter` entity.
- Address information is the primary location input; coordinates can be derived through a manual OpenStreetMap Nominatim lookup or adjusted through the editable map marker. Raw coordinate inputs are hidden from public shelter applicants and normal admin shelter edit forms.
- The UI exposes an explicit "Update address from pin" action when a suggested address exists. Marker movement alone does not automatically overwrite address fields; reverse lookup results are shown as a suggestion before the user applies them.
- `ShelterMap.razor` receives the coordinates and passes them to JavaScript interop for Leaflet initialization.
- In editable form mode, `ShelterMap.razor` exposes latitude/longitude callbacks so marker dragging and map clicks update the bound coordinate fields internally.
- Marker popups show the shelter name and address/city when available.
- The map uses a custom inline SVG-style marker, so it does not depend on Leaflet marker PNG files loading correctly.
- Public shelter details include an external "Open in Google Maps" link that uses coordinates first and falls back to the public address/city query.

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
- Authenticated users see a notification bell with unread count. The dropdown groups recent notifications by category, collapses repeated same-day items, and links to `/notifications`.
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
- Notification chips/dots and category group headings distinguish unread items and categories without exposing notifications across users.
- Shelter profile pages include a Location card that displays a responsive read-only Leaflet/OpenStreetMap map with rounded corners when coordinates are available, plus an external Google Maps navigation link when coordinates or address/city information are available.
- Shelter application and admin shelter edit forms use editable map mode so users can click to place the pin or drag the marker to refine coordinates. The forms keep raw coordinate values internal and show friendly location status messages instead.
- Shelter map fallback states use MudBlazor alerts/messages when coordinates are unavailable, keeping the page usable instead of showing an empty or broken map.

## 13. Validation and Error Handling

Validation is implemented at several levels:

- Data annotations on entities, such as `[Required]`, `[StringLength]`, `[Range]`, `[EmailAddress]`, `[Phone]`, and `[Url]`.
- Service-level validation for business rules and ownership.
- MudBlazor form validation and UI checks in pages.
- Database indexes for uniqueness, such as pending adoption requests and favorites.

Examples of service validation:

- `DogService` validates dog name, breed, location, age years/months, daily food amount, shelter ownership, and dog deletion restrictions.
- `DogImageService` validates image URLs, trims empty input, prevents duplicate image URLs for the same dog, and enforces shelter ownership.
- `MedicalRecordService` validates record date and shelter ownership.
- `AdoptionRequestService` validates adopter role, requestable dog status, duplicate active request, visit time rules, pending/confirmed status transitions, dog ownership for shelter actions, adopter ownership for cancellation, and questionnaire fields.
- `VisitReminderService` sends reminders only for confirmed visits with a future preferred visit time, adopter email, and null `VisitReminderSentAt`.
- `FavoriteDogService` validates adopter role and public-safe dog status.
- `ResourceStockService` validates name, category, non-negative quantity/threshold, unit, required food type for food resources, cleared food type for non-food resources, duplicate stock items per shelter/category/name/food type, and shelter ownership.
- `AdopterProfileService` validates full name, city, phone number, and profile image URL.
- `ShelterService` validates shelter profile contact fields, duplicate shelter email, separated address/city values, and optional latitude/longitude ranges.
- `ShelterRegistrationRequestService` validates shelter application required fields, duplicate pending emails with case-insensitive trimmed comparison, existing shelter account/profile email conflicts, separated address/city values, optional latitude/longitude ranges, role restrictions for public submission, and admin-only pending accept/reject behavior.
- `ExportService` scopes Admin exports to Admin pages and Shelter exports to the current shelter, while excluding sensitive Identity fields.
- `CsvImportService` validates shelter CSV imports before saving, detects duplicate rows, scopes shelter-owned imports to the current shelter, imports Admin shelter rows only as pending registration requests, blocks invalid row data, and processes uploaded CSV files in memory without storing them permanently.
- `NotificationService` enforces notification ownership for read/delete operations and ignores empty notification titles or messages.
- `NominatimGeocodingService` returns `null` for missing/failed geocoding results so forms can show friendly messages without blocking application submission.

Error handling patterns:

- UI pages show validation at the closest useful level: field-specific business validation is mapped to MudBlazor input `Error`/`ErrorText`, multi-field validation appears as compact in-form MudAlert content near the related section, and MudSnackbar is used mainly for success, background/system feedback, or failures that are not tied to one field.
- Adoption visit scheduling maps closed-day errors to the preferred visit date field, outside-hours errors to the preferred visit time field, and questionnaire range errors to the relevant questionnaire inputs while keeping service-level validation as the source of truth.
- Shelter applications, dog create/edit forms, resource stock forms, and admin shelter editing map common service validation such as duplicate emails, duplicate dog image URLs, invalid dog age, invalid resource quantity/threshold, duplicate resources, visiting-hours errors, and optional coordinate range errors to the relevant field or section.
- CSV imports keep row-specific validation in the preview table so invalid rows are visible before import; snackbars only summarize preview/import outcomes.
- Missing shelter coordinates do not break shelter profile pages; the map component displays a friendly fallback message instead.
- Leaflet map initialization is handled through JavaScript interop with unique map element IDs, explicit map sizing, resize invalidation, marker drag/click callbacks in editable mode, and disposal to avoid broken map rendering during Blazor Server navigation.
- Expected business rule violations use clear `InvalidOperationException` messages.
- Email and PDF failures are logged and do not fail the main business action.
- Scheduled shelter report failures are logged per shelter so one failed report does not stop the whole Quartz job.
- Visit reminder failures are logged per request so one failed reminder does not stop other due reminders.
- Confirmation dialogs are used before important/destructive actions such as delete, cancel, accept, reject, and logout.
- Audit logging is best-effort: audit failures are logged as warnings and do not fail the main user action.
- Notification creation is best-effort: notification failures are logged and do not fail the original business action.
- Notification read/delete methods include the current `UserId`, so a user cannot mark or delete another user's notification.
- Report history logging is best-effort: failures are logged as warnings and do not fail report sending, export download, Quartz runs, or PDF/email notification flows.
- Report history stores metadata only and does not persist PDF/CSV bytes, password reset links, SMTP credentials, tokens, or other secrets.
- `UseStatusCodePagesWithReExecute("/not-found")` provides a friendly not-found route.
- `AuthorizeRouteView` redirects unauthorized route access through the account redirect component.

Important delete restrictions:

- Dogs with adoption request history cannot be hard deleted.
- Favorites and recently viewed records do not block dog deletion and are removed when deleting a dog that has no adoption requests.
- Adoption requests are preserved when dog deletion is blocked.
- Shelter coordinates are optional; invalid coordinate ranges are rejected only when values are provided.
- Geocoding failures do not block shelter application or shelter profile saving.
- Audit logs intentionally avoid passwords, reset tokens, security stamps, SMTP credentials, and other sensitive values.

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
- Tests do not require SQL Server, a real SMTP provider, a running web server, or browser UI automation.

Current test organization:

- `DogServiceTests`
- `DogImageServiceTests`
- `AdoptionRequestServiceTests`
- `FavoriteDogServiceTests`
- `ResourceStockServiceTests`
- `ShelterRegistrationRequestServiceTests`
- `NominatimGeocodingServiceTests`
- `ShelterSummaryReportServiceTests`
- `PdfReportServiceTests`
- `ExportServiceTests`
- `EmailMimeBuilderTests`
- `ReportHistoryServiceTests`
- `AuditLogServiceTests`
- `NotificationServiceTests`
- `VisitReminderServiceTests`
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
- Duplicate dog image URL blocking for the same dog while allowing reuse on different dogs.
- Dog age formatting, including puppies such as `7 months old`.
- Invalid dog age months validation.
- Zero daily food amount accepted when provided.
- Adoption request creation and questionnaire persistence.
- Adoption request creation with preferred shelter visit time.
- Past, closed-day, or outside-hours visit times are rejected.
- Non-adopters blocked from submitting adoption requests.
- Duplicate active adoption requests blocked.
- Adoption requests for adopted dogs blocked.
- Confirming non-pending or already-adopted requests blocked.
- Shelter visit confirmation and reject behavior.
- Shelter ownership restrictions for adoption requests.
- Cancellation rules, including blocking cancellation by another adopter.
- Dog status history creation when confirming a visit reserves a dog and when final adoption marks a dog adopted.
- No duplicate status history when status does not change.
- Visit confirmation sends a `text/calendar; method=REQUEST` calendar invite part plus `.ics` attachment fallback and does not mark the dog adopted.
- Marking a confirmed request as adopted updates the dog to `Adopted`.
- Confirmed visits about 24 hours away are eligible for visit reminders.
- Unconfirmed, rejected, cancelled, or already-reminded visits are not eligible for reminders.
- Visit reminder emails include a `text/calendar` `.ics` invitation generated from the same helper.
- `VisitReminderSentAt` is set after a successful reminder send and remains null when the email service throws.
- Visit reminder notification and audit records are created when those services are available.
- Resource create/update/delete and ownership.
- Low-stock detection and non-low-stock detection.
- Negative resource quantities blocked.
- Duplicate shelter resource stock items blocked.
- Food resources require a food type.
- Clearing `FoodTypeId` for non-food resources.
- Shelter registration request submission with optional coordinates.
- Invalid shelter application latitude/longitude rejected when provided.
- Duplicate pending shelter request emails blocked case-insensitively.
- Existing shelter account/profile emails blocked for shelter applications.
- Shelter application addresses are stored without duplicating the city suffix.
- Anonymous shelter registration request submission.
- Blocking Admin and Shelter users from submitting public shelter applications.
- Duplicate pending shelter registration request blocking.
- Admin notification email/PDF attachment capture for shelter applications.
- Admin accept/reject shelter application behavior.
- Blocking non-admin users from accepting or rejecting shelter applications.
- Creating shelter users, assigning the `Shelter` role, and linked shelter profile creation after approval.
- Nominatim geocoding response parsing and failure handling using fake HTTP responses.
- Identity email sender behavior for password reset and account confirmation emails without sending real SMTP messages.
- SMTP email delivery preserves branded HTML/plain text bodies, PDF/CSV attachments, and `text/calendar` invite parts without requiring real SMTP in automated tests.
- PDF report generation returns non-empty bytes.
- Shelter summary report PDF generation returns non-empty bytes.
- Manual shelter summary report sending creates an email with a PDF attachment for the current shelter.
- Scheduled shelter report logic respects `ScheduledReports:Enabled = false`.
- Scheduled shelter report logic sends reports to all shelters with an email when enabled.
- Admin users CSV includes user/role data and excludes sensitive Identity fields.
- Admin dogs CSV includes dog, shelter, status, food, and formatted age data.
- Admin adoption request and shelter request CSV exports include expected business fields.
- Admin adoption request and shelter request PDF exports return non-empty PDF bytes.
- Shelter dogs CSV includes only dogs from the current shelter.
- Shelter adoption requests CSV includes only requests for the current shelter's dogs.
- Shelter resources CSV includes only current shelter resources and low-stock status.
- Shelter adoption request and resource PDF exports return non-empty PDF bytes.
- Shelter resource CSV import validates required fields, duplicate rows, negative values, food/non-food rules, existing resource updates, and current-shelter scoping.
- Shelter dog CSV import validates age/status rules, required fields, optional image URLs, duplicate image URLs, and current-shelter ownership.
- Admin shelter request CSV import creates pending `ShelterRegistrationRequest` rows without creating users or approved shelters directly.
- Admin shelter request CSV import blocks duplicate pending emails, existing shelter/user emails, invalid email values, invalid coordinate values, and duplicated city/address storage.
- Imported shelter request rows can still be accepted through the existing admin approval flow, creating the shelter user, role assignment, and linked shelter profile at approval time.
- Report history records are created for manual shelter summary reports.
- Report history records are created for failed shelter report sends.
- Scheduled shelter reports are tracked with the Quartz trigger.
- Shelter report history queries return only the current shelter's records.
- Admin report history queries can return all records and filter failures.
- Export generation creates metadata-only report history records.
- `ReportHistory` does not include binary content/bytes fields.
- Audit logs are created for selected dog, adoption request, and resource actions.
- Recent audit log queries return newest records first.
- Audit descriptions tested do not include sensitive Identity field names such as password hash or security stamp.
- Notification records are created for selected adoption, shelter application, resource, and report events.
- Notification unread counts, newest-first ordering, category filtering, and owner-only mark-as-read behavior are tested.
- Integration-style service flows for public visibility, favorites/deletion, adoption notifications, dog image/age, resources, and fake PDF/email attachment behavior.

The README documents running tests with:

```bash
dotnet test
```

The test suite is expected to be run with `dotnet test` after code changes; this document describes the test setup found in the codebase rather than recording a permanent test count.

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
- Shelter users can manually send only their own shelter summary report from `/shelter/dashboard`; the page resolves the shelter from the authenticated account.
- Audit logs are visible only to Admin users through `/admin/activity-log`.
- Unauthorized route access is handled through `AuthorizeRouteView` and the account redirect component.

Service-level security/ownership checks:

- Adoption request creation requires an adopter account.
- Favorite management requires an adopter account.
- Shelter dog management checks dog `ShelterId`.
- Shelter image and medical record management checks dog ownership.
- Shelter resource management checks resource `ShelterId`.
- Shelter adoption request review checks the request dog belongs to the shelter.
- Shelter export generation receives the current `ShelterId` and filters dogs, adoption requests, and resources to that shelter.
- Adopter request cancellation checks request ownership.
- Recently viewed data is keyed by adopter ID and returned only for that adopter.
- Admin pages are role-protected and can access platform-wide data.
- Admin export actions are exposed only on Admin pages, and user exports intentionally exclude sensitive Identity security fields.
- Shelter export actions are exposed only on Shelter pages and are scoped to the authenticated shelter account.
- Shelter report history queries are scoped by the authenticated shelter's `ShelterId`.
- Admin report history is available only on the Admin-protected `/admin/report-history` page.
- Audit logging excludes passwords, reset tokens, security stamps, SMTP credentials, and full change snapshots.
- Notifications are private to the owning `ApplicationUser`; query, mark-as-read, mark-all-as-read, and delete operations enforce ownership through `NotificationService`.

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
- CSV import/export workflows for shelter operational data and admin platform data.
- Email communication using local SMTP catchers for development testing.
- Persistent role-based in-app notifications grouped by adoption, shelter application, resource, report, and system categories.
- PDF report generation with structured report content.
- Quartz.NET scheduled shelter summary reports and 24-hour adoption visit reminders.
- Report history metadata tracking for report/export traceability without storing generated file contents.
- Lightweight audit/activity logging for traceability and accountability.
- MudBlazor UI with dashboards, forms, tables, dialogs, cards, snackbars, and empty/loading states.
- Automated service/domain tests and integration-style flow tests.
- Seed/demo data for presentation and testing.

## 17. Limitations and Future Improvements

Based on the current codebase, realistic future improvements include:

- Real image file upload instead of URL-only dog images.
- Cloud or local file storage for uploaded images.
- More advanced address search/geocoding suggestions beyond the current manual Nominatim lookup.
- "Nearby shelters" search.
- Route/directions integration.
- Distance-based shelter or dog filtering.
- Better mobile map interactions.
- Map-based dog and shelter location search.
- More advanced dog recommendations based on adopter profile and preferences.
- Full shelter messaging/chat between adopters and shelters.
- Admin-level scheduled reports and richer report management UI.
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
- No admin scheduled reports.
- No full event sourcing, rollback, or audit-based entity restoration.
- No route planning, distance search, or browser geolocation.
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

Discuss the Blazor pages, services, business rules, dashboards, dog management, adoption request flow, resource stock flow, shelter map integration, email notifications, PDF reports, and Quartz.NET scheduled shelter reports.

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
- Shelter maps use optional stored coordinates on `Shelter`; public maps are read-only and coordinate forms use manual Nominatim lookup plus editable map markers.
- Nominatim geocoding is manual/user-triggered only, not autocomplete, continuous dragging lookup, or automatic background geocoding.
- Email/PDF failures are intentionally non-blocking and logged.
- PDF reports are generated dynamically and are not stored in the database.
- Demo seed data includes test users, demo shelters with approximate Cluj-Napoca coordinates, lookup data, dog records, images, medical records, resources, adopter profile data, and adopted success story examples.
- The README currently still uses some "skeleton/planned features" wording even though many features are implemented; thesis writing should rely on the current code and this context document for accuracy.
- The test suite is service/domain-focused, not browser UI-focused.
- The generic repository exists, but core services mainly use `ApplicationDbContext` directly for richer business queries and ownership checks.
- Real SMTP credentials should not be committed. Local SMTP catcher configuration with Mailpit or smtp4dev is suitable for development/testing; real SMTP providers can be configured later through `EmailSettings` if needed.
