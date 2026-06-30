# PawConnect Live Demo Plan

This document prepares the live demo part of the PawConnect bachelor thesis presentation. The full presentation has a maximum of 15 minutes, so the demo should focus on the most meaningful flows rather than showing every feature.

The recommended story is:

Public visitor discovers a dog -> checks dog details and shelter location -> adopter uses AI-assisted search/recommendations -> adoption request status is shown -> shelter reviews adoption requests -> admin supervises the platform.

## A. Must-Show Features

| Feature | Role | Page / Route | Demo data | Duration | Why it matters |
|---|---|---|---|---:|---|
| Public dog discovery | Public | `/dogs` | Show `Mira`, `Lili`, `Nala`, `Oscar` | 1 min | Shows the public adoption-facing side: real dogs, status chips, filters, images, and public-safe visibility. |
| Dog details with gallery and breed info | Public | `/dogs/{id}` via `Mira` card | `Mira`, Bichon, Hope Tails Rescue | 2 min | Shows the richer profile: image/gallery, breed info, behavior, medical status, food, and shelter context. This is stronger than generic CRUD. |
| Shelter location map | Public | `/shelters`, `/shelters/{id}` | `Hope Tails Rescue` or `Happy Paws Shelter` | 1 min | Demonstrates Leaflet/OpenStreetMap and real-world shelter discovery. Good visual thesis feature. |
| Adoption Copilot | Adopter | `/adopter/copilot` | `adopter@mail.com` | 3 min | Central thesis feature. Shows natural-language search, intent chips, public-safe results, fallback/OpenAI-safe behavior. |
| Recommended Dogs | Adopter | `/adopter/recommendations` | Ana's adopter profile | 1 min | Shows personalized matching based on adopter profile, favorites/recent views, rule-based scoring, and optional OpenAI enhancement. |
| Adopter request tracking | Adopter | `/my-adoption-requests` | Bella or Sasha confirmed visit requests | 1 min | Shows the adoption lifecycle from adopter perspective with polished cards and status/visit chips. |
| Shelter adoption request review | Shelter | `/shelter/adoption-requests` | `shelter@mail.com`, Bella/Max requests | 2 min | Shows the operational side: shelter can review adopter data, manage requests, confirm visits, and track status. |

## B. Nice-To-Show Features

| Feature | Role | Page / Route | Duration | When to show it |
|---|---|---|---:|---|
| Visit confirmation | Shelter | `/shelter/adoption-requests` | 1 min | Show only if using a fresh database and `Max` is still pending. Otherwise show the existing confirmed Bella request. |
| Notifications | Any logged-in role | bell in layout, `/notifications` | 30 sec | Show if a notification is already present after request/report actions. Do not spend time creating one live. |
| Admin adoption requests | Admin | `/admin/adoption-requests` | 1 min | Good if you want to show platform-wide supervision and details dialog/export buttons. |
| Admin dashboard | Admin | `/admin/dashboard` | 45 sec | Good closing view: platform overview after showing public/adopter/shelter flows. |
| Resource stock | Shelter | `/shelter/resources` | 1 min | Show only if there is time. It demonstrates shelter operations and low-stock logic. |
| Reports / CSV / PDF exports | Admin/Shelter | `/admin/adoption-requests`, `/admin/report-history`, shelter pages | 1 min | Safer to mention or show buttons/report history, not generate many downloads live. |

## C. Mention-Only Features

- Email + calendar invite: Mention that visit confirmation emails can include `.ics` calendar attachments, but do not rely on SMTP during the live demo.
- PDF reports: Mention QuestPDF reports for adoption/status/resources/shelter summaries; show report history only if needed.
- CSV import/export: Useful operational feature, but live import validation takes too long.
- Audit/activity logs: Mention admin traceability through `/admin/activity-log`.
- Background jobs: Mention Quartz visit reminders and shelter summary reports; do not demo live.
- Shelter registration approval: Important admin workflow, but slower and less central than the dog adoption flow.
- Dog management/create/edit: Mention shelter-side management, but avoid live editing unless asked.

## D. Skip During Demo

- Register/login/create account: necessary infrastructure, but not thesis value.
- Creating a new dog from scratch: too much form time.
- CSV import live: fragile and distracts from the adoption story.
- Running background jobs live: timing-dependent.
- Password/account management pages: not relevant.
- Full admin user management: generic admin CRUD.

## E. Recommended Final Demo Shortlist

Best 5-7 feature story:

1. Open `/dogs`: public visitor browses real available/reserved dogs.
2. Open `Mira` details: show image, breed information, behavior, medical/food/shelter info.
3. Open `Hope Tails Rescue` or `/shelters`: show map/location.
4. Log in as adopter and open `/adopter/copilot`.
5. Run Copilot prompt: `I have a sick dog recovering at home`.
6. Open `/adopter/recommendations` or `/my-adoption-requests`.
7. Log in as shelter and open `/shelter/adoption-requests` to show request review/status workflow.

