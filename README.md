# PawConnect: A Web Platform for Stray Dog Adoption and Shelter Management

PawConnect is a beginner-friendly ASP.NET Core Blazor Server skeleton for a stray dog adoption and shelter management system. It is structured for a bachelor thesis project and is ready for future CRUD and workflow implementation.

## Technologies

- ASP.NET Core Blazor Server
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity with roles
- MudBlazor
- Quartz.NET for scheduled background jobs

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
- Email notification service with SMTP/local catcher support for development testing
- PDF email report attachments for adoption and low-stock notifications
- Scheduled shelter summary reports with PDF email attachments and manual Shelter Dashboard sending
- Adopter profile page for household/contact information used during adoption request review
- Dog status history tracking for shelter/admin review
- Internal shelter notes for adoption requests, visible only to shelter users and admins
- Recently viewed dogs for adopter dashboard quick access
- Public adoption success stories for adopted dogs
- Dog ages support years and months for puppy-friendly display
- Adopter-selected shelter visit scheduling for adoption requests
- Approval-based shelter registration requests at `/shelters/apply`
- Admin shelter application review at `/admin/shelter-requests`
- Admin CSV/PDF exports for platform management pages
- Shelter CSV/PDF exports for shelter-owned operational pages
- CSV imports for shelter-owned resource/dog records and Admin shelter request imports with preview/validation
- Admin Activity Log for important user and system actions
- Role-based in-app notifications with categorized notification bell and `/notifications` page
- Optional address-based coordinate lookup using OpenStreetMap Nominatim
- Service-level validation and duplicate prevention for core shelter, adoption, dog image, resource, export, notification, and report workflows
- Report History metadata tracking for generated/sent reports and CSV/PDF exports

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

## Validation and Duplicate Prevention

PawConnect uses friendly UI validation together with service-level safeguards for important business rules. UI checks help users fix forms quickly, but ownership, duplicate prevention, and status transition rules are enforced in services so they are not bypassed by page changes.

Important rules include:

- Shelter application emails are trimmed and compared case-insensitively. A duplicate pending shelter application is blocked, and existing shelter account/profile emails cannot be reused for a new shelter application or approval.
- Adoption requests are limited to requestable dogs. Duplicate active requests from the same adopter for the same dog are blocked, adopted/in-treatment dogs cannot receive new requests, and adopters choose a preferred future shelter visit time during submission.
- Preferred visit times are validated against the shelter's visiting hours. Closed days, past times, and times outside the configured visit range are rejected with friendly messages.
- Shelter review is split into visit confirmation and final adoption. Confirming a visit reserves the dog and sends the adopter an email/in-app notification with an `.ics` calendar attachment. Marking as adopted happens later, after the visit.
- Shelter users can manage adoption requests only for dogs owned by their shelter. Adopters can cancel only their own pending requests.
- Dog image URLs are trimmed, empty URLs are ignored, and the same image URL cannot be added twice to the same dog.
- Dog forms validate required identity fields, non-negative age/food values, valid month ranges, and avoid saving unusable dog age data.
- Dogs with adoption request history cannot be hard deleted. Favorites and recently viewed rows do not block deletion when no adoption request history exists.
- Resource stock validates name, category, unit, non-negative quantity/threshold, required food type for food resources, cleared food type for non-food resources, and duplicate shelter stock items.
- Shelter address and city remain separate. Latitude/Longitude are optional internal map fields and are range-checked when present; missing or failed geocoding does not block application or shelter creation.
- Admin and Shelter exports are role-protected and scoped to the correct data. Sensitive Identity fields such as password hashes, security stamps, concurrency stamps, and tokens are intentionally excluded.
- In-app notification read/delete operations check ownership, and audit logging avoids sensitive values such as passwords, reset tokens, security stamps, and SMTP credentials.

Validation failures use user-friendly messages instead of raw technical exceptions. Field-specific problems are shown next to the relevant MudBlazor input, multi-field validation appears as a compact in-form alert near the related section, and snackbars are reserved mostly for success messages, system feedback, and failures that are not tied to a single field.

## Adoption Visit Scheduling

Adoption requests remain the main concept in PawConnect, but the first positive shelter decision is now a visit confirmation rather than final adoption approval.

The flow is:

