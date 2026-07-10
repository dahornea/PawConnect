using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services.CommandPalette;

public sealed class CommandPaletteService(ApplicationDbContext context) : ICommandPaletteService
{
    private const string Navigation = "Navigation";
    private const string Dogs = "Dogs";
    private const string Applications = "Applications";
    private const string ShelterOperations = "Shelter Operations";
    private const string Notifications = "Notifications";
    private const string Admin = "Admin";
    private const string QuickActions = "Quick Actions";
    private const string Volunteer = "Volunteer";
    private const string SavedViews = "Saved Views";

    public async Task<IReadOnlyList<CommandPaletteCommand>> SearchAsync(CommandPaletteSearchRequest request)
    {
        var query = NormalizeQuery(request.Query);
        var user = request.User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAuthenticated = user.Identity?.IsAuthenticated == true;
        var isAdmin = user.IsInRole(IdentitySeedData.AdminRole);
        var isShelter = user.IsInRole(IdentitySeedData.ShelterRole);
        var isAdopter = user.IsInRole(IdentitySeedData.AdopterRole);
        var isVolunteer = user.IsInRole(IdentitySeedData.VolunteerRole);
        var shelterId = isShelter && !string.IsNullOrWhiteSpace(userId)
            ? await GetShelterIdAsync(userId, request.CancellationToken)
            : null;

        var commands = BuildNavigationCommands(isAuthenticated, isAdopter, isShelter, isAdmin, isVolunteer)
            .Concat(BuildContextualCommands(request.CurrentPath, isAdopter, isShelter, isAdmin, shelterId))
            .Where(command => Matches(command, query))
            .ToList();

        if (isAuthenticated && !string.IsNullOrWhiteSpace(userId))
        {
            commands.AddRange(await SearchSavedViewsAsync(
                userId,
                query,
                request.LimitPerGroup,
                isAdmin,
                isShelter,
                isAdopter,
                isVolunteer,
                request.CancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            commands.AddRange(await SearchDogsAsync(query, request.LimitPerGroup, isAdmin, isShelter, isAdopter, shelterId, request.CancellationToken));

            if (isAuthenticated && !string.IsNullOrWhiteSpace(userId))
            {
                commands.AddRange(await SearchNotificationsAsync(userId, query, request.LimitPerGroup, request.CancellationToken));
            }

            if (isAdopter && !string.IsNullOrWhiteSpace(userId))
            {
                commands.AddRange(await SearchAdopterItemsAsync(userId, query, request.LimitPerGroup, request.CancellationToken));
            }

            if ((isShelter || isAdmin) && !string.IsNullOrWhiteSpace(userId))
            {
                commands.AddRange(await SearchOperationalItemsAsync(query, request.LimitPerGroup, isAdmin, shelterId, request.CancellationToken));
            }

            if (isVolunteer && !string.IsNullOrWhiteSpace(userId))
            {
                commands.AddRange(await SearchVolunteerItemsAsync(userId, query, request.LimitPerGroup, request.CancellationToken));
            }
        }

        return commands
            .GroupBy(command => command.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(command => CategoryOrder(command.Category))
            .ThenBy(command => command.Title)
            .Take(Math.Max(12, request.LimitPerGroup * 8))
            .ToList();
    }

    private async Task<int?> GetShelterIdAsync(string shelterUserId, CancellationToken cancellationToken)
    {
        return await context.Shelters
            .AsNoTracking()
            .Where(shelter => shelter.ApplicationUserId == shelterUserId)
            .Select(shelter => (int?)shelter.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IEnumerable<CommandPaletteCommand> BuildNavigationCommands(
        bool isAuthenticated,
        bool isAdopter,
        bool isShelter,
        bool isAdmin,
        bool isVolunteer)
    {
        yield return Command("nav-home", "Home", "Open the PawConnect home page.", Navigation, "/", Icons.Material.Filled.Home, "home", "start");
        yield return Command("nav-dogs", "Browse Dogs", "Search public dogs available or reserved for adoption.", Dogs, "/dogs", Icons.Material.Filled.Pets, "dogs", "browse", "adoption");
        yield return Command("nav-lost-found", "Lost & Found", "Browse approved lost and found dog notices.", Navigation, "/lost-found", Icons.Material.Filled.TravelExplore, "lost", "found", "community");
        yield return Command("nav-shelters", "Shelters", "View shelter profiles and locations.", Navigation, "/shelters", Icons.Material.Filled.Place, "shelters", "map", "location");
        yield return Command("nav-success-stories", "Success Stories", "Open adopted dog success stories.", Navigation, "/success-stories", Icons.Material.Filled.EmojiEvents, "success", "adopted");

        if (!isAuthenticated)
        {
            yield return Command("nav-login", "Log In", "Sign in to an existing PawConnect account.", Navigation, "/Account/Login", Icons.Material.Filled.Login, "login", "sign in");
            yield return Command("nav-register", "Register", "Create an adopter account.", Navigation, "/Account/Register", Icons.Material.Filled.PersonAdd, "register", "account");
            yield return Command("nav-shelter-apply", "Apply as a Shelter", "Submit a shelter registration request.", Navigation, "/shelters/apply", Icons.Material.Filled.Assignment, "shelter", "apply");
        }

        if (isAuthenticated)
        {
            yield return Command("nav-notifications", "Notification Center", "Open your notifications.", Notifications, "/notifications", Icons.Material.Filled.Notifications, "notifications", "alerts");
            yield return Command("nav-notification-preferences", "Notification Preferences", "Manage notification delivery settings.", Notifications, "/notification-preferences", Icons.Material.Filled.Tune, "preferences", "notification settings");
        }

        if (isAdopter)
        {
            yield return Command("adopter-dashboard", "Adopter Dashboard", "Open your adopter overview.", Navigation, "/adopter/dashboard", Icons.Material.Filled.Dashboard, "adopter", "dashboard");
            yield return Command("adopter-profile", "My Profile", "Edit adopter profile and home information.", Navigation, "/adopter/profile", Icons.Material.Filled.AccountCircle, "profile", "housing");
            yield return Command("adopter-insights", "My Adoption Insights", "Open saved-search matches and adoption next steps.", Applications, "/adopter/insights", Icons.Material.Filled.TipsAndUpdates, "insights", "next steps", "matches");
            yield return Command("adopter-recommendations", "Recommended Dogs", "View recommendation-based dog matches.", Dogs, "/adopter/recommendations", Icons.Material.Filled.AutoAwesome, "recommended", "matches");
            yield return Command("adopter-copilot", "Adoption Copilot", "Search for dogs with natural language.", Dogs, "/adopter/copilot", Icons.Material.Filled.AutoFixHigh, "copilot", "assistant");
            yield return Command("adopter-favorites", "Favorite Dogs", "Open your saved favorite dogs.", Dogs, "/favorites", Icons.Material.Filled.Favorite, "favorites", "saved dogs");
            yield return Command("adopter-saved-searches", "Saved Searches", "Manage dog search alerts and matches.", Dogs, "/adopter/saved-searches", Icons.Material.Filled.Bookmark, "saved searches", "alerts");
            yield return Command("adopter-requests", "My Adoption Requests", "Track submitted adoption requests.", Applications, "/my-adoption-requests", Icons.Material.Filled.Assignment, "applications", "requests");
        }

        if (isShelter)
        {
            yield return Command("shelter-dashboard", "Shelter Dashboard", "Open the shelter operations overview.", ShelterOperations, "/shelter/dashboard", Icons.Material.Filled.Dashboard, "shelter", "dashboard");
            yield return Command("shelter-intelligence", "Operations Intelligence", "Open explainable shelter priorities and recommended actions.", ShelterOperations, "/shelter/intelligence", Icons.Material.Filled.TipsAndUpdates, "intelligence", "priorities", "risk");
            yield return Command("shelter-simulator", "Scenario Simulator", "Model capacity, workload, and operational assumptions without changing live data.", ShelterOperations, "/shelter/simulator", Icons.Material.Filled.ModelTraining, "simulation", "what if", "capacity");
            yield return Command("shelter-simulator-intake", "Simulate Intake Surge", "Open the simulator with the intake surge template.", QuickActions, "/shelter/simulator?template=intake-surge", Icons.Material.Filled.AddHomeWork, "simulate", "intake", "capacity");
            yield return Command("shelter-analytics", "Shelter Analytics", "View shelter performance and adoption metrics.", ShelterOperations, "/shelter/analytics", Icons.Material.Filled.Analytics, "analytics", "metrics");
            yield return Command("shelter-assistant", "Shelter Assistant", "Open the AI-assisted shelter operations assistant.", ShelterOperations, "/shelter/assistant", Icons.Material.Filled.SupportAgent, "assistant", "operations");
            yield return Command("shelter-dogs", "Manage Dogs", "Open shelter dog management.", Dogs, "/shelter/dogs", Icons.Material.Filled.Pets, "manage dogs", "profiles");
            yield return Command("shelter-add-dog", "Add New Dog", "Create a new dog profile.", QuickActions, "/shelter/dogs/create", Icons.Material.Filled.AddCircle, "add dog", "create dog");
            yield return Command("shelter-requests", "Review Adoption Applications", "Review adoption requests for your shelter dogs.", Applications, "/shelter/adoption-requests", Icons.Material.Filled.AssignmentTurnedIn, "applications", "review");
            yield return Command("shelter-pipeline", "Adoption Pipeline", "Open the adoption pipeline board.", Applications, "/shelter/adoption-pipeline", Icons.Material.Filled.ViewKanban, "pipeline", "kanban");
            yield return Command("shelter-availability", "Visit Availability", "Manage shelter visit slots.", ShelterOperations, "/shelter/availability", Icons.Material.Filled.EventAvailable, "availability", "visits");
            yield return Command("shelter-resources", "Resource Stock", "Manage shelter resources and low-stock items.", ShelterOperations, "/shelter/resources", Icons.Material.Filled.Inventory2, "resources", "stock");
            yield return Command("shelter-transfers", "Dog Transfers", "Manage dog transfer requests.", ShelterOperations, "/shelter/transfers", Icons.Material.Filled.SwapHoriz, "transfers");
            yield return Command("shelter-search", "Shelter Search", "Search shelter operations records.", ShelterOperations, "/shelter/search", Icons.Material.Filled.Search, "search");
            yield return Command("shelter-volunteer-tasks", "Volunteer Tasks", "Create and manage shelter volunteer tasks.", Volunteer, "/shelter/volunteer-tasks", Icons.Material.Filled.TaskAlt, "volunteer", "tasks");
        }

        if (isVolunteer)
        {
            yield return Command("volunteer-tasks", "My Volunteer Tasks", "View assigned and available volunteer tasks.", Volunteer, "/volunteer/tasks", Icons.Material.Filled.TaskAlt, "volunteer", "tasks");
        }

        if (isAdmin)
        {
            yield return Command("admin-dashboard", "Admin Dashboard", "Open the platform admin overview.", Admin, "/admin/dashboard", Icons.Material.Filled.AdminPanelSettings, "admin", "dashboard");
            yield return Command("admin-intelligence", "Platform Intelligence", "Review platform risks, shelter workload, and notification reliability.", Admin, "/admin/intelligence", Icons.Material.Filled.TipsAndUpdates, "intelligence", "platform risk", "workload");
            yield return Command("admin-simulator", "Scenario Simulator", "Compare shelter and platform what-if scenarios.", Admin, "/admin/simulator", Icons.Material.Filled.ModelTraining, "simulation", "what if", "capacity");
            yield return Command("admin-simulator-volunteers", "Simulate Volunteer Shortage", "Open the platform simulator with a volunteer shortage template.", QuickActions, "/admin/simulator?template=volunteer-shortage", Icons.Material.Filled.Groups, "simulate", "volunteer", "shortage");
            yield return Command("admin-analytics", "Admin Analytics", "View platform analytics.", Admin, "/admin/analytics", Icons.Material.Filled.Analytics, "analytics", "metrics");
            yield return Command("admin-users", "Manage Users", "Review platform user accounts.", Admin, "/admin/users", Icons.Material.Filled.Group, "users", "identity");
            yield return Command("admin-shelters", "Manage Shelters", "Review and manage shelters.", Admin, "/admin/shelters", Icons.Material.Filled.HomeWork, "shelters");
            yield return Command("admin-shelter-requests", "Shelter Applications", "Review shelter registration requests.", Admin, "/admin/shelter-requests", Icons.Material.Filled.Assignment, "shelter requests");
            yield return Command("admin-dogs", "All Dogs", "Open the admin dog list.", Dogs, "/admin/dogs", Icons.Material.Filled.Pets, "dogs", "admin");
            yield return Command("admin-adoption-requests", "All Adoption Requests", "Review adoption requests across shelters.", Applications, "/admin/adoption-requests", Icons.Material.Filled.AssignmentTurnedIn, "requests");
            yield return Command("admin-transfers", "Transfer Requests", "Review multi-shelter dog transfers.", Admin, "/admin/transfers", Icons.Material.Filled.SwapHoriz, "transfers");
            yield return Command("admin-volunteer-tasks", "Volunteer Task Oversight", "Review volunteer work across shelters.", Volunteer, "/admin/volunteer-tasks", Icons.Material.Filled.Task, "volunteer", "tasks");
            yield return Command("admin-search", "Admin Search", "Search operational records.", Admin, "/admin/search", Icons.Material.Filled.ManageSearch, "search");
            yield return Command("admin-copilot-evaluation", "Copilot Evaluation", "Review Adoption Copilot evaluation runs.", Admin, "/admin/copilot-evaluation", Icons.Material.Filled.Psychology, "copilot", "evaluation");
            yield return Command("admin-search-index", "Search Index", "Inspect semantic search index status.", Admin, "/admin/search-index", Icons.Material.Filled.ManageSearch, "semantic", "embeddings");
            yield return Command("admin-message-reports", "Message Reports", "Moderate reported conversation messages.", Admin, "/admin/message-reports", Icons.Material.Filled.Report, "message reports", "moderation");
            yield return Command("admin-lost-found", "Lost & Found Moderation", "Review lost and found community posts.", Admin, "/admin/lost-found", Icons.Material.Filled.TravelExplore, "lost found");
            yield return Command("admin-report-history", "Report History", "View generated PDF and CSV reports.", Admin, "/admin/report-history", Icons.Material.Filled.Article, "reports");
            yield return Command("admin-notification-delivery", "Notification Delivery Logs", "Review notification delivery attempts.", Notifications, "/admin/notification-delivery", Icons.Material.Filled.MarkEmailRead, "delivery", "notifications");
            yield return Command("admin-notification-outbox", "Notification Outbox", "Open queued and failed notification messages.", Notifications, "/admin/notification-outbox", Icons.Material.Filled.Outbox, "outbox", "failed notifications");
            yield return Command("admin-audit-logs", "Audit Logs", "Inspect audit and observability logs.", Admin, "/admin/audit-logs", Icons.Material.Filled.FactCheck, "audit", "logs");
            yield return Command("admin-swagger", "Swagger API Docs", "Open local REST API documentation.", Admin, "/swagger", Icons.Material.Filled.Api, "swagger", "api", "openapi");
            yield return Command("admin-health", "Health Check", "Open the application health endpoint.", Admin, "/health", Icons.Material.Filled.MonitorHeart, "health");
        }
    }

    private static IEnumerable<CommandPaletteCommand> BuildContextualCommands(
        string currentPath,
        bool isAdopter,
        bool isShelter,
        bool isAdmin,
        int? shelterId)
    {
        var path = string.IsNullOrWhiteSpace(currentPath) ? "/" : currentPath;

        if (TryGetDogId(path, out var dogId))
        {
            yield return Command("context-current-dog", "Reopen Current Dog", "Open this dog details page again.", QuickActions, $"/dogs/{dogId}", Icons.Material.Filled.Pets, "current dog", "dog details");

            if (isAdopter)
            {
                yield return Command("context-adoption-request", "Start Adoption Request", "Open this dog profile and start the adoption request workflow.", QuickActions, $"/dogs/{dogId}", Icons.Material.Filled.Assignment, "apply", "adoption request");
            }

            if (isShelter && shelterId.HasValue)
            {
                yield return Command("context-edit-dog", "Edit Current Dog", "Open the shelter edit page for this dog.", QuickActions, $"/shelter/dogs/edit/{dogId}", Icons.Material.Filled.Edit, "edit dog", "profile");
            }

            if (isAdmin)
            {
                yield return Command("context-admin-dog-list", "Open Admin Dog List", "Review this dog from the admin dog list.", QuickActions, "/admin/dogs", Icons.Material.Filled.AdminPanelSettings, "admin dog");
            }
        }

        if (isAdmin && path.StartsWith("/admin/notification-outbox", StringComparison.OrdinalIgnoreCase))
        {
            yield return Command("context-failed-outbox", "Show Failed Notifications", "Filter the notification outbox to failed messages.", QuickActions, "/admin/notification-outbox?status=Failed", Icons.Material.Filled.ErrorOutline, "failed", "outbox");
        }

        if (isShelter && path.StartsWith("/shelter/dogs", StringComparison.OrdinalIgnoreCase))
        {
            yield return Command("context-add-dog", "Add Dog From Here", "Create another dog profile.", QuickActions, "/shelter/dogs/create", Icons.Material.Filled.AddCircle, "add", "new dog");
        }
    }

    private async Task<IReadOnlyList<CommandPaletteCommand>> SearchDogsAsync(
        string query,
        int limit,
        bool isAdmin,
        bool isShelter,
        bool isAdopter,
        int? shelterId,
        CancellationToken cancellationToken)
    {
        var dogs = context.Dogs
            .AsNoTracking()
            .Include(dog => dog.Shelter)
            .AsQueryable();

        if (isShelter && shelterId.HasValue && !isAdmin)
        {
            dogs = dogs.Where(dog => dog.ShelterId == shelterId.Value);
        }
        else if (!isAdmin)
        {
            dogs = dogs.Where(dog => dog.Status == DogStatus.Available || dog.Status == DogStatus.Reserved);
        }

        return await dogs
            .Where(dog =>
                dog.Name.Contains(query) ||
                dog.Breed.Contains(query) ||
                (dog.CustomBreedName != null && dog.CustomBreedName.Contains(query)) ||
                (dog.CoatColor != null && dog.CoatColor.Contains(query)) ||
                dog.Location.Contains(query) ||
                (dog.Shelter != null && dog.Shelter.Name.Contains(query)))
            .OrderBy(dog => dog.Name)
            .Take(limit)
            .Select(dog => Command(
                $"dog-{dog.Id}",
                dog.Name,
                $"{dog.Breed} • {dog.Status} • {dog.Location}",
                Dogs,
                isShelter && !isAdopter && !isAdmin ? $"/shelter/dogs/edit/{dog.Id}" : $"/dogs/{dog.Id}",
                Icons.Material.Filled.Pets,
                new[] { dog.Name, dog.Breed, dog.Location },
                dog.Status.ToString()))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CommandPaletteCommand>> SearchSavedViewsAsync(
        string userId,
        string query,
        int limit,
        bool isAdmin,
        bool isShelter,
        bool isAdopter,
        bool isVolunteer,
        CancellationToken cancellationToken)
    {
        var allowedScopes = GetAllowedSavedViewScopes(isAdmin, isShelter, isAdopter, isVolunteer);
        var savedViews = context.UserSavedViews
            .AsNoTracking()
            .Where(view => view.UserId == userId || (view.IsSystemView && allowedScopes.Contains(view.RoleScope)));

        if (string.IsNullOrWhiteSpace(query))
        {
            savedViews = savedViews.Where(view => view.IsPinned || view.IsDefault);
        }
        else
        {
            savedViews = savedViews.Where(view =>
                view.Name.Contains(query) ||
                view.PageKey.Contains(query) ||
                (view.Description != null && view.Description.Contains(query)) ||
                (view.FilterSummaryJson != null && view.FilterSummaryJson.Contains(query)));
        }

        var views = await savedViews
            .OrderByDescending(view => view.IsPinned)
            .ThenByDescending(view => view.IsDefault)
            .ThenBy(view => view.Name)
            .Take(limit)
            .Select(view => new
            {
                view.Id,
                view.Name,
                view.PageKey,
                view.Description,
                view.IsPinned,
                view.IsDefault,
                view.IsSystemView,
                view.FilterSummaryJson
            })
            .ToListAsync(cancellationToken);

        return views
            .Select(view => new { View = view, Route = GetSavedViewRoute(view.PageKey, view.Id) })
            .Where(item => item.Route is not null)
            .Select(item => Command(
                $"saved-view-{item.View.Id}",
                item.View.Name,
                GetSavedViewDescription(item.View.PageKey, item.View.Description, item.View.FilterSummaryJson),
                SavedViews,
                item.Route!,
                Icons.Material.Filled.Bookmarks,
                new[] { "saved view", "filter preset", item.View.PageKey, item.View.Name },
                item.View.IsPinned ? "Pinned" : item.View.IsDefault ? "Default" : item.View.IsSystemView ? "System" : null))
            .ToList();
    }

    private async Task<IReadOnlyList<CommandPaletteCommand>> SearchNotificationsAsync(
        string userId,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        return await context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .Where(notification =>
                notification.Title.Contains(query) ||
                notification.Message.Contains(query) ||
                (notification.RelatedEntityName != null && notification.RelatedEntityName.Contains(query)))
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(limit)
            .Select(notification => Command(
                $"notification-{notification.Id}",
                notification.Title,
                notification.Message,
                Notifications,
                string.IsNullOrWhiteSpace(notification.Link) ? "/notifications" : notification.Link,
                notification.IsRead ? Icons.Material.Filled.NotificationsNone : Icons.Material.Filled.NotificationsActive,
                new[] { "notification", notification.Title },
                notification.IsRead ? null : "Unread"))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CommandPaletteCommand>> SearchAdopterItemsAsync(
        string adopterUserId,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var requests = await context.AdoptionRequests
            .AsNoTracking()
            .Include(request => request.Dog)
            .Where(request => request.AdopterId == adopterUserId)
            .Where(request =>
                (request.Dog != null && request.Dog.Name.Contains(query)) ||
                request.ReasonForAdoption.Contains(query) ||
                (request.Message != null && request.Message.Contains(query)))
            .OrderByDescending(request => request.CreatedAt)
            .Take(limit)
            .Select(request => Command(
                $"adoption-request-{request.Id}",
                request.Dog == null ? $"Adoption Request #{request.Id}" : $"Adoption Request for {request.Dog.Name}",
                $"{request.Status} • submitted {request.CreatedAt:dd MMM yyyy}",
                Applications,
                "/my-adoption-requests",
                Icons.Material.Filled.Assignment,
                new[] { "application", "request", request.Status.ToString() },
                request.Status.ToString()))
            .ToListAsync(cancellationToken);

        var savedSearches = await context.SavedDogSearches
            .AsNoTracking()
            .Where(search => search.AdopterUserId == adopterUserId)
            .Where(search =>
                search.Name.Contains(query) ||
                (search.SearchText != null && search.SearchText.Contains(query)) ||
                (search.Breed != null && search.Breed.Contains(query)) ||
                (search.Location != null && search.Location.Contains(query)) ||
                (search.Neighborhood != null && search.Neighborhood.Contains(query)))
            .OrderBy(search => search.Name)
            .Take(limit)
            .Select(search => Command(
                $"saved-search-{search.Id}",
                search.Name,
                string.IsNullOrWhiteSpace(search.SearchText) ? "Saved dog search" : search.SearchText,
                Dogs,
                $"/adopter/saved-searches/{search.Id}",
                Icons.Material.Filled.Bookmark,
                new[] { "saved search", "alert", search.Name },
                search.AlertsEnabled ? "Alerts on" : "Alerts off"))
            .ToListAsync(cancellationToken);

        return requests.Concat(savedSearches).ToList();
    }

    private async Task<IReadOnlyList<CommandPaletteCommand>> SearchOperationalItemsAsync(
        string query,
        int limit,
        bool isAdmin,
        int? shelterId,
        CancellationToken cancellationToken)
    {
        var requests = context.AdoptionRequests
            .AsNoTracking()
            .Include(request => request.Dog)
            .ThenInclude(dog => dog!.Shelter)
            .Include(request => request.Adopter)
            .AsQueryable();

        if (!isAdmin)
        {
            if (!shelterId.HasValue)
            {
                return [];
            }

            requests = requests.Where(request => request.Dog != null && request.Dog.ShelterId == shelterId.Value);
        }

        var requestCommands = await requests
            .Where(request =>
                (request.Dog != null && request.Dog.Name.Contains(query)) ||
                (request.Adopter != null && (
                    (request.Adopter.Email != null && request.Adopter.Email.Contains(query)) ||
                    (request.Adopter.FullName != null && request.Adopter.FullName.Contains(query)))) ||
                request.ReasonForAdoption.Contains(query))
            .OrderByDescending(request => request.CreatedAt)
            .Take(limit)
            .Select(request => Command(
                $"ops-adoption-request-{request.Id}",
                request.Dog == null ? $"Adoption Request #{request.Id}" : $"{request.Dog.Name} adoption request",
                request.Adopter == null ? request.Status.ToString() : $"{request.Adopter.Email} • {request.Status}",
                Applications,
                isAdmin ? "/admin/adoption-requests" : "/shelter/adoption-requests",
                Icons.Material.Filled.AssignmentTurnedIn,
                new[] { "application", "request", request.Status.ToString() },
                request.Status.ToString()))
            .ToListAsync(cancellationToken);

        var taskQuery = context.VolunteerTasks
            .AsNoTracking()
            .Include(task => task.Shelter)
            .AsQueryable();

        if (!isAdmin && shelterId.HasValue)
        {
            taskQuery = taskQuery.Where(task => task.ShelterId == shelterId.Value);
        }

        var taskCommands = await taskQuery
            .Where(task =>
                task.Title.Contains(query) ||
                (task.Description != null && task.Description.Contains(query)) ||
                (task.Shelter != null && task.Shelter.Name.Contains(query)))
            .OrderBy(task => task.ScheduledStartUtc)
            .Take(limit)
            .Select(task => Command(
                $"volunteer-task-{task.Id}",
                task.Title,
                task.Shelter == null ? $"{task.Status} • {task.Priority}" : $"{task.Shelter.Name} • {task.Status} • {task.Priority}",
                Volunteer,
                isAdmin ? "/admin/volunteer-tasks" : "/shelter/volunteer-tasks",
                Icons.Material.Filled.TaskAlt,
                new[] { "volunteer", "task", task.Status.ToString(), task.Priority.ToString() },
                task.Status.ToString()))
            .ToListAsync(cancellationToken);

        var failedOutboxCommands = isAdmin
            ? await context.NotificationOutboxMessages
                .AsNoTracking()
                .Where(message => message.Status == NotificationOutboxStatus.Failed || message.Status == NotificationOutboxStatus.DeadLetter)
                .Where(message => message.Subject.Contains(query) || (message.LastError != null && message.LastError.Contains(query)))
                .OrderByDescending(message => message.UpdatedAt)
                .Take(limit)
                .Select(message => Command(
                    $"outbox-{message.Id}",
                    $"Notification Outbox #{message.Id}",
                    $"{message.Subject} • {message.Status}",
                    Notifications,
                    "/admin/notification-outbox",
                    Icons.Material.Filled.Outbox,
                    new[] { "outbox", "failed notification", message.Status.ToString() },
                    message.Status.ToString(),
                    false,
                    true))
                .ToListAsync(cancellationToken)
            : [];

        return requestCommands.Concat(taskCommands).Concat(failedOutboxCommands).ToList();
    }

    private async Task<IReadOnlyList<CommandPaletteCommand>> SearchVolunteerItemsAsync(
        string userId,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var volunteerProfileId = await context.VolunteerProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (int?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!volunteerProfileId.HasValue)
        {
            return [];
        }

        return await context.VolunteerTasks
            .AsNoTracking()
            .Include(task => task.Shelter)
            .Where(task => task.AssignedVolunteerProfileId == volunteerProfileId || task.Status == VolunteerTaskStatus.Open)
            .Where(task =>
                task.Title.Contains(query) ||
                (task.Description != null && task.Description.Contains(query)) ||
                (task.Shelter != null && task.Shelter.Name.Contains(query)))
            .OrderBy(task => task.AssignedVolunteerProfileId == volunteerProfileId ? 0 : 1)
            .ThenBy(task => task.ScheduledStartUtc)
            .Take(limit)
            .Select(task => Command(
                $"volunteer-my-task-{task.Id}",
                task.Title,
                task.Shelter == null ? $"{task.Status} • {task.Priority}" : $"{task.Shelter.Name} • {task.Status} • {task.Priority}",
                Volunteer,
                "/volunteer/tasks",
                Icons.Material.Filled.TaskAlt,
                new[] { "volunteer", "task", task.Status.ToString(), task.Priority.ToString() },
                task.Status.ToString()))
            .ToListAsync(cancellationToken);
    }

    private static bool Matches(CommandPaletteCommand command, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Contains(command.Title, query) ||
               Contains(command.Description, query) ||
               Contains(command.Category, query) ||
               command.Keywords.Any(keyword => Contains(keyword, query));
    }

    private static bool Contains(string? value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeQuery(string? query)
    {
        return string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
    }

    private static int CategoryOrder(string category)
    {
        return category switch
        {
            QuickActions => 0,
            Navigation => 1,
            Dogs => 2,
            Applications => 3,
            ShelterOperations => 4,
            Volunteer => 5,
            SavedViews => 6,
            Notifications => 7,
            Admin => 8,
            _ => 99
        };
    }

    private static SavedViewRoleScope[] GetAllowedSavedViewScopes(
        bool isAdmin,
        bool isShelter,
        bool isAdopter,
        bool isVolunteer)
    {
        var scopes = new List<SavedViewRoleScope> { SavedViewRoleScope.Global };
        if (isAdmin)
        {
            scopes.Add(SavedViewRoleScope.Admin);
        }

        if (isShelter)
        {
            scopes.Add(SavedViewRoleScope.Shelter);
        }

        if (isAdopter)
        {
            scopes.Add(SavedViewRoleScope.Adopter);
        }

        if (isVolunteer)
        {
            scopes.Add(SavedViewRoleScope.Volunteer);
        }

        return scopes.ToArray();
    }

    private static string? GetSavedViewRoute(string pageKey, int savedViewId)
    {
        var route = pageKey switch
        {
            "Dogs.Search" => "/dogs",
            "Shelter.Dogs" => "/shelter/dogs",
            "Admin.Notifications.Outbox" => "/admin/notification-outbox",
            "Admin.Audit" => "/admin/audit-logs",
            "Notifications.Center" => "/notifications",
            "Shelter.Intelligence" => "/shelter/intelligence",
            "Admin.Intelligence" => "/admin/intelligence",
            "Adopter.Insights" => "/adopter/insights",
            _ => null
        };

        return route is null ? null : $"{route}?savedViewId={savedViewId}";
    }

    private static string GetSavedViewDescription(string pageKey, string? description, string? filterSummaryJson)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        if (!string.IsNullOrWhiteSpace(filterSummaryJson))
        {
            return $"{GetSavedViewPageLabel(pageKey)} filter preset";
        }

        return $"Open {GetSavedViewPageLabel(pageKey)} with saved filters.";
    }

    private static string GetSavedViewPageLabel(string pageKey)
    {
        return pageKey switch
        {
            "Dogs.Search" => "dog browsing",
            "Shelter.Dogs" => "shelter dogs",
            "Admin.Notifications.Outbox" => "notification outbox",
            "Admin.Audit" => "audit logs",
            "Notifications.Center" => "notifications",
            "Shelter.Intelligence" => "shelter intelligence",
            "Admin.Intelligence" => "platform intelligence",
            "Adopter.Insights" => "adopter insights",
            _ => "this page"
        };
    }

    private static bool TryGetDogId(string currentPath, out int dogId)
    {
        dogId = 0;
        var path = currentPath.Split('?', '#')[0].Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length == 2 &&
               string.Equals(segments[0], "dogs", StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(segments[1], out dogId);
    }

    private static CommandPaletteCommand Command(
        string id,
        string title,
        string description,
        string category,
        string route,
        string icon,
        params string[] keywords)
    {
        return new CommandPaletteCommand(id, title, description, category, route, icon, keywords);
    }

    private static CommandPaletteCommand Command(
        string id,
        string title,
        string description,
        string category,
        string route,
        string icon,
        IReadOnlyList<string> keywords,
        string? badge = null,
        bool requiresConfirmation = false,
        bool isSensitive = false)
    {
        return new CommandPaletteCommand(id, title, description, category, route, icon, keywords, badge, requiresConfirmation, isSensitive);
    }
}
