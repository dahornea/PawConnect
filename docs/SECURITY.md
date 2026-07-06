# PawConnect Security Notes

This document summarizes the practical security model used by PawConnect. It is intended for reviewers, interviewers, and developers who want to understand how the application protects role-specific workflows and local/demo configuration.

PawConnect is not claiming formal compliance certification. The goal is a sensible security baseline for a portfolio application that can run locally, in Docker, and later in a production-like ASP.NET Core environment.

## 1. Roles and Access Model

PawConnect uses ASP.NET Core Identity with role-based authorization.

| Role | Typical access |
| ---- | -------------- |
| Public visitor | Public dog discovery, shelter pages, success stories, approved lost/found posts, shelter application form. |
| Adopter | Adopter dashboard, profile, favorites, saved searches, Adoption Copilot, recommendations, own adoption requests, own conversations, own notifications. |
| Shelter | Own shelter dashboard, own dogs, own adoption requests, own resources, own transfers, own volunteer tasks, shelter analytics, shelter conversations. |
| Volunteer | Volunteer task board, tasks assigned to the volunteer, available tasks, own notifications. |
| Admin | Platform-wide dashboards, users, shelters, dogs, adoption requests, reports, audit/activity logs, notification outbox, search index, moderation pages. |

Authorization is applied at multiple layers:

- Razor pages/components use `@attribute [Authorize(...)]` for role-specific routes.
- API controllers use `[Authorize]` and role restrictions.
- Services enforce ownership and object-level checks for data that belongs to a specific user or shelter.
- UI hiding is not treated as the only security boundary.

## 2. Object-Level Authorization Principles

PawConnect protects private resources by checking ownership in service methods and controller flows.

Important examples:

- Adopters should only access their own adoption requests, saved searches, favorites, conversations, and notifications.
- Shelters should only manage dogs, resources, requests, transfers, availability, and volunteer tasks belonging to their shelter.
- Admins can access operational data across the platform where admin pages/API endpoints explicitly allow it.
- Public dog and shelter API responses use DTOs and public-safe filtering rather than exposing EF entities directly.
- Message attachments are downloaded through an authorized endpoint instead of direct public file paths.

## 3. API and Swagger Security

Swagger/OpenAPI is configured in `Program.cs` and is enabled only in `Development` and `Docker` environments.

Local URLs:

```text
http://localhost:8080/swagger
http://localhost:8080/swagger/v1/swagger.json
```

API protections:

- Protected API endpoints return `401` for unauthenticated API calls instead of redirecting to the login page.
- Wrong-role API access returns `403`.
- Protected Swagger operations document the PawConnect Identity cookie requirement.
- Public endpoints such as `/api/v1/dogs` and `/api/v1/shelters` expose only public-safe DTOs.
- Admin endpoints are restricted to the Admin role.

## 4. Security Headers

PawConnect adds a small set of safe response headers in `Program.cs`:

| Header | Purpose |
| ------ | ------- |
| `X-Content-Type-Options: nosniff` | Reduces content-type sniffing risk. |
| `X-Frame-Options: SAMEORIGIN` | Prevents most clickjacking while still allowing same-origin framing if ever needed. |
| `Referrer-Policy: strict-origin-when-cross-origin` | Avoids leaking full paths to other origins. |
| `Permissions-Policy` | Disables camera, microphone, payment, and USB access; allows geolocation only from the app origin for map/location features. |

A strict Content Security Policy was not added in this pass because Blazor Server, MudBlazor, Swagger UI, SignalR, Leaflet, and existing scripts/styles should be tested carefully before enforcing CSP. CSP is a good future hardening item.

## 5. Cookie and Session Settings

The Identity application cookie is configured to be:

- `HttpOnly`
- `SameSite=Lax`
- secure outside local Development/Docker HTTP scenarios
- sliding with an 8-hour expiration window

The secure cookie policy remains compatible with local HTTP Docker/demo runs while being stricter outside local environments.

## 6. Anti-Forgery / CSRF

PawConnect uses ASP.NET Core anti-forgery middleware through `app.UseAntiforgery()` and Identity UI components include anti-forgery tokens for state-changing account forms.

API endpoints are primarily intended for same-origin local/API usage with Identity cookie authentication. If PawConnect later exposes a separate browser or mobile client, CSRF and token-based API authentication should be reviewed again.

## 7. Rate Limiting

PawConnect uses built-in ASP.NET Core in-memory rate limiting for local/API abuse protection.

Current policies:

| Policy | Applied to | Limit |
| ------ | ---------- | ----- |
| `ApiFixedWindow` | MVC API controllers mapped through `MapControllers()` | 120 requests per minute per authenticated user or source IP. |
| `AuthenticatedDownload` | Authorized message attachment download endpoint | 60 requests per minute per authenticated user or source IP. |

This is intentionally simple and zero-cost. It does not require Redis or external infrastructure.

## 8. Secrets and Configuration Hygiene

Repository rules:

- Do not commit `.env`, real connection strings, API keys, SMTP credentials, tokens, certificates, or production passwords.
- `.env.example` contains local-demo placeholders only.
- OpenAI is optional and disabled by default in Docker and Production settings.
- SMTP is local/demo-friendly through smtp4dev and should use environment variables or platform secrets for real providers.
- Demo seed credentials are local-only and must not be used for production.

Important local demo credentials are documented because they are intentionally seeded for local evaluation.

## 9. File Upload Safety

Where upload features exist, PawConnect uses validation and local storage abstractions. The intended safety rules are:

- validate allowed extensions/content types
- enforce maximum file size
- generate safe stored filenames
- avoid trusting original filenames
- store relative paths/keys rather than absolute server paths
- use authorized endpoints for private attachments

Production deployments should add malware scanning or move private files to a hardened object storage service if the app handles real user uploads.

## 10. Background Jobs and Outbox Safety

PawConnect has notification outbox/background processing and scheduled jobs. Security expectations:

- manual outbox/admin actions stay Admin-only
- email/report failures should not expose secrets in UI errors
- logs should avoid passwords, tokens, API keys, SMTP credentials, full private payloads, and sensitive questionnaire answers
- Docker and Production default scheduled jobs are conservative to avoid surprise background email traffic

## 11. Data Privacy Notes

- Public dog discovery excludes non-public statuses such as adopted or in-treatment where appropriate.
- Copilot and recommendation flows use backend-provided public-safe dog data, not direct database access from OpenAI.
- Private adopter data, shelter internal notes, audit logs, and operational data should not appear in public DTOs.
- Admin data remains behind Admin-only pages/API endpoints.

## 12. Known Limitations and Future Hardening

Useful future improvements:

- Add a carefully tested Content Security Policy.
- Add dedicated API authentication if PawConnect becomes a separate web/mobile API product.
- Add automated authorization integration tests for every high-risk API controller.
- Add security event dashboarding for failed authorization and suspicious actions.
- Add malware scanning or external hardened storage for production file uploads.
- Add stricter production CORS policy if external clients are introduced.
- Add real secret scanning in CI if the repository becomes public/team-maintained.