```text
Adopter submits adoption request with preferred visit time
-> PawConnect validates the time against shelter visiting hours
-> Shelter confirms the visit or rejects the request
-> Adopter receives email/in-app notification with an .ics calendar attachment
-> After the visit, the shelter marks the dog/request as adopted or rejects/closes the request
```

Shelter visiting hours are stored on the `Shelter` profile with one daily time range and allowed visit weekdays. Admin users can edit these hours from `/admin/shelters`. Demo/default hours are Monday-Friday, 10:00-17:00.

Visit confirmation reserves the dog but does not mark it as adopted. Final adoption happens only through the later "Mark as Adopted" action after the visit.

## Visit Reminder Emails

PawConnect can send automatic visit reminder emails 24 hours before confirmed shelter visits. The reminder uses Quartz.NET and the same SMTP/email template infrastructure as the confirmation email.

Reminder emails include:

- dog name
- shelter name and contact details
- visit date/time
- shelter address/city
- a generated `.ics` calendar attachment

The reminder job is `VisitReminderJob`. It calls `IVisitReminderService`, which finds confirmed visits that are approximately 24 hours away and have not already received a reminder. Each adoption request stores `VisitReminderSentAt`; once it has a value, PawConnect will not send another reminder for that visit.

Visit reminders are configured in `appsettings.json`:

```json
"VisitReminders": {
  "Enabled": false,
  "CheckIntervalMinutes": 30,
  "ReminderHoursBeforeVisit": 24
}
```

`Enabled` is `false` by default to avoid accidental development email spam. For local testing, enable it and use a local SMTP catcher such as smtp4dev. The reminder job does not use Google Calendar API/OAuth and does not store `.ics` files in the database.

## Email Notifications

PawConnect uses `IEmailService` with `SmtpEmailService` as the active implementation. The service sends email through a generic SMTP server using MailKit. Important notifications include a branded PawConnect HTML layout with a plain text fallback, so a local SMTP catcher can be used to inspect readable text bodies, richer HTML previews, and PDF attachments during development.

Email notifications are triggered when:

- A user requests a password reset through Forgot Password, sending an Identity reset link.
- Identity account confirmation and email verification messages are sent.
- An adopter submits a new adoption request, notifying the owning shelter.
- A shelter confirms an adoption visit, notifying the adopter with a calendar invitation attachment.
- A confirmed adoption visit is about 24 hours away, reminding the adopter with another calendar invitation attachment.
- A shelter rejects or finalizes an adoption request after review/visit, notifying the adopter.
- A shelter creates or updates a resource stock item that is at or below its low-stock threshold, notifying the shelter.

Some notifications include PDF report attachments:

- `AdoptionRequestReport.pdf` is attached when a new adoption request is sent to a shelter.
- `AdoptionStatusReport.pdf` is attached when a request is finalized as adopted after the visit or rejected and the adopter is notified.
- `LowStockResourceReport.pdf` is attached when a resource reaches low stock.
- `ShelterSummaryReport-{yyyy-MM-dd}.pdf` is attached when a shelter summary report is sent manually or by the scheduler.

Confirmed shelter visits and 24-hour visit reminders include an iCalendar invitation named `adoption-visit-{dog-name}-{yyyy-MM-dd}.ics`. PawConnect sends it as a `text/calendar; method=REQUEST` MIME part so compatible email clients may show an Add/Accept calendar action, and also keeps the `.ics` file as an attachment fallback. PawConnect does not use Google Calendar API/OAuth or Microsoft Graph; the generated `.ics` file can still be imported by common calendar applications.

The reports are generated without charts. They use a clean PawConnect-style text and table layout with section headings, spacing, and a generated-date footer.

Email or PDF generation failures are logged and do not cancel the main database action. For example, an adoption request can still be submitted even if SMTP credentials are missing or invalid.

Forgot Password and other ASP.NET Core Identity emails use `PawConnectIdentityEmailSender`, which reuses the same configured `IEmailService` SMTP settings as the rest of the application. Password reset emails include a clean plain text body with the reset URL on its own line for easy copying from a local development inbox, plus a branded HTML body with a clickable action button when HTML preview is available. Demo users are seeded with confirmed email addresses so password reset emails can be sent during development/testing.

## In-App Notifications

PawConnect also stores important user-facing events as private in-app notifications. These complement email notifications and snackbar feedback; they do not replace either one.

