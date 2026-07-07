namespace PawConnect.Services.CommandPalette;

public interface ICommandPaletteService
{
    Task<IReadOnlyList<CommandPaletteCommand>> SearchAsync(CommandPaletteSearchRequest request);
}
