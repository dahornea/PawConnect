using System.Security.Claims;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services.CommandPalette;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class CommandPaletteServiceTests
{
    [Fact]
    public async Task SearchAsync_AdminReceivesAdminCommands()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CommandPaletteService(context);

        var commands = await service.SearchAsync(new CommandPaletteSearchRequest(
            Principal(TestDbContextFactory.AdminId, IdentitySeedData.AdminRole),
            "audit",
            "/admin/dashboard"));

        Assert.Contains(commands, command => command.Id == "admin-audit-logs");
    }

    [Fact]
    public async Task SearchAsync_AdopterDoesNotReceiveAdminCommands()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CommandPaletteService(context);

        var commands = await service.SearchAsync(new CommandPaletteSearchRequest(
            Principal(TestDbContextFactory.AdopterId, IdentitySeedData.AdopterRole),
            "admin",
            "/adopter/dashboard"));

        Assert.DoesNotContain(commands, command => command.Category == "Admin");
    }

    [Fact]
    public async Task SearchAsync_FiltersStaticCommandsByKeyword()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CommandPaletteService(context);

        var commands = await service.SearchAsync(new CommandPaletteSearchRequest(
            Principal(TestDbContextFactory.AdopterId, IdentitySeedData.AdopterRole),
            "copilot",
            "/adopter/dashboard"));

        Assert.Contains(commands, command => command.Id == "adopter-copilot");
        Assert.DoesNotContain(commands, command => command.Id == "adopter-favorites");
    }

    [Fact]
    public async Task SearchAsync_ShelterDogResultsAreScopedToOwnShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Buddy", shelterId: TestDbContextFactory.ShelterId),
            TestDbContextFactory.CreateDog("Buddy Other", shelterId: TestDbContextFactory.OtherShelterId));
        await context.SaveChangesAsync();
        var service = new CommandPaletteService(context);

        var commands = await service.SearchAsync(new CommandPaletteSearchRequest(
            Principal(TestDbContextFactory.ShelterUserId, IdentitySeedData.ShelterRole),
            "Buddy",
            "/shelter/dogs"));

        Assert.Contains(commands, command => command.Title == "Buddy" && command.Route.StartsWith("/shelter/dogs/edit/", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Title == "Buddy Other");
    }

    [Fact]
    public async Task SearchAsync_PublicDogResultsExcludePrivateStatuses()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Public Buddy", DogStatus.Available),
            TestDbContextFactory.CreateDog("Hidden Buddy", DogStatus.InTreatment));
        await context.SaveChangesAsync();
        var service = new CommandPaletteService(context);

        var commands = await service.SearchAsync(new CommandPaletteSearchRequest(
            new ClaimsPrincipal(new ClaimsIdentity()),
            "Buddy",
            "/dogs"));

        Assert.Contains(commands, command => command.Title == "Public Buddy");
        Assert.DoesNotContain(commands, command => command.Title == "Hidden Buddy");
    }

    private static ClaimsPrincipal Principal(string userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, $"{userId}@example.local")
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
