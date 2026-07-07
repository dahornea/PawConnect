using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record SavedViewDto(
    int Id,
    string Name,
    string PageKey,
    SavedViewRoleScope RoleScope,
    string? Description,
    string FilterStateJson,
    string? SortStateJson,
    string? ColumnStateJson,
    string? ViewMode,
    bool IsPinned,
    bool IsDefault,
    bool IsSystemView,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastUsedAtUtc,
    IReadOnlyList<string> SummaryLabels);

public sealed record SavedViewCreateRequest(
    string Name,
    string PageKey,
    SavedViewRoleScope RoleScope,
    string? Description,
    string FilterStateJson,
    string? SortStateJson = null,
    string? ColumnStateJson = null,
    string? ViewMode = null,
    bool IsPinned = false,
    bool IsDefault = false,
    IReadOnlyList<string>? SummaryLabels = null);

public sealed record SavedViewUpdateRequest(
    string Name,
    string? Description,
    string FilterStateJson,
    string? SortStateJson = null,
    string? ColumnStateJson = null,
    string? ViewMode = null,
    bool IsPinned = false,
    bool IsDefault = false,
    IReadOnlyList<string>? SummaryLabels = null);

public sealed record SavedViewRenameRequest(string Name);

public sealed record SavedViewPageConfigDto(
    string PageKey,
    string DisplayName,
    IReadOnlyList<SavedViewRoleScope> AllowedScopes);
