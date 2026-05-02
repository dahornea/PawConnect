using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PawConnect.Entities;

namespace PawConnect.Data;

public static class IdentitySeedData
{
    public const string AdopterRole = "Adopter";
    public const string ShelterRole = "Shelter";
    public const string AdminRole = "Admin";

    public const string DefaultPassword = "PawConnect123!";
    public const string AdopterUserId = "00000000-0000-0000-0000-000000000001";
    public const string ShelterUserId = "11111111-1111-1111-1111-111111111111";
    public const string AdminUserId = "22222222-2222-2222-2222-222222222222";

    private static readonly (string Id, string Email, string FullName, string Role)[] Users =
    [
        (AdopterUserId, "adopter@test.com", "Demo Adopter", AdopterRole),
        (ShelterUserId, "shelter@test.com", "Demo Shelter", ShelterRole),
        (AdminUserId, "admin@test.com", "Demo Admin", AdminRole)
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var role in new[] { AdopterRole, ShelterRole, AdminRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        foreach (var seedUser in Users)
        {
            var user = await userManager.FindByIdAsync(seedUser.Id)
                ?? await userManager.FindByEmailAsync(seedUser.Email);

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
            else
            {
                user.UserName = seedUser.Email;
                user.Email = seedUser.Email;
                user.EmailConfirmed = true;
                user.FullName = seedUser.FullName;

                await userManager.UpdateAsync(user);
            }

            if (!await userManager.IsInRoleAsync(user, seedUser.Role))
            {
                await userManager.AddToRoleAsync(user, seedUser.Role);
            }
        }

        await SeedDomainDataAsync(context);
    }

    private static async Task SeedDomainDataAsync(ApplicationDbContext context)
    {
        if (await context.Shelters.AnyAsync())
        {
            return;
        }

        var shelter = new Shelter
        {
            Name = "PawConnect Demo Shelter",
            Description = "Development shelter used for demo data and dashboard testing.",
            Address = "123 Shelter Street",
            City = "Bucharest",
            PhoneNumber = "+40 700 000 001",
            Email = "shelter@test.com",
            ApplicationUserId = ShelterUserId
        };

        shelter.Dogs =
        [
            new Dog
            {
                Name = "Max",
                Breed = "Mixed Breed",
                Age = 3,
                Size = DogSize.Medium,
                Location = "Bucharest",
                Status = DogStatus.Available,
                Description = "Friendly and playful dog looking for an active family.",
                BehaviorDescription = "Energetic, social, and good on walks.",
                MedicalStatus = "Vaccinated and dewormed.",
                Images = [new DogImage { ImageUrl = "https://placehold.co/800x500?text=Max", IsMainImage = true }],
                MedicalRecords =
                [
                    new MedicalRecord
                    {
                        VaccineName = "Rabies",
                        TreatmentDescription = "Annual rabies vaccination.",
                        RecordDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                        Notes = "No adverse reaction."
                    }
                ]
            },
            new Dog
            {
                Name = "Bella",
                Breed = "Labrador Mix",
                Age = 5,
                Size = DogSize.Large,
                Location = "Bucharest",
                Status = DogStatus.Reserved,
                Description = "Calm, affectionate, and good with people.",
                BehaviorDescription = "Gentle and patient.",
                MedicalStatus = "Healthy.",
                Images = [new DogImage { ImageUrl = "https://placehold.co/800x500?text=Bella", IsMainImage = true }]
            },
            new Dog
            {
                Name = "Luna",
                Breed = "Terrier Mix",
                Age = 1,
                Size = DogSize.Small,
                Location = "Ilfov",
                Status = DogStatus.InTreatment,
                Description = "Young dog currently receiving basic medical care.",
                BehaviorDescription = "Curious but shy around new people.",
                MedicalStatus = "Under treatment for a minor skin condition.",
                Images = [new DogImage { ImageUrl = "https://placehold.co/800x500?text=Luna", IsMainImage = true }],
                MedicalRecords =
                [
                    new MedicalRecord
                    {
                        TreatmentDescription = "Skin treatment plan started.",
                        RecordDate = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
                        Notes = "Follow-up recommended after two weeks."
                    }
                ]
            },
            new Dog
            {
                Name = "Rocky",
                Breed = "German Shepherd Mix",
                Age = 4,
                Size = DogSize.Large,
                Location = "Brasov",
                Status = DogStatus.Available,
                Description = "Loyal dog suitable for an experienced adopter.",
                BehaviorDescription = "Protective, smart, and active.",
                MedicalStatus = "Vaccinated.",
                Images = [new DogImage { ImageUrl = "https://placehold.co/800x500?text=Rocky", IsMainImage = true }]
            },
            new Dog
            {
                Name = "Milo",
                Breed = "Beagle Mix",
                Age = 2,
                Size = DogSize.Medium,
                Location = "Cluj-Napoca",
                Status = DogStatus.Adopted,
                Description = "Cheerful dog used as an adopted example in demo data.",
                BehaviorDescription = "Playful and food motivated.",
                MedicalStatus = "Healthy.",
                Images = [new DogImage { ImageUrl = "https://placehold.co/800x500?text=Milo", IsMainImage = true }]
            }
        ];

        shelter.ResourceStocks =
        [
            new ResourceStock { Name = "Dry Food", Quantity = 50, Unit = "kg", LowStockThreshold = 15 },
            new ResourceStock { Name = "Blankets", Quantity = 20, Unit = "pcs", LowStockThreshold = 5 },
            new ResourceStock { Name = "Medicine Kits", Quantity = 2, Unit = "pcs", LowStockThreshold = 3 }
        ];

        context.Shelters.Add(shelter);
        await context.SaveChangesAsync();
    }
}
