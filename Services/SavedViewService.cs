using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public sealed class SavedViewService(ApplicationDbContext context) : ISavedViewService
{
    private const int MaxNameLength = 120;
    private const int MaxDescriptionLength = 300;
    private const int MaxJsonLength = 8000;
    private const int MaxViewModeLength = 80;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<string, SavedViewPageConfigDto> PageConfigs =
        new Dictionary<string, SavedViewPageConfigDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dogs.Search"] = new("Dogs.Search", "Dog browsing", [SavedViewRoleScope.Global, SavedViewRoleScope.Adopter, SavedViewRoleScope.Shelter, SavedViewRoleScope.Admin, SavedViewRoleScope.Volunteer]),
            ["Shelter.Dogs"] = new("Shelter.Dogs", "Shelter dog management", [SavedViewRoleScope.Shelter]),
            ["Admin.Notifications.Outbox"] = new("Admin.Notifications.Outbox", "Notification outbox", [SavedViewRoleScope.Admin]),
            ["Admin.Audit"] = new("Admin.Audit", "Audit logs", [SavedViewRoleScope.Admin]),
            ["Notifications.Center"] = new("Notifications.Center", "Notification center", [SavedViewRoleScope.Global, SavedViewRoleScope.Adopter, SavedViewRoleScope.Shelter, SavedViewRoleScope.Admin, SavedViewRoleScope.Volunteer])
        };

    public async Task<IReadOnlyList<SavedViewDto>> GetViewsForPageAsync(
        string userId,
        IEnumerable<string> userRoles,
        string pageKey,
        CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        EnsureCanAccessPage(userRoles, pageKey);
        var roleScopes = GetAllowedRoleScopes(userRoles).ToHashSet();

        var views = await context.UserSavedViews
            .Where(view => view.PageKey == NormalizePageKey(pageKey) &&
                ((view.UserId == userId && !view.IsSystemView) ||
                 (view.IsSystemView && roleScopes.Contains(view.RoleScope))))
            .OrderByDescending(view => view.IsPinned)
            .ThenByDescending(view => view.IsDefault)
            .ThenByDescending(view => view.IsSystemView)
            .ThenBy(view => view.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return views.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<SavedViewDto>> GetPinnedViewsAsync(
        string userId,
        IEnumerable<string> userRoles,
        CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        var roleScopes = GetAllowedRoleScopes(userRoles).ToHashSet();
        var pageKeys = PageConfigs.Values
            .Where(config => config.AllowedScopes.Any(roleScopes.Contains))
            .Select(config => config.PageKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var views = await context.UserSavedViews
            .Where(view => view.IsPinned &&
                pageKeys.Contains(view.PageKey) &&
                ((view.UserId == userId && !view.IsSystemView) ||
                 (view.IsSystemView && roleScopes.Contains(view.RoleScope))))
            .OrderBy(view => view.PageKey)
            .ThenBy(view => view.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return views.Select(ToDto).ToList();
    }

    public async Task<SavedViewDto?> GetDefaultViewAsync(
        string userId,
        IEnumerable<string> userRoles,
        string pageKey,
        CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        EnsureCanAccessPage(userRoles, pageKey);

        var view = await context.UserSavedViews
            .Where(candidate => candidate.UserId == userId &&
                candidate.PageKey == NormalizePageKey(pageKey) &&
                candidate.IsDefault &&
                !candidate.IsSystemView)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return view is null ? null : ToDto(view);
    }

    public async Task<IReadOnlyList<SavedViewDto>> GetSystemViewsForPageAsync(
        IEnumerable<string> userRoles,
        string pageKey,
        CancellationToken cancellationToken = default)
    {
        EnsureCanAccessPage(userRoles, pageKey);
        var roleScopes = GetAllowedRoleScopes(userRoles).ToHashSet();

        var views = await context.UserSavedViews
            .Where(view => view.IsSystemView &&
                view.PageKey == NormalizePageKey(pageKey) &&
                roleScopes.Contains(view.RoleScope))
            .OrderBy(view => view.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return views.Select(ToDto).ToList();
    }

    public async Task<SavedViewDto> CreateViewAsync(
        string userId,
        IEnumerable<string> userRoles,
        SavedViewCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        EnsureCanAccessPage(userRoles, request.PageKey);
        EnsureCanUseScope(userRoles, request.PageKey, request.RoleScope);

        var normalizedName = NormalizeRequired(request.Name, MaxNameLength, "Saved view name is required.");
        var normalizedPageKey = NormalizePageKey(request.PageKey);
        await EnsureUniqueNameAsync(userId, normalizedPageKey, normalizedName, null, cancellationToken);

        var now = DateTime.UtcNow;
        var view = new UserSavedView
        {
            UserId = userId,
            Name = normalizedName,
            PageKey = normalizedPageKey,
            RoleScope = request.RoleScope,
            Description = NormalizeOptional(request.Description, MaxDescriptionLength),
            FilterStateJson = NormalizeJson(request.FilterStateJson, "{}") ?? "{}",
            SortStateJson = NormalizeJson(request.SortStateJson, null),
            ColumnStateJson = NormalizeJson(request.ColumnStateJson, null),
            ViewMode = NormalizeOptional(request.ViewMode, MaxViewModeLength),
            FilterSummaryJson = SerializeSummary(request.SummaryLabels),
            IsPinned = request.IsPinned,
            IsDefault = request.IsDefault,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        if (view.IsDefault)
        {
            await ClearDefaultForPageAsync(userId, view.PageKey, cancellationToken);
        }

        context.UserSavedViews.Add(view);
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(view);
    }

    public async Task<SavedViewDto> UpdateViewAsync(
        int savedViewId,
        string userId,
        IEnumerable<string> userRoles,
        SavedViewUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        var view = await GetOwnedEditableViewAsync(savedViewId, userId, cancellationToken);
        EnsureCanAccessPage(userRoles, view.PageKey);
        await EnsureUniqueNameAsync(userId, view.PageKey, NormalizeRequired(request.Name, MaxNameLength, "Saved view name is required."), savedViewId, cancellationToken);

        view.Name = NormalizeRequired(request.Name, MaxNameLength, "Saved view name is required.");
        view.Description = NormalizeOptional(request.Description, MaxDescriptionLength);
        view.FilterStateJson = NormalizeJson(request.FilterStateJson, "{}") ?? "{}";
        view.SortStateJson = NormalizeJson(request.SortStateJson, null);
        view.ColumnStateJson = NormalizeJson(request.ColumnStateJson, null);
        view.ViewMode = NormalizeOptional(request.ViewMode, MaxViewModeLength);
        view.FilterSummaryJson = SerializeSummary(request.SummaryLabels);
        view.IsPinned = request.IsPinned;
        view.IsDefault = request.IsDefault;
        view.UpdatedAtUtc = DateTime.UtcNow;

        if (view.IsDefault)
        {
            await ClearDefaultForPageAsync(userId, view.PageKey, cancellationToken, savedViewId);
        }

        await context.SaveChangesAsync(cancellationToken);
        return ToDto(view);
    }

    public async Task<SavedViewDto> RenameViewAsync(
        int savedViewId,
        string userId,
        SavedViewRenameRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        var view = await GetOwnedEditableViewAsync(savedViewId, userId, cancellationToken);
        var normalizedName = NormalizeRequired(request.Name, MaxNameLength, "Saved view name is required.");
        await EnsureUniqueNameAsync(userId, view.PageKey, normalizedName, savedViewId, cancellationToken);

        view.Name = normalizedName;
        view.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(view);
    }

    public async Task DeleteViewAsync(
        int savedViewId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        var view = await GetOwnedEditableViewAsync(savedViewId, userId, cancellationToken);
        context.UserSavedViews.Remove(view);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetDefaultViewAsync(
        int savedViewId,
        string userId,
        IEnumerable<string> userRoles,
        CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        var view = await GetOwnedEditableViewAsync(savedViewId, userId, cancellationToken);
        EnsureCanAccessPage(userRoles, view.PageKey);

        await ClearDefaultForPageAsync(userId, view.PageKey, cancellationToken, savedViewId);
        view.IsDefault = true;
        view.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task PinViewAsync(int savedViewId, string userId, CancellationToken cancellationToken = default)
    {
        return SetPinnedAsync(savedViewId, userId, true, cancellationToken);
    }

    public Task UnpinViewAsync(int savedViewId, string userId, CancellationToken cancellationToken = default)
    {
        return SetPinnedAsync(savedViewId, userId, false, cancellationToken);
    }

    public async Task MarkViewUsedAsync(
        int savedViewId,
        string userId,
        IEnumerable<string> userRoles,
        CancellationToken cancellationToken = default)
    {
        EnsureUser(userId);
        var roleScopes = GetAllowedRoleScopes(userRoles).ToHashSet();
        var view = await context.UserSavedViews.FirstOrDefaultAsync(
            candidate => candidate.Id == savedViewId &&
                ((candidate.UserId == userId && !candidate.IsSystemView) ||
                 (candidate.IsSystemView && roleScopes.Contains(candidate.RoleScope))),
            cancellationToken);

        if (view is null)
        {
            return;
        }

        EnsureCanAccessPage(userRoles, view.PageKey);
        view.LastUsedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SetPinnedAsync(int savedViewId, string userId, bool isPinned, CancellationToken cancellationToken)
    {
        EnsureUser(userId);
        var view = await GetOwnedEditableViewAsync(savedViewId, userId, cancellationToken);
        view.IsPinned = isPinned;
        view.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserSavedView> GetOwnedEditableViewAsync(
        int savedViewId,
        string userId,
        CancellationToken cancellationToken)
    {
        var view = await context.UserSavedViews.FirstOrDefaultAsync(
            candidate => candidate.Id == savedViewId && candidate.UserId == userId,
            cancellationToken);

        if (view is null)
        {
            throw new InvalidOperationException("Saved view was not found.");
        }

        if (view.IsSystemView)
        {
            throw new InvalidOperationException("System views cannot be modified.");
        }

        return view;
    }

    private async Task EnsureUniqueNameAsync(
        string userId,
        string pageKey,
        string name,
        int? exceptId,
        CancellationToken cancellationToken)
    {
        var exists = await context.UserSavedViews.AnyAsync(
            view => view.UserId == userId &&
                view.PageKey == pageKey &&
                view.Name == name &&
                (!exceptId.HasValue || view.Id != exceptId.Value),
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("You already have a saved view with this name on this page.");
        }
    }

    private async Task ClearDefaultForPageAsync(
        string userId,
        string pageKey,
        CancellationToken cancellationToken,
        int? exceptId = null)
    {
        var existingDefaults = await context.UserSavedViews
            .Where(view => view.UserId == userId &&
                view.PageKey == pageKey &&
                view.IsDefault &&
                (!exceptId.HasValue || view.Id != exceptId.Value))
            .ToListAsync(cancellationToken);

        foreach (var view in existingDefaults)
        {
            view.IsDefault = false;
            view.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private static SavedViewDto ToDto(UserSavedView view)
    {
        return new SavedViewDto(
            view.Id,
            view.Name,
            view.PageKey,
            view.RoleScope,
            view.Description,
            view.FilterStateJson,
            view.SortStateJson,
            view.ColumnStateJson,
            view.ViewMode,
            view.IsPinned,
            view.IsDefault,
            view.IsSystemView,
            view.CreatedAtUtc,
            view.UpdatedAtUtc,
            view.LastUsedAtUtc,
            DeserializeSummary(view.FilterSummaryJson));
    }

    private static string NormalizePageKey(string pageKey)
    {
        var normalized = NormalizeRequired(pageKey, 120, "Saved view page key is required.");
        if (!PageConfigs.ContainsKey(normalized))
        {
            throw new InvalidOperationException("Saved views are not supported on this page.");
        }

        return PageConfigs[normalized].PageKey;
    }

    private static void EnsureCanAccessPage(IEnumerable<string> userRoles, string pageKey)
    {
        var normalizedPageKey = NormalizePageKey(pageKey);
        var allowedScopes = PageConfigs[normalizedPageKey].AllowedScopes;
        if (!allowedScopes.Any(GetAllowedRoleScopes(userRoles).Contains))
        {
            throw new InvalidOperationException("You cannot manage saved views for this page.");
        }
    }

    private static void EnsureCanUseScope(IEnumerable<string> userRoles, string pageKey, SavedViewRoleScope roleScope)
    {
        var normalizedPageKey = NormalizePageKey(pageKey);
        var allowedForPage = PageConfigs[normalizedPageKey].AllowedScopes.Contains(roleScope);
        var allowedForUser = GetAllowedRoleScopes(userRoles).Contains(roleScope);

        if (!allowedForPage || !allowedForUser)
        {
            throw new InvalidOperationException("This saved view scope is not available for your account.");
        }
    }

    private static IReadOnlyList<SavedViewRoleScope> GetAllowedRoleScopes(IEnumerable<string> roles)
    {
        var normalized = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scopes = new List<SavedViewRoleScope> { SavedViewRoleScope.Global };
        if (normalized.Contains(IdentitySeedData.AdminRole))
        {
            scopes.Add(SavedViewRoleScope.Admin);
        }

        if (normalized.Contains(IdentitySeedData.ShelterRole))
        {
            scopes.Add(SavedViewRoleScope.Shelter);
        }

        if (normalized.Contains(IdentitySeedData.AdopterRole))
        {
            scopes.Add(SavedViewRoleScope.Adopter);
        }

        if (normalized.Contains(IdentitySeedData.VolunteerRole))
        {
            scopes.Add(SavedViewRoleScope.Volunteer);
        }

        return scopes;
    }

    private static void EnsureUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Current user could not be found.");
        }
    }

    private static string NormalizeRequired(string? value, int maxLength, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeJson(string? value, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        if (normalized.Length > MaxJsonLength)
        {
            throw new InvalidOperationException("Saved view state is too large.");
        }

        try
        {
            using var _ = JsonDocument.Parse(normalized);
            return normalized;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Saved view state is not valid JSON.");
        }
    }

    private static string? SerializeSummary(IReadOnlyList<string>? labels)
    {
        var normalized = labels?
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        return normalized is { Count: > 0 }
            ? JsonSerializer.Serialize(normalized, JsonOptions)
            : null;
    }

    private static IReadOnlyList<string> DeserializeSummary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