Authenticated users see a notification bell in the top app bar with an unread count. The dropdown groups recent notifications by category and links to the full notifications page:

```text
/notifications
```

Notification categories are:

- Adoption
- Shelter Applications
- Resources
- Reports
- System

The bell dropdown is intentionally compact: it shows a limited set of recent notifications, groups them by category, and collapses repeated same-day notifications with the same title/message, such as frequent scheduled "Summary report sent" notifications. The full notification history remains available on `/notifications`.

Examples of role-based notifications:

- Adopters receive notifications when a shelter visit is confirmed.
- Adopters receive notifications when a 24-hour visit reminder is sent.
- Adopters receive notifications when their adoption requests are finalized as adopted after the visit or rejected.
- Shelters receive notifications for new adoption requests with preferred visit times, cancelled adopter requests, low-stock resources, and sent summary reports.
- Admins receive notifications when a new shelter application is submitted.
- Admins receive one summary notification when shelter application requests are imported from CSV.

Scheduled shelter summary report notifications also use a simple duplicate guard so an enabled short demo interval does not create the same in-app report notification every minute. The report emails/PDF attachments are still sent according to the scheduler; only duplicate in-app notification clutter is reduced.

Notifications belong to a single `ApplicationUser`. Users can view, mark as read, mark all as read, and delete only their own notifications. Notification text avoids sensitive values such as passwords, reset tokens, security stamps, and SMTP credentials.

Email delivery uses the generic `SmtpEmailService`. The default development setup uses a local SMTP catcher so emails can be inspected without sending anything to real inboxes:

```json
"EmailSettings": {
  "SmtpHost": "localhost",
  "SmtpPort": 2525,
  "SmtpUser": "",
  "SmtpPassword": "",
  "SenderEmail": "no-reply@pawconnect.local",
  "SenderName": "PawConnect",
  "EnableSsl": false,
  "OpenLocalInboxOnStartup": false,
  "LocalInboxUrl": "http://localhost:3000"
}
```

Do not commit real SMTP credentials. If a real SMTP provider is configured later, put real values in .NET User Secrets or environment variables.

## Local Email Testing

For development, use a local SMTP catcher so no real emails are sent. Local catchers also let you inspect password reset emails, adoption/resource/shelter notifications, scheduled reports, HTML bodies, plain text bodies, and PDF attachments in a browser.

Recommended option: smtp4dev.

```bash
docker run -d --name smtp4dev -p 3000:80 -p 2525:25 rnwood/smtp4dev
```

If smtp4dev is installed locally without Docker, run:

```powershell
smtp4dev --urls=http://localhost:3000 --smtpport=2525
```

smtp4dev web UI:

```text
http://localhost:3000
```

Development `EmailSettings` for smtp4dev:

```json
"EmailSettings": {
  "SmtpHost": "localhost",
  "SmtpPort": 2525,
  "SmtpUser": "",
  "SmtpPassword": "",
  "SenderEmail": "no-reply@pawconnect.local",
  "SenderName": "PawConnect",
  "EnableSsl": false,
  "OpenLocalInboxOnStartup": true,
  "LocalInboxUrl": "http://localhost:3000"
}
```

Alternative option: Mailpit.

```bash
docker run -d --name mailpit -p 1025:1025 -p 8025:8025 axllent/mailpit
```

Mailpit uses SMTP port `1025` and web UI `http://localhost:8025`.

The SMTP service connects without authentication when `SmtpUser` and `SmtpPassword` are empty, which is the expected local smtp4dev/Mailpit setup. Real SMTP providers can still be configured later through the same `EmailSettings` keys, but they are not the default development path.

In Development, PawConnect can open the local email inbox automatically when the app starts. This is controlled by `OpenLocalInboxOnStartup` and `LocalInboxUrl`; it opens the browser UI only and does not start the Mailpit/smtp4dev Docker container for you.

The same SMTP path sends branded HTML and plain text email bodies, password reset links, adoption/resource/shelter notifications, scheduled report emails, PDF/CSV attachments, and `.ics` calendar invitations. PawConnect does not use Google Calendar API, Outlook/Microsoft Graph, or OAuth for visit invitations.

## Scheduled Shelter Summary Reports

PawConnect uses Quartz.NET for optional scheduled shelter summary reports. The job is in-process and uses Quartz's simple in-memory scheduling. No external cron job, Hangfire server, Quartz dashboard, or persistent Quartz job store is required.

