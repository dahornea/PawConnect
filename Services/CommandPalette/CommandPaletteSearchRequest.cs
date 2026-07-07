using System.Security.Claims;

namespace PawConnect.Services.CommandPalette;

public sealed record CommandPaletteSearchRequest(
    ClaimsPrincipal User,
    string? Query,
    string CurrentPath,
    int LimitPerGroup = 6,
    CancellationToken CancellationToken = default);
