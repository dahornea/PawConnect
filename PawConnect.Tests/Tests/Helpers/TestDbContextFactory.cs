using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Tests.Tests.Helpers;

public static class TestDbContextFactory
{
    public const string AdopterId = "adopter-test-id";
    public const string SecondAdopterId = "second-adopter-test-id";
    public const string ShelterUserId = "shelter-user-test-id";
    public const string OtherShelterUserId = "other-shelter-user-test-id";
    public const string AdminId = "admin-test-id";
    public const int ShelterId = 1;
    public const int OtherShelterId = 2;
    public const int FoodCategoryId = 1;
    public const int MedicineCategoryId = 2;
    public const int AdultFoodTypeId = 1;

    public static string CreateDatabaseName()
    {
        return Guid.NewGuid().ToString();
    }

    public static ApplicationDbContext CreateContext(string? databaseName = null)
    {
        var options = CreateOptions(databaseName ?? CreateDatabaseName());

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        SeedIdentityAndLookups(context);
        return context;
    }

    public static IDbContextFactory<ApplicationDbContext> CreateContextFactory(string databaseName)
    {
        return new InMemoryApplicationDbContextFactory(CreateOptions(databaseName));
    }

    public static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext context)
    {
        var store = new UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>(context);

        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    public static Dog CreateDog(
        string name,
        DogStatus status = DogStatus.Available,
        int shelterId = ShelterId,
        int ageYears = 2,
        int ageMonths = 0)
    {
        return new Dog
        {
            Name = name,
            Breed = "Mixed Breed",
            Age = ageYears,
            AgeYears = ageYears,
            AgeMonths = ageMonths,
            Size = DogSize.Medium,
            Location = "Bucharest",
            Status = status,
            ShelterId = shelterId,
            Description = $"{name} test dog"
        };
    }

    private static void SeedIdentityAndLookups(ApplicationDbContext context)
    {
        context.Roles.AddRange(
            Role(IdentitySeedData.AdopterRole),
            Role(IdentitySeedData.ShelterRole),
            Role(IdentitySeedData.AdminRole));

        context.Users.AddRange(
            User(AdopterId, "adopter@test.com", "Test Adopter"),
            User(SecondAdopterId, "adopter2@test.com", "Second Adopter"),
            User(ShelterUserId, "shelter@test.com", "Test Shelter"),
            User(OtherShelterUserId, "other-shelter@test.com", "Other Shelter"),
            User(AdminId, "admin@test.com", "Test Admin"));

        context.UserRoles.AddRange(
            UserRole(AdopterId, IdentitySeedData.AdopterRole),
            UserRole(SecondAdopterId, IdentitySeedData.AdopterRole),
            UserRole(ShelterUserId, IdentitySeedData.ShelterRole),
            UserRole(OtherShelterUserId, IdentitySeedData.ShelterRole),
            UserRole(AdminId, IdentitySeedData.AdminRole));

        context.Shelters.AddRange(
            new Shelter
            {
                Id = ShelterId,
                Name = "Test Shelter",
                Address = "Shelter Street 1",
                City = "Bucharest",
                Email = "shelter@test.com",
                PhoneNumber = "123",
                ApplicationUserId = ShelterUserId
            },
            new Shelter
            {
                Id = OtherShelterId,
                Name = "Other Shelter",
                Address = "Other Street 1",
                City = "Cluj",
                Email = "other-shelter@test.com",
                PhoneNumber = "456",
                ApplicationUserId = OtherShelterUserId
            });

        if (!context.ResourceCategories.Any(c => c.Id == FoodCategoryId))
        {
            context.ResourceCategories.Add(new ResourceCategory { Id = FoodCategoryId, Name = "Food" });
        }

        if (!context.ResourceCategories.Any(c => c.Id == MedicineCategoryId))
        {
            context.ResourceCategories.Add(new ResourceCategory { Id = MedicineCategoryId, Name = "Medicine" });
        }

        if (!context.FoodTypes.Any(f => f.Id == AdultFoodTypeId))
        {
            context.FoodTypes.Add(new FoodType { Id = AdultFoodTypeId, Name = "Adult dry food" });
        }

        context.SaveChanges();
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

    private static DbContextOptions<ApplicationDbContext> CreateOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;
    }

    private sealed class InMemoryApplicationDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
        {
            return new ApplicationDbContext(options);
        }
    }
}
