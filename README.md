# PawConnect: A Web Platform for Stray Dog Adoption and Shelter Management

PawConnect is a beginner-friendly ASP.NET Core Blazor Server skeleton for a stray dog adoption and shelter management system. It is structured for a bachelor thesis project and is ready for future CRUD and workflow implementation.

## Technologies

- ASP.NET Core Blazor Server
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity with roles
- MudBlazor

## Roles

- Adopter
- Shelter
- Admin

## Current Skeleton

- Identity user model with role support
- SQL Server DbContext and EF migrations
- Domain entities for shelters, dogs, dog images, medical records, adoption requests, favorite dogs, adopter profiles, and resource stock
- Seed roles, test users, and demo domain data at startup after the database schema exists
- Demo data includes Cluj-Napoca demo shelters, dogs, dog images, medical records, resource categories, food types, and resource stock
- MudBlazor layout with role-based sidebar navigation
- Placeholder pages for public, adopter, shelter, and admin workflows
- Simple service and repository layer
- SMTP email notification service using configuration-based settings
- PDF email report attachments for adoption and low-stock notifications
- Adopter profile page for household/contact information used during adoption request review
- Dog status history tracking for shelter/admin review
- Internal shelter notes for adoption requests, visible only to shelter users and admins
- Recently viewed dogs for adopter dashboard quick access
- Public adoption success stories for adopted dogs
- Dog ages support years and months for puppy-friendly display
- Approval-based shelter registration requests at `/shelters/apply`
- Admin shelter application review at `/admin/shelter-requests`
- Optional address-based coordinate lookup using OpenStreetMap Nominatim

## Planned Features

- Image upload support

## Shelter Registration Requests

Public registration is for adopter accounts only. Shelter representatives do not create shelter accounts directly from the public Register page.

The shelter application form is intended for public shelter representatives. Admin users review applications from `/admin/shelter-requests`, and existing Shelter users already have active shelter accounts, so Admin/Shelter users are not allowed to submit public shelter applications.

Shelters apply through:

```text
/shelters/apply
```

The shelter application form stores a `ShelterRegistrationRequest` with `Pending` status. Applicants provide normal address/contact information:

- Shelter name
- Contact person
- Email
- Phone number
- City
- Street/address
- Description
- Optional website, opening hours, reason for joining, latitude, and longitude

Administrators review requests at:

```text
/admin/shelter-requests
```

When an administrator accepts a request, PawConnect creates an `ApplicationUser`, assigns the `Shelter` role, and creates a linked `Shelter` profile. Rejected requests do not create a user or shelter profile. Admin-managed shelter editing remains available for already approved shelters.

Submitting a shelter request attempts to notify admins by email and attach a PDF summary. Email/PDF failures are logged and do not delete or cancel the saved request.

## Email Notifications

PawConnect uses `IEmailService` with `SmtpEmailService` as the active implementation. The service sends email through SMTP using MailKit. Important notifications include a branded PawConnect HTML layout with a plain text fallback, so Mailtrap can be used to inspect both readable text bodies and richer HTML previews.

Email notifications are triggered when:

- A user requests a password reset through Forgot Password, sending an Identity reset link.
- Identity account confirmation and email verification messages are sent.
- An adopter submits a new adoption request, notifying the owning shelter.
- A shelter accepts or rejects an adoption request, notifying the adopter.
- A shelter creates or updates a resource stock item that is at or below its low-stock threshold, notifying the shelter.

Some notifications include PDF report attachments:

- `AdoptionRequestReport.pdf` is attached when a new adoption request is sent to a shelter.
- `AdoptionStatusReport.pdf` is attached when a request is accepted or rejected and the adopter is notified.
- `LowStockResourceReport.pdf` is attached when a resource reaches low stock.

The reports are generated without charts. They use a clean PawConnect-style text and table layout with section headings, spacing, and a generated-date footer.

Email or PDF generation failures are logged and do not cancel the main database action. For example, an adoption request can still be submitted even if SMTP credentials are missing or invalid.

Forgot Password and other ASP.NET Core Identity emails use `PawConnectIdentityEmailSender`, which reuses the same configured `IEmailService`/Mailtrap SMTP settings as the rest of the application. Password reset emails include a clean plain text body with the reset URL on its own line for easy copying from Mailtrap's Text view, plus a branded HTML body with a clickable action button when HTML preview is available. Demo users are seeded with confirmed email addresses so password reset emails can be sent during development/testing.

SMTP settings are configured in `appsettings.json` under:

```json
"EmailSettings": {
  "SmtpHost": "sandbox.smtp.mailtrap.io",
  "SmtpPort": 2525,
  "SmtpUser": "4d19669f0d9a6b",
  "SmtpPassword": "the-full-password-from-mailtrap",
  "SenderEmail": "no-reply@pawconnect.local",
  "SenderName": "PawConnect",
  "EnableSsl": true
}
```

Do not commit real passwords or app passwords. For local development, put real values in `appsettings.Development.json`, .NET User Secrets, or environment variables.

Example User Secrets setup:

