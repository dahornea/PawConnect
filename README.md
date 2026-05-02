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
- Domain entities for shelters, dogs, dog images, medical records, adoption requests, favorite dogs, and resource stock
- Seed roles, test users, and demo domain data at startup after the database schema exists
- Demo data includes one shelter, five dogs, dog images, medical records, and resource stock
- MudBlazor layout with role-based sidebar navigation
- Placeholder pages for public, adopter, shelter, and admin workflows
- Simple service and repository layer
- Mock email service that logs messages instead of sending SMTP email

## Planned Features

- Dog CRUD for shelters
- Adoption request workflow
- Favorite dogs for adopters
- Shelter resource stock management
- Admin review screens
- Real email notifications
- Image upload support

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

The latest domain migration is:

```text
20260502154612_CompleteDomainModel
```

## Test Users

All seeded users use this password:

```text
PawConnect123!
```

| Role | Email |
| --- | --- |
| Adopter | adopter@test.com |
| Shelter | shelter@test.com |
| Admin | admin@test.com |

Newly registered users are assigned the `Adopter` role by default.
