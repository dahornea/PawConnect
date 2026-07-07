namespace PawConnect.Services.CommandPalette;

public sealed record CommandPaletteCommand(
    string Id,
    string Title,
    string Description,
    string Category,
    string Route,
    string Icon,
    IReadOnlyList<string> Keywords,
    string? Badge = null,
    bool RequiresConfirmation = false,
    bool IsSensitive = false);
