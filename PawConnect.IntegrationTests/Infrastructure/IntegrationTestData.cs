using Microsoft.AspNetCore.Identity;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.IntegrationTests.Infrastructure;

public static class IntegrationTestData
{
    public const string AdopterId = "integration-adopter";
    public const string ShelterUserId = "integration-shelter";
    public const string AdminId = "integration-admin";

    public static async Task SeedIdentityAndShelterAsync(ApplicationDbContext context)
    {
        context.Roles.AddRange(
            Role(IdentitySeedData.AdopterRole),
            Role(IdentitySeedData.ShelterRole),
            Role(IdentitySeedData.AdminRole));

        context.Users.AddRange(
            User(AdopterId, "integration.adopter@pawconnect.local", "Integration Adopter"),
            User(ShelterUserId, "integration.shelter@pawconnect.local", "Integration Shelter"),
            User(AdminId, "integration.admin@pawconnect.local", "Integration Admin"));

        context.UserRoles.AddRange(
            UserRole(AdopterId, IdentitySeedData.AdopterRole),
            UserRole(ShelterUserId, IdentitySeedData.ShelterRole),
            UserRole(AdminId, IdentitySeedData.AdminRole));

        context.Shelters.Add(CreateShelter());
        await context.SaveChangesAsync();
    }

    public static Shelter CreateShelter()
    {
        return new Shelter
        {
            Name = "Integration Paws Shelter",
            Description = "Shelter used only by SQL Server integration tests.",
            Address = "Integration Street 1",
            City = "Cluj-Napoca",
            Neighborhood = "Zorilor",
            Email = "integration.shelter@pawconnect.local",
            PhoneNumber = "+40 700 000 999",
            ApplicationUserId = ShelterUserId
        };
    }

    public static Dog CreateDog(int shelterId, string name = "Integration Dog")
    {
        return new Dog
        {
            Name = name,
            Breed = "Labrador Retriever",
            Age = 3,
            AgeYears = 3,
            AgeMonths = 0,
            Size = DogSize.Medium,
            Location = "Cluj-Napoca",
            CoatColor = "Golden",
            Description = "Enjoys steady walks and settles indoors after a predictable routine.",
            BehaviorDescription = "Responds well to gentle handling and calm introductions.",
            MedicalStatus = "Vaccinated and healthy.",
            Status = DogStatus.Available,
            ShelterId = shelterId
        };
    }

    private static IdentityRole Role(string roleName)
    {
        return new IdentityRole
        {
            Id = roleName,
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant()
        };
    }

    private static ApplicationUser User(string id, string email, string fullName)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            FullName = fullName
        };
    }

    private static IdentityUserRole<string> UserRole(string userId, string roleName)
    {
        return new IdentityUserRole<string>
        {
            UserId = userId,
            RoleId = roleName
        };
    }
}
