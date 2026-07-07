# Global Command Palette

The global command palette adds a fast `Ctrl+K` / `Cmd+K` workflow to PawConnect. It helps users jump to pages, discover role-specific workflows, search a few scoped entities, and reuse recently opened commands without adding any paid or external search service.

## How It Opens

- `Ctrl+K` on Windows/Linux.
- `Cmd+K` on macOS.
- `/` when focus is not inside an input, select, textarea, or editable element.
- The top app bar also shows a visible **Search or jump to...** button.

The palette supports:

- `Escape` to close.
- `Arrow Up` / `Arrow Down` to move between results.
- `Enter` to open the selected command.
- Click outside the panel to close it.

## Files

| File | Purpose |
| ---- | ------- |
| `Components/CommandPalette/CommandPaletteHost.razor` | UI host, search input, grouped result rendering, keyboard handling, and navigation execution. |
| `Components/CommandPalette/CommandPaletteHost.razor.css` | Modal, trigger button, result list, mobile, and focus styling. |
| `Components/CommandPalette/CommandPaletteHost.razor.js` | Global keyboard shortcut registration and localStorage recent-command storage. |
| `Services/CommandPalette/ICommandPaletteService.cs` | Service abstraction used by the UI. |
| `Services/CommandPalette/CommandPaletteService.cs` | Builds role-aware commands and scoped entity search results. |
| `Services/CommandPalette/CommandPaletteCommand.cs` | Command metadata model. |
| `Services/CommandPalette/CommandPaletteSearchRequest.cs` | Search request model containing user, query, current path, and cancellation token. |

## Command Categories

The current implementation groups commands into:

- Navigation
- Dogs
- Applications
- Shelter Operations
- Notifications
- Admin
- Quick Actions
- Volunteer
- Recent

Only categories with available results are shown.

## Role-Based Behavior

Commands are created according to the current authenticated user's role:

- Public users see public-safe navigation such as Home, Dogs, Shelters, Lost & Found, Success Stories, Login, Register, and Shelter Application.
- Adopters see adopter dashboard, profile, recommendations, Adoption Copilot, favorites, saved searches, adoption requests, notifications, and public dog search.
- Shelters see shelter dashboard, dog management, add dog, adoption request review, adoption pipeline, availability, resources, transfers, shelter search, volunteer tasks, notifications, and shelter-scoped dog/application/task search.
- Volunteers see their volunteer task page and task search scoped to their assigned/open tasks.
- Admins see admin dashboard, analytics, users, shelters, dogs, adoption requests, transfers, volunteer oversight, admin search, Copilot evaluation, search index, message reports, Lost & Found moderation, report history, notification delivery/outbox, audit logs, Swagger, and health check.

The palette hides commands the user should not see, but this is only a usability layer. Existing page, service, and endpoint authorization remains the real security boundary.

## Entity Search

When the user types a query, the service performs limited database-backed search using `AsNoTracking()` and projections.

Supported searches:

- Dogs:
  - Public/adopter users see only `Available` and `Reserved` dogs.
  - Shelter users search dogs owned by their shelter.
  - Admin users can search all dogs.
- Notifications:
  - Users search only their own notifications.
- Adopter records:
  - Adopters can search their own adoption requests and saved dog searches.
- Shelter/Admin operational records:
  - Shelters can search adoption requests and volunteer tasks scoped to their shelter.
  - Admins can search platform adoption requests, volunteer tasks, and failed/dead-letter notification outbox records.
- Volunteer records:
  - Volunteers can search assigned tasks and open tasks.

## Contextual Commands

Some commands are added based on the current route:

- On `/dogs/{id}`:
  - Reopen Current Dog.
  - Start Adoption Request for adopters.
  - Edit Current Dog for shelter users.
  - Open Admin Dog List for admins.
- On `/admin/notification-outbox`:
  - Show Failed Notifications.
- On `/shelter/dogs`:
  - Add Dog From Here.

These commands navigate to existing workflows instead of mutating data directly.

## Recent Commands

Recently used commands are stored in browser `localStorage` under:

```text
pawconnect.commandPalette.recent
```

Only safe command metadata is stored:

- ID
- title
- description
- category
- route
- icon
- badge

The implementation does not store private form data, secrets, or command payloads. The recent list is limited to eight entries.

## Adding a New Command

1. Add the route to `CommandPaletteService.BuildNavigationCommands` or a relevant search/context method.
2. Choose a stable command ID.
3. Add a concise title and description.
4. Put it in an existing category or introduce a category only if it helps users.
5. Add keywords/aliases users are likely to type.
6. Scope it by role before returning it.
7. Prefer navigation to an existing page over direct mutations.
8. Add or update a test if the command has role-sensitive behavior.

## Security Notes

- The palette must not show commands for unfinished or unauthorized pages.
- Entity searches are scoped by role and user ID.
- Shelter searches are scoped to the shelter account where applicable.
- Adopter searches are scoped to the current adopter.
- Sensitive operations are represented as navigation commands, not direct destructive actions.

## Performance Notes

- Static navigation commands are generated in memory.
- Database entity search only runs when the query is non-empty.
- The UI debounces input before calling the service.
- Queries use `AsNoTracking()`, `Take(limit)`, and projection to avoid loading large object graphs.
- The component does not run searches during normal page rendering unless the palette is opened.
