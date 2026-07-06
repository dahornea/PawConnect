# PawConnect Performance Notes

This document summarizes the practical performance and caching choices used in PawConnect. The goal is to keep the application responsive for local, Docker, and portfolio/demo usage without adding paid infrastructure such as Redis, cloud cache services, or external monitoring tools.

## What Was Optimized

### Public dog API paging

The public dog list API now uses service-level pagination through `DogService.SearchDogsPagedAsync`. This keeps the existing filtering behavior but applies `CountAsync`, `Skip`, and `Take` in the database query path before returning API DTOs.

Relevant files:

- `Services/DogService.cs`
- `Services/IDogService.cs`
- `Services/PagedResult.cs`
- `Controllers/Api/V1/DogsController.cs`

Why it matters:

- `/api/v1/dogs` no longer has to materialize the full matching dog list before applying `page` and `pageSize`.
- Public-safe dog filtering remains in the service layer.
- API responses still include `TotalCount`, `TotalPages`, `Page`, and `PageSize`.

### Cached lookup/reference data

Stable lookup lists are cached locally with `IMemoryCache`:

- active dog breeds
- resource categories
- food types

Relevant files:

- `Services/Caching/ILocalCacheService.cs`
- `Services/Caching/LocalCacheService.cs`
- `Services/Caching/CacheKeys.cs`
- `Services/DogBreedService.cs`
- `Services/ResourceCategoryService.cs`
- `Services/FoodTypeService.cs`

Why it matters:

- These lists are requested by multiple create/edit forms and dashboards.
- They rarely change during normal app usage.
- Caching avoids repeated identical database reads.

### Short-lived analytics dashboard cache

Admin and shelter analytics dashboards are cached briefly after authorization and ownership checks.

Relevant files:

- `Services/AnalyticsService.cs`
- `Services/Caching/CacheKeys.cs`

Why it matters:

- Analytics pages perform several grouped read queries.
- Short-lived caching avoids recomputing the same dashboard when users refresh or navigate back quickly.
- Role checks still run before cached data is returned.

## Cache TTLs

| Data | TTL | Reason |
| --- | ---: | --- |
| Dog breeds | 30 minutes | Reference data changes rarely. |
| Resource categories | 30 minutes | Reference data changes rarely; service invalidates after changes. |
| Food types | 30 minutes | Reference data changes rarely; service invalidates after changes. |
| Admin/shelter analytics dashboards | 45 seconds | Good enough for dashboard responsiveness while keeping data fresh. |
| Admin shelter filter options | 10 minutes | Shelter list changes are uncommon in normal usage. |

## Cache Key Strategy

Cache keys are centralized in `Services/Caching/CacheKeys.cs`.

Rules:

- user/shelter scoped data includes the relevant scope in the key
- analytics keys include date range and shelter/platform scope
- lookup keys use explicit `lookup:*` prefixes
- cache prefixes can be removed through `ILocalCacheService.RemoveByPrefix`

This avoids accidentally sharing cached data between different users, shelters, or roles.

## Invalidation Strategy

The current invalidation approach is intentionally simple:

- resource categories and food types remove their lookup cache after create/update/delete
- analytics dashboards use short TTLs instead of complex invalidation
- dog API search results are not cached, because dogs and adoption statuses should remain fresh

If dashboard freshness becomes more important later, the next step would be explicit analytics cache invalidation after adoption request, resource, transfer, volunteer, or dog status changes.

## What Is Not Cached

The following data should not be globally cached:

- adoption decisions and status transitions
- private notifications or message payloads
- authorization decisions
- user-specific saved search details unless the user ID is part of the key
- files and private attachments
- data that must be immediately consistent after a write

Correctness and privacy are more important than cache hit rate.

## EF Core Query Notes

The existing codebase already uses `AsNoTracking()` in many read-only queries. This card keeps that pattern and adds a paginated dog search path for the public API.

Useful query patterns in the project:

- `AsNoTracking()` for read-only screens and API responses
- projections for analytics and API DTO preparation
- grouped database queries for analytics summaries
- role/shelter/adopter filtering in service methods
- existing indexes for notification outbox, saved searches, audit logs, volunteer tasks, transfers, and messaging

No new database migration was added in this pass. The existing schema already contains many of the high-value indexes for the current query patterns, and the most visible issue found was API paging rather than a missing index.

## Troubleshooting Stale Data

If a page appears stale:

1. Refresh the page after 45 seconds for analytics dashboards.
2. Confirm whether the data is lookup/reference data or active workflow data.
3. For resource categories or food types, make sure updates go through the corresponding service so cache invalidation runs.
4. Restarting the app clears all local in-memory cache.

Because PawConnect uses in-process caching, cache contents are not shared between multiple app instances. This is acceptable for the current local/Docker/portfolio deployment model.

## Manual Verification

Suggested checks:

1. Open `/api/v1/dogs?page=1&pageSize=3` in Swagger or the browser and confirm only three items are returned with the correct total count.
2. Change to `page=2` and confirm the next page is returned.
3. Open the public Dogs page and confirm filtering still works.
4. Log in as shelter and open the shelter analytics dashboard twice; the second load should avoid recomputing the same dashboard within the short TTL.
5. Log in as admin and verify admin analytics still respects shelter filters.
6. Create/update a resource category or food type through the app and confirm lookup lists refresh.

## Known Limitations

- There are no production benchmark numbers in this document; the changes are practical code-path optimizations, not a formal load test.
- In-memory cache is per application instance. A future multi-instance deployment would need distributed cache or explicit event-based invalidation.
- Analytics cache invalidation is TTL-based, not event-driven.
- Some Blazor pages still load full lists because the current demo dataset is small. Future large-data usage should add more server-side paging to admin/shelter tables.

## Future Ideas

- Add server-side paging to admin dog, adoption application, audit, notification, volunteer, transfer, and foster tables if datasets grow.
- Add debounce to live filter inputs that currently trigger service calls often.
- Add slow-operation log warnings around dashboard and search service methods.
- Add lightweight timing metrics to existing observability screens.
- Consider response compression for production if payload sizes become large.
- Add targeted SQL indexes only after query plans or measured slow queries justify them.
