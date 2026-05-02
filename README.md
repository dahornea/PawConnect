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
- Demo data includes one shelter, five dogs, dog images, medical records, resource categories, food types, and resource stock
- MudBlazor layout with role-based sidebar navigation
- Placeholder pages for public, adopter, shelter, and admin workflows
- Simple service and repository layer
- SMTP email notification service using configuration-based settings
- Adopter profile page for household/contact information used during adoption request review
- Dog status history tracking for shelter/admin review
- Internal shelter notes for adoption requests, visible only to shelter users and admins
- Recently viewed dogs for adopter dashboard quick access

## Planned Features

- Dog CRUD for shelters
- Adoption request workflow
- Favorite dogs for adopters
- Shelter resource stock management
- Resource categories and food-type tracking for shelter inventory
- Admin review screens
- Image upload support

## Email Notifications

PawConnect uses `IEmailService` with `SmtpEmailService` as the active implementation. The service sends plain text emails through SMTP using MailKit.

Email notifications are triggered when:

- An adopter submits a new adoption request, notifying the owning shelter.
- A shelter accepts or rejects an adoption request, notifying the adopter.
- A shelter creates or updates a resource stock item that is at or below its low-stock threshold, notifying the shelter.

Email failures are logged and do not cancel the main database action. For example, an adoption request can still be submitted even if SMTP credentials are missing or invalid.

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

Mailtrap sandbox is useful for development because it captures test emails instead of delivering them to real inboxes. If you switch to Gmail SMTP later, Gmail requires an App Password.

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

## Migrations

Create a new migration after changing entities:

```bash
dotnet tool run dotnet-ef migrations add MigrationName
```

Apply migrations to the `PawConnect` database:

```bash
dotnet tool run dotnet-ef database update
```

The latest recently viewed dogs migration is:

```text
20260502221155_AddRecentlyViewedDogs
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