The scheduled job is `ShelterSummaryReportJob`. It calls `IShelterSummaryReportService`, which generates a PDF through `IPdfReportService` and sends it through the configured SMTP email service. The report summarizes:

- adoption request counts and new requests in the report period
- dog status counts and recently adopted dogs
- low-stock resources with category, food type, quantity, unit, and threshold

Shelter users can also send the same report manually from `/shelter/dashboard` with the "Send Summary Report" button. Manual sending works even when automatic scheduling is disabled, which is useful for demo/testing.

Scheduled report settings are configured in `appsettings.json`:

```json
"ScheduledReports": {
  "Enabled": false,
  "RunOnStartupInDevelopment": false,
  "ShelterReportIntervalMinutes": 5
}
```

`Enabled` is `false` by default to avoid accidental email spam during local development. A 5-minute interval is convenient for demos; production values should usually be larger, such as `1440` minutes for daily reports. `RunOnStartupInDevelopment` only sends at startup when it is explicitly set to `true` in development. When scheduled reports are enabled, each run sends a summary report to every shelter that has an email address.

## Report History

PawConnect stores generated/sent report metadata in `ReportHistories`. This tracks when reports and exports were generated, who received emailed reports, whether sending succeeded, the file name, trigger type, and related shelter/entity information.

Report history is metadata only:

- PDF and CSV bytes are not stored in the database.
- Password reset links, tokens, SMTP credentials, and other secrets are not stored.
- Failed send attempts store a short user-safe error message.
- History logging is best-effort and does not block email sending, PDF generation, scheduled reports, or browser downloads.

Tracked examples include:

- Manual Shelter Summary Reports from `/shelter/dashboard`
- Quartz scheduled Shelter Summary Reports
- Adoption request/status PDF notification reports
- Low-stock resource PDF reports
- Shelter registration request PDF reports
- Admin and Shelter CSV/PDF exports

Shelter users see recent metadata for their own shelter on `/shelter/dashboard`. Admin users can review all records at:

```text
/admin/report-history
```

Stored report download from history is intentionally not implemented because PawConnect does not persist generated files.

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

The public `/shelters` page lists approved shelter profiles with public-safe contact/location details, public dog counts, a simple search by shelter name/city/address, and an "Apply as a Shelter" call-to-action that points to `/shelters/apply`. The application CTA is hidden for Admin and Shelter users because those roles either review applications or already have active shelter accounts.

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

## Admin Exports

Admin pages include export actions for platform data. Exports are generated on demand and downloaded in the browser; files are not stored in the database.

CSV exports are available for:

- `/admin/users`
- `/admin/shelters`
- `/admin/dogs`
- `/admin/adoption-requests`
- `/admin/shelter-requests`

PDF exports are available where a formatted summary is useful:

- `/admin/adoption-requests`
- `/admin/shelter-requests`

CSV files are UTF-8 encoded with a header row and can be opened in Excel. PDF files use a clean PawConnect report layout generated with QuestPDF. Admin user exports intentionally exclude sensitive Identity fields such as password hashes, security stamps, concurrency stamps, and reset tokens.

Export buttons are shown only on Admin pages, which are protected with the `Admin` role. If export logic is reused later through endpoints, those endpoints should also require Admin authorization.

## Shelter Exports

Shelter pages include export actions for the authenticated shelter's own operational data. Exports are generated on demand and downloaded in the browser; files are not stored in the database.

CSV exports are available for:

- `/shelter/dogs`
- `/shelter/adoption-requests`
- `/shelter/resources`

PDF exports are available where a formatted report is useful:

- `/shelter/adoption-requests`
- `/shelter/resources`

CSV files are UTF-8 encoded with a header row and can be opened in Excel. PDF files use a clean PawConnect report layout generated with QuestPDF. Shelter exports are scoped by the current shelter profile, so a shelter user can export only their own dogs, adoption requests, and resource stock.

## CSV Imports

PawConnect supports CSV import for shelter-owned operational data. Imports use a preview-and-validate workflow:

1. The Shelter user chooses a `.csv` file.
2. PawConnect parses the header row and validates every row.
3. The page shows total, valid, and invalid rows with row-level errors.
4. The user confirms the import only after the preview is valid.
5. Rows are saved for the current shelter only.

