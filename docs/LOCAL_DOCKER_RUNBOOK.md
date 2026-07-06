# PawConnect Local Docker Runbook

This runbook explains how to run PawConnect as a zero-cost local demo environment using Docker Compose. It is intended for recruiters, interviewers, reviewers, and developers who want to evaluate the application without installing SQL Server locally or configuring paid external services.

## 1. What This Starts

The Docker Compose stack starts these local services:

| Service | Purpose | Default local URL/port |
| ------- | ------- | ---------------------- |
| `pawconnect` | ASP.NET Core Blazor Server web app | `http://localhost:8080` |
| `sqlserver` | SQL Server 2022 Developer container | `localhost:1433` |
| `smtp4dev` | Local email inbox for captured emails | `http://localhost:5001` |

No paid hosting, paid database, paid email provider, or paid AI API is required. OpenAI-backed features remain disabled by default.

## 2. Prerequisites

Required:

- Docker Desktop or Docker Engine
- Git

Optional:

- .NET SDK 10 if you also want to build or test outside Docker
- SQL Server Management Studio or Azure Data Studio if you want to inspect the container database manually

## 3. Quick Start

Clone the repository:

```bash
git clone https://github.com/dahornea/PawConnect.git
cd PawConnect
```

Create a local Docker environment file:

```powershell
Copy-Item .env.example .env
```

On macOS/Linux:

```bash
cp .env.example .env
```

Review `.env`. The example password is local-demo safe, but you can replace `SQL_PASSWORD` with another strong password that satisfies SQL Server complexity rules.

Start the full local stack:

```bash
docker compose up --build
```

If Docker reports `unknown command: docker compose`, use the legacy Compose command instead:

```bash
docker-compose up --build
```

Open the app:

```text
PawConnect: http://localhost:8080
Swagger UI: http://localhost:8080/swagger
OpenAPI JSON: http://localhost:8080/swagger/v1/swagger.json
Health: http://localhost:8080/health
smtp4dev inbox: http://localhost:5001
```

## 4. Demo Users

When migrations and seed data are enabled, PawConnect creates local demo users from `Data/IdentitySeedData.cs`.

| Role | Email | Password | Good pages to inspect |
| ---- | ----- | -------- | --------------------- |
| Admin | `admin@mail.com` | `Admin1!` | `/admin/dashboard`, `/admin/users`, `/admin/search-index`, `/admin/analytics` |
| Shelter | `shelter@mail.com` | `Shelter1!` | `/shelter/dashboard`, `/shelter/dogs`, `/shelter/adoption-requests`, `/shelter/resources` |
| Adopter | `adopter@mail.com` | `Adopter1!` | `/dogs`, `/adopter/dashboard`, `/adopter/copilot`, `/my-adoption-requests` |
| Volunteer | `volunteer@mail.com` | `Volunteer1!` | `/volunteer/tasks`, `/notifications`, `/notification-preferences` |

These credentials are for local demo data only. Do not reuse them for production.

## 5. How Database Setup Works

The Docker environment uses the `Docker` ASP.NET Core environment and the SQL Server container connection string from `docker-compose.yml`.

By default:

- `Database__ApplyMigrationsOnStartup=true`
- `SeedData__Enabled=true`
- the database is stored in the named Docker volume `sqlserver-data`
- uploaded files are stored in the named Docker volume `pawconnect-uploads`

This means the first `docker compose up --build` creates the schema and local demo data automatically. Existing Docker volume data is preserved between restarts.

## 6. Common Commands

Start or rebuild the local stack:

```bash
docker compose up --build
```

Legacy fallback if needed:

```bash
docker-compose up --build
```

Start in detached mode:

```bash
docker compose up --build -d
```

View web app logs:

```bash
docker compose logs -f pawconnect
```

View SQL Server logs:

```bash
docker compose logs -f sqlserver
```

Stop containers but keep data volumes:

```bash
docker compose down
```

Rebuild the app image without resetting the database:

```bash
docker compose build --no-cache pawconnect
docker compose up
```

