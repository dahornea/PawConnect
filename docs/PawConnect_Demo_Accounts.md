# PawConnect Demo Accounts

Use these three accounts for the bachelor thesis live demo.

| Role | Email | Password | Use during presentation |
|---|---|---|---|
| Admin | `admin@mail.com` | `Admin1!` | Platform supervision: admin dashboard, all dogs, adoption requests, reports, and activity logs. |
| Adopter | `adopter@mail.com` | `Adopter1!` | Adopter dashboard, profile, recommendations, Adoption Copilot, favorites, and My Adoption Requests. |
| Shelter representative | `shelter@mail.com` | `Shelter1!` | Happy Paws Shelter workspace: dog management, adoption request review, resources, and shelter-side exports. |

## Seeded Demo Profile

The adopter account is seeded as `Ana Ionescu` in Cluj-Napoca. The profile is intentionally aligned with the Copilot demo scenario:

- Housing: apartment
- Has yard: no
- Has children: no
- Has other pets: yes
- Other-pet context: one older/recovering dog at home
- Dog experience: moderate experience with family dogs and recovery care
- Preferred fit: small or medium calm companion, low-to-moderate activity
- Demo need: a dog that will not overwhelm the recovering resident dog

## Shelter Profile

The shelter account is linked to `Happy Paws Shelter`.

- Public contact email: `shelter@mail.com`
- Phone: `0722345678`
- City: Cluj-Napoca
- Neighborhood: Zorilor
- Address: `Strada Observatorului 12`
- Coordinates: valid Cluj-Napoca map coordinates
- Visit window: Monday-Saturday, 10:00-18:00 in the current data model

## Best Demo Data

| Demo item | Recommended data |
|---|---|
| Public dog details page | `Mira` is a good public profile for images, breed information, behavior, medical status, food, and shelter context. |
| Adopter request tracking | `Bella` has a visit-confirmed request for `adopter@mail.com`. |
| Shelter request review | `Bella` is already visit-confirmed, and `Max` is a pending request if the database is freshly seeded. |
| Shelter account | `shelter@mail.com`, linked to Happy Paws Shelter. |
| Admin page | `/admin/dashboard` or `/admin/adoption-requests`. |

## Recommended Demo Flow

1. Open `/dogs` as a public visitor and show public dog browsing.
2. Open `Mira` and show dog details, image gallery, breed information, behavior, medical/food information, and shelter context.
3. Log in as `adopter@mail.com`.
4. Open `/adopter/copilot` and use:
   - `I have a sick dog recovering at home`
5. Briefly show `/adopter/recommendations`.
6. Open `/my-adoption-requests` and show the `Bella` request.
7. Log in as `shelter@mail.com`.
8. Open `/shelter/adoption-requests` and show the same workflow from the shelter side.
9. If time remains, log in as `admin@mail.com` and show `/admin/dashboard` or `/admin/adoption-requests`.

## Copilot Prompts

Primary prompt:

```text
I have a sick dog recovering at home
```

Expected behavior:

- Prefer calmer dogs.
- Prefer respectful or gentle dog-to-dog behavior.
- Show sensitive-dog/calm-lifestyle chips and caution tags when needed.
- Work even when OpenAI is disabled, using the fallback matching flow.

Backup prompt:

```text
I live in an apartment and want a dog that does not need too much activity.
```

Expected behavior:

- Prefer small or medium dogs.
- Prefer short walks, indoor rest, quiet routine, and settles-quickly evidence.
- Avoid over-promoting high-energy dogs.