This tells a coherent story without becoming a CRUD tour.

## F. Stable Demo Data To Prepare

### Accounts

| Role | Email | Password | Use |
|---|---|---|---|
| Adopter | `adopter@mail.com` | `Adopter1!` | Copilot, recommendations, request tracking |
| Shelter | `shelter@mail.com` | `Shelter1!` | Shelter dashboard, adoption requests, resources |
| Admin | `admin@mail.com` | `Admin1!` | Admin dashboard, adoption requests, report/activity pages |

### Best Public Dog

Use `Mira`.

Why:

- Available.
- Has a seeded image.
- Bichon.
- Has calm/cat/calm-dog signals.
- Has medical record data.
- Works well for the dog details page.

### Best Dog Details Page

Open `Mira` from `/dogs`.

Show:

- Main image/gallery.
- Breed Information.
- Behavior description.
- Medical status.
- Food information.
- Shelter context.

### Best Shelter / Map

Use `Hope Tails Rescue` for Mira's shelter, or `Happy Paws Shelter` if you want the public shelter view to match the shelter account used later.

### Best Copilot Prompt

Primary prompt:

```text
I have a sick dog recovering at home
```

Expected behavior:

- Chips like `Compatibility: Sensitive dog`, `Lifestyle: Calm`, `Status: Available, Reserved`.
- Results should prefer dogs with calm dog-company, respectful, gentle, or slow-introduction evidence.
- Likely good candidates: `Mira`, `Lili`, `Toby`, `Iris`, `Hazel`, depending current database state.

Backup prompt:

```text
I live in an apartment and want a dog that does not need too much activity.
```

Expected behavior:

- Likely candidates: `Lili`, `Mira`, `Poppy`, `Hazel`, maybe `Bella`.
- Tags like `Short walks`, `Quiet routine`, `Indoor rest`, `Settles quickly`.

### Best Adoption Request To Show As Adopter

Use Ana's `Bella` request.

Expected status:

- Request status: `VisitConfirmed`.
- Visit status: `Confirmed`.
- Dog status: `Reserved`.

### Best Shelter Request To Show

With `shelter@mail.com`, show:

- `Bella`: already visit confirmed.
- `Max`: pending request if database is fresh.

### Best Admin Page

Use `/admin/adoption-requests` or `/admin/dashboard`.

## G. Risks And Backup Options

| Risk | What can happen | Practical backup |
|---|---|---|
| OpenAI not configured | Copilot uses fallback instead of OpenAI | This is fine. Say: "OpenAI is optional; backend fallback still works." Use the apartment prompt if needed. |
| Embeddings missing/stale | Semantic search may not be used | Copilot still falls back to keyword/rule-based search. Do not rebuild embeddings live. |
| Database not fresh | Requests already processed, old dog images/breeds appear | Before demo, recreate/update the `PawConnect` LocalDB and let seed run. Keep screenshots as backup. |
| Images fail remotely | Some external dog photos may show fallback placeholder | Use dogs with known working images, especially `Mira`, `Nala`, `Oscar`, `Lili`. If one fails, move on. |
| Email server not running | Visit confirmation email/calendar invite not visible | Mention email/ICS behavior from code; do not demo SMTP unless already prepared. |
| Role/session confusion | Wrong user cannot access route | Use separate browser profiles/incognito windows or log out between role switches. |
| Request already confirmed | Cannot show confirm action again | Show the details/status instead, or reset DB before presentation. |
| PDF/CSV download blocked/slow | Browser download distracts | Show export buttons/report history, explain generated files, skip actual download. |
| Background jobs timing | Quartz jobs may not run during demo window | Mention them only. They are not good live-demo material. |
| Shelter map internet issue | Leaflet/OpenStreetMap tiles may not load | Still show stored coordinates/address; keep a screenshot backup. |

## H. Suggested 15-Minute Timing

| Segment | Time | What to show |
|---|---:|---|
| Public discovery | 2 min | `/dogs`, open `Mira`, show details and breed info |
| Shelter location | 1 min | Shelter map for `Hope Tails Rescue` or `Happy Paws Shelter` |
| AI/adopter matching | 4 min | `/adopter/copilot`, run one prompt, briefly show `/adopter/recommendations` |
| Adoption workflow | 3 min | `/my-adoption-requests`, then shelter `/shelter/adoption-requests` |
| Admin supervision | 1 min | `/admin/dashboard` or `/admin/adoption-requests` |
| Mention-only wrap-up | 2 min | Email/calendar, reports, CSV, audit logs, background jobs |
| Buffer | 2 min | Login switching, slow loading, questions |

## I. One-Sentence Thesis Framing

PawConnect is not just a CRUD adoption website: it combines public dog discovery, shelter operations, adoption workflow management, role-based security, maps, notifications, reports, and an AI-assisted Adoption Copilot that helps adopters find suitable dogs while keeping backend validation and public-safe filtering as the source of truth.