Reset the local Docker demo database and uploads:

```bash
docker compose down -v
docker compose up --build
```

Warning: `docker compose down -v` deletes the local SQL Server and upload volumes. Use it only when you want a clean local demo database.

## 7. Running Tests Locally

Run the normal automated test suite outside Docker:

```bash
dotnet test PawConnect.sln
```

Run Release tests:

```bash
dotnet test PawConnect.sln --configuration Release
```

The SQL Server integration tests use Testcontainers and require Docker Desktop/Engine:

```bash
dotnet test PawConnect.IntegrationTests/PawConnect.IntegrationTests.csproj
```

If Docker is not available, those tests are skipped by the test fixture. You can skip Docker-backed tests explicitly:

```powershell
$env:PAWCONNECT_SKIP_DOCKER_TESTS = "1"
dotnet test PawConnect.sln
```

## 8. Swagger and API Notes

Swagger is enabled in `Development` and `Docker` environments.

Useful local URLs:

```text
Swagger UI: http://localhost:8080/swagger
OpenAPI JSON: http://localhost:8080/swagger/v1/swagger.json
```

Implemented API groups include:

- `/api/v1/dogs`
- `/api/v1/shelters`
- `/api/v1/adoption-applications`
- `/api/v1/saved-searches`
- `/api/v1/notification-preferences`
- `/api/v1/transfers`
- `/api/v1/volunteer-tasks`
- `/api/v1/admin`

Some endpoints are public. Protected endpoints use the same ASP.NET Core Identity cookie authentication as the Blazor UI. Sign in through the web app in the same browser before trying protected Swagger endpoints.

## 9. Optional Integrations

OpenAI-backed features are disabled by default in Docker:

```text
OPENAI_ENABLED=false
OPENAI_API_KEY=
```

The app still runs without an OpenAI key. If you want to test optional AI features locally, set these values in `.env` and restart the stack. Do not commit `.env`.

Email is routed to smtp4dev by default. No real SMTP credentials are needed.

## 10. Troubleshooting

### Docker is not running

Start Docker Desktop, wait until it says the engine is running, then retry:

```bash
docker compose up --build
```

### SQL Server is still starting

The SQL Server container can take 30-90 seconds on the first run. The web app waits for the SQL Server health check, but if startup still fails, run:

```bash
docker compose logs -f sqlserver
```

Then restart:

```bash
docker compose up
```

### SQL password rejected

SQL Server requires a strong `SA` password. Update `SQL_PASSWORD` in `.env` and recreate the database volume:

```bash
docker compose down -v
docker compose up --build
```

### Port already in use

Change ports in `.env`:

```text
APP_HTTP_PORT=8081
SQLSERVER_HOST_PORT=11433
SMTP4DEV_WEB_PORT=5002
SMTP4DEV_SMTP_HOST_PORT=2526
```

Then restart Docker Compose.

### App cannot connect to SQL Server

Check that the `sqlserver` service is healthy:

```bash
docker compose ps
docker compose logs -f sqlserver
```

If the schema is old or migrations failed, reset the local volume:

```bash
docker compose down -v
docker compose up --build
```

### Swagger is not visible

Swagger is enabled when `ASPNETCORE_ENVIRONMENT` is `Docker` or `Development`. Confirm the environment in `.env` or `docker-compose.yml`, then open:

```text
http://localhost:8080/swagger
```

### Old database volume has an outdated schema

If you changed branches or pulled new migrations, reset the Docker database volume for a clean local demo:

```bash
docker compose down -v
docker compose up --build
```

## 11. Zero-Cost Local Demo Checklist

Before showing PawConnect locally:

- Docker Desktop is running.
- `.env` exists and contains a strong local `SQL_PASSWORD`.
- `docker compose up --build` starts all services.
- `http://localhost:8080/health` returns healthy status.
- `http://localhost:8080` loads the app.
- Demo users can log in.
- `http://localhost:5001` shows captured local emails.
- Optional OpenAI settings are either disabled or configured through local `.env` only.
