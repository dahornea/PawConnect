# PawConnect React Adopter Portal

This client is an API-first React interface for PawConnect's adopter workflows. It shares the existing ASP.NET Core backend, SQL Server data, Identity cookie authentication, service-layer rules, and public-safety checks with the Blazor application.

## Implemented scope

- Public landing page and dog discovery
- Server-side dog filters, sorting, and pagination
- Dog details, natural image gallery, and lightbox
- Adopter login using the existing ASP.NET Core Identity cookie
- Favorite dogs
- Saved searches, alerts, evaluation, and match preview
- Adoption application submission, tracking, details, and cancellation
- Notification Center and delivery preferences
- Adopter profile
- Explainable adopter insights
- Adoption Copilot results grounded in public PawConnect dog records

PawConnect does not currently expose Card 84's dynamic questionnaire or Card 103's journey model on this branch. The React portal therefore uses the real existing adoption-request questionnaire and does not simulate those features.

## Prerequisites

- Node.js 22 or newer
- npm
- .NET SDK 10.0.301
- The PawConnect backend and its configured database

## Local development

From the repository root, start the backend:

```powershell
dotnet run --project PawConnect.csproj --launch-profile http
```

In another terminal:

```powershell
cd clients/pawconnect-react
npm ci
npm run dev
```

Open `http://localhost:5173`. Vite proxies API, image, upload, Swagger, and health-check requests to `http://localhost:5180` by default. Set `VITE_DEV_API_TARGET` if the backend uses a different origin.

The intentionally seeded adopter account is documented in PawConnect's demo account documentation. On the current seed data it is `adopter@mail.com` with password `Adopter1!`.

## Generated API contract

The committed TypeScript contract at `src/api/generated/schema.d.ts` is generated from the backend OpenAPI document. Start the backend, then run:

```powershell
npm run api:generate
```

To use another document:

```powershell
$env:PAWCONNECT_OPENAPI_URL="http://localhost:5180/swagger/v1/swagger.json"
npm run api:generate
```

## Quality commands

```powershell
npm run typecheck
npm run lint
npm run test:run
npm run build
```

The repository's Playwright project also contains focused portal flows for public filtering/details, adopter login/logout, and rejection of Shelter credentials. With both apps running:

```powershell
$env:PAWCONNECT_RUN_E2E = "1"
$env:PAWCONNECT_E2E_BASE_URL = "http://localhost:5180"
$env:PAWCONNECT_REACT_E2E_BASE_URL = "http://127.0.0.1:5173"
dotnet test ..\..\PawConnect.E2ETests\PawConnect.E2ETests.csproj --filter "FullyQualifiedName~ReactAdopterPortalTests"
```

## Authentication and API safety

- Requests use `credentials: include` and the existing HttpOnly Identity cookie.
- Unsafe requests send an antiforgery token obtained from `/api/v1/auth/antiforgery`.
- The backend converts API redirects to `401`/`403` responses.
- React route guards improve UX, while backend role and ownership checks remain authoritative.
- The API client normalizes safe error messages and does not blindly retry authorization or validation failures.

## Docker

The root Compose file builds this client and exposes it at `http://localhost:5173`. Nginx serves the SPA and forwards `/api`, `/images`, `/uploads`, and `/health` to the ASP.NET Core service.