The workflow is intentionally all-or-nothing: invalid rows are not imported silently. The user fixes the CSV and previews it again before confirming. Uploaded files are processed in memory, limited to small `.csv` files, and are not stored permanently.

Supported imports:

- `/shelter/resources`: Resource stock import
- `/shelter/dogs`: Dog profile import
- `/admin/shelters`: Shelter registration request import for Admin users

Resource CSV template columns:

```csv
Name,Category,FoodType,Quantity,Unit,LowStockThreshold
Adult dry food,Food,Adult dry food,25,kg,10
Blankets,Blankets,,12,pieces,5
Cleaning spray,Cleaning Supplies,,8,bottles,3
```

Dog CSV template columns:

```csv
Name,Breed,AgeYears,AgeMonths,Size,Status,Location,Description,PreferredFoodType,DailyFoodAmount,ImageUrls
Buddy,Labrador Mix,2,6,Large,Available,Cluj-Napoca,Friendly dog,Adult dry food,480,"https://example.com/dog1.jpg;https://example.com/dog2.jpg"
```

Template download buttons are available on the import pages:

- `pawconnect-resource-import-template.csv`
- `pawconnect-dog-import-template.csv`
- `pawconnect-shelter-requests-import-template.csv`

Validation rules include required names/categories/units, non-negative quantities and thresholds, dog age validation, enum validation for dog size/status, optional semicolon-separated dog image URLs, duplicate row detection, and shelter ownership scoping. Existing resource stock items for the same shelter/name/category/food type are updated; new resource rows are created.

Successful imports are recorded in the Activity Log with row counts, without storing full CSV content.

Admin Shelter CSV import preserves the approval-based onboarding flow. Rows are imported as pending `ShelterRegistrationRequest` records, not as direct shelter accounts. Admins still review imported requests at `/admin/shelter-requests`; accepting a request uses the existing approval flow to create the `ApplicationUser`, assign the `Shelter` role, and create the linked `Shelter` profile.

Admin shelter request template columns:

```csv
ShelterName,ContactPersonName,Email,PhoneNumber,City,Address,Description,Website,OpeningHours,ReasonForJoining,Latitude,Longitude
Happy Tails Rescue,Alex Popescu,happytails@example.com,+40 700 000 100,Cluj-Napoca,Strada Exemplu 10,Fictional demo shelter for PawConnect testing,https://example.com,Mon-Fri 09:00-17:00,We want to list adoptable dogs,46.7712,23.6236
```

Duplicate pending shelter request emails and existing shelter/user emails are blocked case-insensitively. Latitude and longitude are optional but range-checked when present. Address and city are normalized separately so imported seed/demo-style rows do not duplicate the city inside the address.

## Audit Log / Activity Log

PawConnect records important activity in the `AuditLogs` table for traceability and administrative monitoring. The Admin-only page is:

```text
/admin/activity-log
```

Tracked actions include important dog management, dog image, medical record, adoption request, shelter registration request, shelter/resource update, report, and export events. Background actions are logged as `System` when no user is involved.

The activity log intentionally does not store passwords, reset tokens, security stamps, SMTP credentials, or other sensitive Identity/security values. It is a lightweight activity log for accountability, not full event sourcing, rollback, or historical data restoration.

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

The `PawConnect.Tests` project covers key business rules for dog management, dog image handling, adoption requests, favorites, shelter resources, shelter registration requests, Nominatim geocoding behavior, scheduled shelter summary reports, PDF report generation, admin export generation, shelter export generation, CSV import validation/import behavior, report history metadata tracking, audit log behavior, and in-app notification ownership/triggers. It also includes service-flow integration tests for public dog visibility, favorite deletion behavior, adoption request status changes, dog image/age behavior, resource stock rules, and email/PDF notification triggers.

Tests use isolated in-memory databases and fake email/PDF services. They do not require SQL Server, a real SMTP provider, a running web server, or browser UI automation.

## Migrations

Create a new migration after changing entities:

```bash
dotnet tool run dotnet-ef migrations add MigrationName
```

Apply migrations to the `PawConnect` database:

```bash
dotnet tool run dotnet-ef database update
```

Recent feature migrations include:

```text
20260512203656_AddAuditLogs
20260512210309_AddNotifications
20260512222313_AddReportHistory
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