```bash
dotnet user-secrets set "EmailSettings:SmtpHost" "sandbox.smtp.mailtrap.io"
dotnet user-secrets set "EmailSettings:SmtpPort" "2525"
dotnet user-secrets set "EmailSettings:SmtpUser" "4d19669f0d9a6b"
dotnet user-secrets set "EmailSettings:SmtpPassword" "the-full-password-from-mailtrap"
dotnet user-secrets set "EmailSettings:SenderEmail" "no-reply@pawconnect.local"
dotnet user-secrets set "EmailSettings:SenderName" "PawConnect"
dotnet user-secrets set "EmailSettings:EnableSsl" "true"
```

Mailtrap sandbox is useful for development because it captures test emails and lets you inspect PDF attachments without delivering them to real inboxes. If you switch to Gmail SMTP later, Gmail requires an App Password.

## Shelter Map Integration

PawConnect uses OpenStreetMap with Leaflet for shelter location maps and editable coordinate previews in shelter forms. No Google Maps API key or paid map service is required.

Shelter coordinates are stored directly on the `Shelter` entity as optional `Latitude` and `Longitude` values. Shelters without coordinates still work normally and show a friendly fallback message instead of a broken map.

Public shelter details keep the embedded Leaflet/OpenStreetMap map and also include an external "Open in Google Maps" link for navigation. The link opens in a new tab, uses stored coordinates when available, and falls back to the public address/city query when coordinates are missing. This external link does not require a Google Maps API key.

Shelter applications and admin shelter editing use address information as the primary input. Latitude/Longitude are optional derived fields. Public applicants do not manually type coordinates; they can click "Find location" to perform a manual OpenStreetMap Nominatim lookup from address + city + Romania. The app does not geocode while typing, and applicants can still submit without coordinates if lookup fails.

The public shelter application form uses an editable map as the user-facing coordinate editor. After address lookup, applicants can drag the marker or click the map to adjust the shelter location. Latitude and Longitude are stored internally, remain optional, and are not shown as raw numeric fields to public applicants or normal shelter location edit forms. Public shelter details maps remain read-only.

Moving the marker or clicking the editable map can produce a suggested address through a reverse lookup. The "Update address from pin" action applies the displayed suggestion to the address/city fields; marker movement by itself does not automatically overwrite address fields.

The public shelter pages are:

- `/shelters`
- `/shelters/{id:int}`

Demo shelters use approximate, fictional Cluj-Napoca, Romania locations for development and testing. The demo addresses and coordinates are not real shelter addresses and should not be treated as public contact/location data for real organizations.

After adding the map coordinate fields, apply migrations with:

```bash
dotnet tool run dotnet-ef database update
```

Nominatim integration notes:

- Used only for low-volume/manual location lookup and explicit address updates from the selected pin.
- No Google Maps API key is required.
- Users select coordinates through address lookup and the editable map marker, not raw coordinate inputs.
- Coordinates can be adjusted by dragging the map marker or clicking the editable map in shelter forms.
- Moving the marker does not automatically overwrite address/city fields; users can review the suggested address and click "Update address from pin" when they want that update.
- Missing coordinates show a friendly map fallback.
- Route planning, autocomplete, browser geolocation, nearby search, and distance filtering are not implemented.

## Database

The connection string points to a SQL Server database named `PawConnect`:

```json
"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=PawConnect;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

If your SSMS 2022 database uses another SQL Server instance, update only the `Server=` value in `appsettings.json`. For example:

```json
"Server=.\\SQLEXPRESS;Database=PawConnect;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

## How To Run

```bash
dotnet restore
dotnet build
dotnet tool restore
dotnet tool run dotnet-ef database update
dotnet run
```

Open the URL shown in the terminal, usually `https://localhost:7xxx` or `http://localhost:5xxx`.

## Tests

Run the service/domain test suite with:

```bash
dotnet test
```

The `PawConnect.Tests` project covers key business rules for dog management, dog image handling, adoption requests, favorites, shelter resources, shelter registration requests, Nominatim geocoding behavior, and PDF report generation. It also includes service-flow integration tests for public dog visibility, favorite deletion behavior, adoption request status changes, dog image/age behavior, resource stock rules, and email/PDF notification triggers.

Tests use isolated in-memory databases and fake email/PDF services. They do not require SQL Server, Mailtrap, a running web server, or browser UI automation.

## Migrations

Create a new migration after changing entities:

```bash
dotnet tool run dotnet-ef migrations add MigrationName
```

Apply migrations to the `PawConnect` database:

```bash
dotnet tool run dotnet-ef database update
```

The latest map coordinate migration is:

```text
20260511184208_AddShelterRegistrationRequests
```

If `dotnet ef database update` cannot connect from your terminal, check that SQL Server/LocalDB is running and that the `DefaultConnection` server name matches the `PawConnect` database you created in SSMS.

## Test Users

All seeded users use this password:

```text
PawConnect123!
```

| Role | Email |
| --- | --- |
| Adopter | adopter@test.com |
| Shelter | u8878233525@id.gle |
| Admin | admin@test.com |

Newly registered users are assigned the `Adopter` role by default.
