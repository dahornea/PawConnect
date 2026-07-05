# PawConnect Deployment Preparation

This document describes how to prepare PawConnect for a production or portfolio-demo deployment. It does not deploy the application by itself.

PawConnect can run locally with SQL Server/LocalDB, with Docker Compose, or on a hosted ASP.NET Core environment that provides a SQL Server connection string and persistent file storage.

## 1. Required Runtime

- .NET SDK/runtime: `10.0.301` or compatible .NET 10 runtime
- Database: SQL Server
- Persistent storage for uploaded files if dog images or message attachments are used
- Optional SMTP provider for email delivery
- Optional OpenAI API key for AI-assisted features

## 2. Required Environment Variables

| Variable | Required | Example placeholder | Purpose |
| -------- | -------- | ------------------- | ------- |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` | Loads production-safe configuration. |
| `ConnectionStrings__DefaultConnection` | Yes | `Server=tcp:your-sql-host,1433;Database=PawConnect;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True` | SQL Server connection string. Store as a platform secret. |

Do not commit real connection strings or database passwords.

## 3. Optional Environment Variables

| Variable | Default | Purpose |
| -------- | ------- | ------- |
| `Database__ApplyMigrationsOnStartup` | `false` in Production | Applies EF Core migrations at app startup only when explicitly enabled. |
| `SeedData__Enabled` | `false` in Production | Creates demo roles/users/shelters/dogs when enabled. Keep disabled for real production data. |
| `OpenAI__Enabled` | `false` in Production | Enables optional OpenAI-backed features. |
| `OpenAI__ApiKey` | empty | OpenAI API key. Required only when `OpenAI__Enabled=true`. |
| `OpenAI__Model` | `gpt-5.4-mini` | Default model name for OpenAI chat features. |
| `OpenAI__ChatModel` | `gpt-5.4-mini` | Chat model used by Copilot/recommendations/summaries. |
| `OpenAI__EmbeddingModel` | `text-embedding-3-small` | Embedding model used by semantic dog search. |
| `OpenAI__DogProfileQualityEnabled` | `false` in Production | Enables AI dog profile quality checks. |
| `OpenAI__ReportSummariesEnabled` | `false` in Production | Enables AI-generated report summaries. |
| `OpenAI__ShelterOperationsAssistantEnabled` | `false` in Production | Enables the shelter operations assistant. |
| `EmailSettings__Enabled` | `false` in Production | Enables SMTP email sending. In-app notifications still work when email is disabled. |
| `EmailSettings__SmtpHost` | empty | SMTP host. |
| `EmailSettings__SmtpPort` | `587` | SMTP port. |
| `EmailSettings__SmtpUser` | empty | SMTP username. |
| `EmailSettings__SmtpPassword` | empty | SMTP password. Store as a secret. |
| `EmailSettings__SenderEmail` | `no-reply@pawconnect.local` | Sender email address. |
| `EmailSettings__SenderName` | `PawConnect` | Sender display name. |
| `EmailSettings__EnableSsl` | `true` in Production | Enables SMTP TLS/SSL behavior. |
| `ScheduledReports__Enabled` | `false` in Production | Enables Quartz shelter summary report emails. |
| `ScheduledReports__ShelterReportIntervalMinutes` | `1440` in Production | Shelter summary report interval. |
| `VisitReminders__Enabled` | `false` in Production | Enables Quartz visit reminder emails. |
| `VisitReminders__CheckIntervalMinutes` | `60` | Visit reminder polling interval. |
| `DogImageStorage__LocalRoot` | `uploads/dogs` | Relative upload path under `wwwroot`. |
| `DogImageStorage__MaxFileSizeBytes` | `5242880` | Maximum dog image upload size. |

## 4. Database Migration Strategy

Recommended production strategy:

1. Back up the target database.
2. Apply migrations manually during deployment:

```bash
dotnet ef database update --project PawConnect.csproj
```

3. Start or restart the application.

Automatic startup migrations are disabled by default in Production:

```text
Database__ApplyMigrationsOnStartup=false
```

For a temporary demo environment, automatic migrations can be enabled explicitly:

```text
Database__ApplyMigrationsOnStartup=true
```

Do not enable automatic production migrations unless the deployment process is intentionally designed for it.

## 5. Seed and Demo Data Strategy

PawConnect has demo seed data in `Data/IdentitySeedData.cs`. It creates roles, demo users, lookup data, shelters, dogs, resources, medical records, and demo adoption requests.

Production default:

```text
SeedData__Enabled=false
```

Development and Docker demo environments keep seeding enabled by default.

The seed process is intended to be idempotent, but it is demo-oriented. For real production data, keep seeding disabled and create real users/data through normal application workflows or controlled administration scripts.

Demo accounts should not be used as real production credentials.

## 6. OpenAI Behavior

OpenAI is optional.

Production default:

```text
OpenAI__Enabled=false
OpenAI__ApiKey=
```

When OpenAI is disabled, missing an API key, or returns unusable output:

- Adoption Copilot falls back to deterministic/keyword/semantic-safe behavior where available.
- Recommendations continue with rule-based scoring.
- Search index rebuilds report that embeddings are unavailable instead of crashing.
- AI summaries/profile checks return safe fallback messages.

Safety notes:

- OpenAI never receives direct SQL access.
- Copilot can only display dog IDs validated against PawConnect backend candidates.
- Do not log or commit API keys.
- Do not send private adopter contact details, passwords, tokens, SMTP credentials, or audit data to OpenAI.

## 7. SMTP and Email Behavior

Email is optional.

Production default:

```text
EmailSettings__Enabled=false
```

When email is disabled or SMTP settings are incomplete:

- The application still starts.
- Core adoption workflows continue.
- In-app notifications still work.
- Email delivery attempts are skipped or logged safely.
- SMTP credentials are not logged.

To enable production email, configure:

```text
EmailSettings__Enabled=true
EmailSettings__SmtpHost=your-smtp-host
EmailSettings__SmtpPort=587
EmailSettings__SmtpUser=your-smtp-user
EmailSettings__SmtpPassword=your-secret-password
EmailSettings__SenderEmail=no-reply@your-domain.example
EmailSettings__EnableSsl=true
```

Use smtp4dev only for local development or staging demos.

## 8. File Upload and Storage Behavior

PawConnect stores local uploaded files under `wwwroot/uploads` by default.

Current upload areas include:

- dog images
- message attachments

Production notes:

- Store only relative paths in the database.
- Keep the upload directory persistent across app restarts.
- In Docker, `pawconnect-uploads` is mounted as a named volume.
- In a cloud deployment, use persistent mounted storage or replace the local storage services with cloud object storage.
- Do not rely on container-local writable layers for uploaded files.

Known limitation:

- Local file storage is acceptable for demos and simple deployments, but cloud object storage is a better production follow-up.

## 9. Quartz Scheduled Jobs

Quartz jobs are configured through:

- `ScheduledReports`
- `VisitReminders`

Production defaults keep scheduled email jobs disabled:

```text
ScheduledReports__Enabled=false
VisitReminders__Enabled=false
```

Enable them only after SMTP is configured and tested. The jobs log failures and should not crash the app, but a bad schedule can still create unwanted email volume.

## 10. Logging and Error Handling

Production configuration uses safer logging defaults:

- general log level: `Warning`
- EF database command logging: `Warning`
- ASP.NET Core detailed developer exception pages are not used in Production
- `UseExceptionHandler("/Error")` and `UseHsts()` are active outside Development

Do not log:

- connection strings
- API keys
- SMTP passwords
- password reset tokens
- security stamps
- private notes or private user contact data unless intentionally required and protected

## 11. Health Check

PawConnect exposes:

```text
/health
```

The endpoint returns:

- HTTP 200 with `{ "status": "Healthy" }` when the app can connect to the database
- HTTP 503 when database connectivity fails

The health endpoint does not expose connection strings, server names, exception messages, OpenAI status, SMTP status, or other sensitive details.

## 12. Docker Deployment Notes

Docker Compose is mainly intended for local/staging/demo use.

```bash
docker compose up --build
```

Docker defaults:

- app: `http://localhost:8080`
- SQL Server container
- smtp4dev local inbox: `http://localhost:5001`
- startup migrations enabled
- demo seed data enabled
- OpenAI disabled
- scheduled jobs disabled
- upload volume mounted at `/app/wwwroot/uploads`

For real production, prefer a managed SQL Server and a deployment platform secret store. Do not use the `.env.example` password.

## 13. Verification Checklist

Before deployment:

- `dotnet restore PawConnect.sln`
- `dotnet build PawConnect.sln --configuration Release`
- `dotnet test PawConnect.sln --configuration Release`
- `docker compose config`
- confirm `.env` is ignored
- confirm no real secrets are committed
- confirm production database migrations are applied manually or explicitly enabled
- confirm `SeedData__Enabled` is correct for the environment
- confirm OpenAI fallback works if no API key is configured
- confirm email-disabled behavior is acceptable
- confirm uploaded files persist after restart
- confirm `/health` returns the expected status

## 14. Known Limitations and Follow-ups

- This card does not deploy PawConnect to a live host.
- No cloud-specific infrastructure is committed.
- Local upload storage should be replaced or backed by persistent/cloud storage for serious production use.
- Browser end-to-end tests are still a future improvement.
- Swagger/public REST API documentation is not present because PawConnect currently uses Blazor Server pages and service-layer operations.
- Production email and OpenAI require real provider accounts and secrets configured outside the repository.
