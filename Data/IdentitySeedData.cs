using Microsoft.AspNetCore.Identity;

namespace PawConnect.Data;

public static class IdentitySeedData
{
    public const string AdopterRole = "Adopter";
    public const string ShelterRole = "Shelter";
    public const string AdminRole = "Admin";

    private const string DefaultPassword = "PawConnect123!";

    private static readonly (string Id, string Email, string FullName, string Role)[] Users =
    [
        ("00000000-0000-0000-0000-000000000001", "adopter@pawconnect.test", "Demo Adopter", AdopterRole),
        ("11111111-1111-1111-1111-111111111111", "shelter@pawconnect.test", "Demo Shelter", ShelterRole),
        ("22222222-2222-2222-2222-222222222222", "admin@pawconnect.test", "Demo Admin", AdminRole)
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { AdopterRole, ShelterRole, AdminRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        foreach (var seedUser in Users)
        {
            var user = await userManager.FindByEmailAsync(seedUser.Email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = seedUser.Id,
                    UserName = seedUser.Email,
                    Email = seedUser.Email,
                    EmailConfirmed = true,
                    FullName = seedUser.FullName
                };

                await userManager.CreateAsync(user, DefaultPassword);
            }

            if (!await userManager.IsInRoleAsync(user, seedUser.Role))
            {
                await userManager.AddToRoleAsync(user, seedUser.Role);
            }
        }
    }
}
