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
    private const int FoodCategoryId = 1;
    private const int MedicineCategoryId = 2;
    private const int BlanketsCategoryId = 3;
    private const int CleaningSuppliesCategoryId = 4;
    private const int AccessoriesCategoryId = 5;
    private const int OtherCategoryId = 6;
    private const int AdultDryFoodTypeId = 1;
    private const int PuppyFoodTypeId = 2;
    private const int SeniorFoodTypeId = 3;
    private const int WetFoodTypeId = 4;
    private const int MedicalDietFoodTypeId = 5;

    private static readonly (string Id, string Email, string FullName, string Role)[] Users =
    [
        (AdopterUserId, "adopter@test.com", "Demo Adopter", AdopterRole),
        (ShelterUserId, "u8878233525@id.gle", "Demo Shelter", ShelterRole),
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

        await SeedLookupDataAsync(context);
        await SeedAdopterProfileAsync(context);
        await SeedDomainDataAsync(context);
    }

    private static async Task SeedAdopterProfileAsync(ApplicationDbContext context)
    {
        if (await context.AdopterProfiles.AnyAsync(p => p.ApplicationUserId == AdopterUserId))
        {
            return;
        }

        context.AdopterProfiles.Add(new AdopterProfile
        {
            ApplicationUserId = AdopterUserId,
            FullName = "Maria Popescu",
            ProfileImageUrl = "https://placehold.co/300x300?text=Adopter",
            Address = "45 Green Street",
            City = "Bucharest",
            PhoneNumber = "+40 721 123 456",
            HousingType = HousingType.Apartment,
            HasYard = false,
            HasOtherPets = true,
            HasChildren = false,
            ExperienceWithDogs = "Grew up with family dogs and has experience caring for medium-sized dogs.",
            AdditionalNotes = "Works from home several days per week and can provide daily walks."
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedLookupDataAsync(ApplicationDbContext context)
    {
        if (!await context.ResourceCategories.AnyAsync())
        {
            context.ResourceCategories.AddRange(
                new ResourceCategory { Id = FoodCategoryId, Name = "Food", Description = "Food supplies for dogs." },
                new ResourceCategory { Id = MedicineCategoryId, Name = "Medicine", Description = "Medication and medical supplies." },
                new ResourceCategory { Id = BlanketsCategoryId, Name = "Blankets", Description = "Blankets and bedding materials." },
                new ResourceCategory { Id = CleaningSuppliesCategoryId, Name = "Cleaning Supplies", Description = "Cleaning and sanitation products." },
                new ResourceCategory { Id = AccessoriesCategoryId, Name = "Accessories", Description = "Leashes, collars, bowls, and similar items." },
                new ResourceCategory { Id = OtherCategoryId, Name = "Other", Description = "General shelter resources." });
        }

        if (!await context.FoodTypes.AnyAsync())
        {
            context.FoodTypes.AddRange(
                new FoodType { Id = AdultDryFoodTypeId, Name = "Adult dry food", Description = "Standard dry food for adult dogs." },
                new FoodType { Id = PuppyFoodTypeId, Name = "Puppy food", Description = "Food suitable for puppies." },
                new FoodType { Id = SeniorFoodTypeId, Name = "Senior food", Description = "Food suitable for older dogs." },
                new FoodType { Id = WetFoodTypeId, Name = "Wet food", Description = "Canned or wet dog food." },
                new FoodType { Id = MedicalDietFoodTypeId, Name = "Medical diet food", Description = "Special diet food recommended by a veterinarian." });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedDomainDataAsync(ApplicationDbContext context)
    {
        if (await context.Shelters.AnyAsync())
        {
            await UpdateExistingDemoDataAsync(context);
            return;
        }

        var shelter = new Shelter
        {
            Name = "PawConnect Demo Shelter",
            Description = "Development shelter used for demo data and dashboard testing.",
            Address = "123 Shelter Street",
            City = "Bucharest",
            PhoneNumber = "+40 700 000 001",
            Email = "u8878233525@id.gle",
            ApplicationUserId = ShelterUserId
        };

        shelter.Dogs =
        [
            new Dog
            {
                Name = "Max",
                Breed = "Mixed Breed",
                Age = 3,
                AgeYears = 3,
                Size = DogSize.Medium,
                Location = "Bucharest",
                Status = DogStatus.Available,
                PreferredFoodTypeId = AdultDryFoodTypeId,
                DailyFoodAmountGrams = 350,
                Description = "Friendly and playful dog looking for an active family.",
                BehaviorDescription = "Energetic, social, and good on walks.",
                MedicalStatus = "Vaccinated and dewormed.",
                Images = [new DogImage { ImageUrl = "https://placedog.net/800/500?id=11", IsMainImage = true }],
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
                AgeYears = 5,
                Size = DogSize.Large,
                Location = "Bucharest",
                Status = DogStatus.Reserved,
                PreferredFoodTypeId = SeniorFoodTypeId,
                DailyFoodAmountGrams = 420,
                Description = "Calm, affectionate, and good with people.",
                BehaviorDescription = "Gentle and patient.",
                MedicalStatus = "Healthy.",
                Images = [new DogImage { ImageUrl = "https://placedog.net/800/500?id=22", IsMainImage = true }]
            },
            new Dog
            {
                Name = "Luna",
                Breed = "Terrier Mix",
                Age = 1,
                AgeYears = 1,
                Size = DogSize.Small,
                Location = "Ilfov",
                Status = DogStatus.InTreatment,
                PreferredFoodTypeId = MedicalDietFoodTypeId,
                DailyFoodAmountGrams = 180,
                Description = "Young dog currently receiving basic medical care.",
                BehaviorDescription = "Curious but shy around new people.",
                MedicalStatus = "Under treatment for a minor skin condition.",
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
                AgeYears = 4,
                Size = DogSize.Large,
                Location = "Brasov",
                Status = DogStatus.Available,
                PreferredFoodTypeId = AdultDryFoodTypeId,
                DailyFoodAmountGrams = 500,
                Description = "Loyal dog suitable for an experienced adopter.",
                BehaviorDescription = "Protective, smart, and active.",
                MedicalStatus = "Vaccinated.",
                Images = [new DogImage { ImageUrl = "https://placedog.net/800/500?id=33", IsMainImage = true }]
            },
            new Dog
            {
                Name = "Nala",
                Breed = "Border Collie Mix",
                Age = 2,
                AgeYears = 2,
                Size = DogSize.Medium,
                Location = "Sibiu",
                Status = DogStatus.Available,
                PreferredFoodTypeId = AdultDryFoodTypeId,
                DailyFoodAmountGrams = 340,
                Description = "Bright and affectionate dog who enjoys walks, training, and gentle play.",
                BehaviorDescription = "Smart, attentive, and eager to bond with people.",
                MedicalStatus = "Vaccinated and healthy.",
                Images = [new DogImage { ImageUrl = "https://placedog.net/800/500?id=66", IsMainImage = true }]
            },
            new Dog
            {
                Name = "Milo",
                Breed = "Beagle Mix",
                Age = 2,
                AgeYears = 2,
                Size = DogSize.Medium,
                Location = "Cluj-Napoca",
                Status = DogStatus.Adopted,
                PreferredFoodTypeId = WetFoodTypeId,
                DailyFoodAmountGrams = 300,
                Description = "Cheerful dog used as an adopted example in demo data.",
                BehaviorDescription = "Playful and food motivated.",
                MedicalStatus = "Healthy.",
                AdoptedAt = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                SuccessStoryText = "Milo found a patient family who loves long walks and training games.",
                Images = [new DogImage { ImageUrl = "https://placedog.net/800/500?id=44", IsMainImage = true }]
            }
        ];

        shelter.ResourceStocks =
        [
            new ResourceStock { Name = "Adult Dry Food Bags", Quantity = 50, Unit = "kg", LowStockThreshold = 15, ResourceCategoryId = FoodCategoryId, FoodTypeId = AdultDryFoodTypeId },
            new ResourceStock { Name = "Medicine Kits", Quantity = 2, Unit = "pcs", LowStockThreshold = 3, ResourceCategoryId = MedicineCategoryId },
            new ResourceStock { Name = "Blankets", Quantity = 20, Unit = "pcs", LowStockThreshold = 5, ResourceCategoryId = BlanketsCategoryId },
            new ResourceStock { Name = "Disinfectant", Quantity = 8, Unit = "liters", LowStockThreshold = 4, ResourceCategoryId = CleaningSuppliesCategoryId }
        ];

        context.Shelters.Add(shelter);
        await context.SaveChangesAsync();
    }

    private static async Task UpdateExistingDemoDataAsync(ApplicationDbContext context)
    {
        var stocks = await context.ResourceStocks.ToListAsync();
        foreach (var stock in stocks)
        {
            if (stock.ResourceCategoryId != 0)
            {
                continue;
            }

            if (stock.Name.Contains("food", StringComparison.OrdinalIgnoreCase))
            {
                stock.Name = "Adult Dry Food Bags";
                stock.ResourceCategoryId = FoodCategoryId;
                stock.FoodTypeId = AdultDryFoodTypeId;
            }
            else if (stock.Name.Contains("medicine", StringComparison.OrdinalIgnoreCase))
            {
                stock.ResourceCategoryId = MedicineCategoryId;
            }
            else if (stock.Name.Contains("blanket", StringComparison.OrdinalIgnoreCase))
            {
                stock.ResourceCategoryId = BlanketsCategoryId;
            }
            else
            {
                stock.ResourceCategoryId = OtherCategoryId;
            }
        }

        var shelter = await context.Shelters.FirstOrDefaultAsync();
        if (shelter is not null && !await context.ResourceStocks.AnyAsync(r => r.ResourceCategoryId == CleaningSuppliesCategoryId))
        {
            context.ResourceStocks.Add(new ResourceStock
            {
                ShelterId = shelter.Id,
                Name = "Disinfectant",
                Quantity = 8,
                Unit = "liters",
                LowStockThreshold = 4,
                ResourceCategoryId = CleaningSuppliesCategoryId
            });
        }

        var dogs = await context.Dogs.ToListAsync();
        foreach (var dog in dogs)
        {
            if (dog.AgeYears == 0 && dog.AgeMonths == 0 && dog.Age > 0)
            {
                dog.AgeYears = dog.Age;
            }

            await UpdateDemoDogImagesAsync(context, dog);

            if (dog.PreferredFoodTypeId is not null)
            {
                continue;
            }

            dog.PreferredFoodTypeId = dog.AgeYears switch
            {
                <= 1 => PuppyFoodTypeId,
                >= 5 => SeniorFoodTypeId,
                _ => AdultDryFoodTypeId
            };
            dog.DailyFoodAmountGrams = dog.Size switch
            {
                DogSize.Small => 180,
                DogSize.Medium => 320,
                DogSize.Large => 480,
                _ => 300
            };
        }

        if (shelter is not null)
        {
            var milo = dogs.FirstOrDefault(d => d.Name == "Milo");
            if (milo is not null)
            {
                milo.Status = DogStatus.Adopted;
                milo.AdoptedAt ??= new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
                milo.SuccessStoryText ??= "Milo found a patient family who loves long walks and training games.";
            }

            if (!await context.Dogs.AnyAsync(d => d.Name == "Daisy"))
            {
                context.Dogs.Add(new Dog
                {
                    ShelterId = shelter.Id,
                    Name = "Daisy",
                    Breed = "Golden Retriever Mix",
                    Age = 3,
                    AgeYears = 3,
                    Size = DogSize.Medium,
                    Location = "Bucharest",
                    Status = DogStatus.Adopted,
                    PreferredFoodTypeId = AdultDryFoodTypeId,
                    DailyFoodAmountGrams = 360,
                    Description = "Gentle and social dog who quickly became a favorite with volunteers.",
                    BehaviorDescription = "Friendly, calm, and comfortable around children.",
                    MedicalStatus = "Vaccinated and healthy.",
                    AdoptedAt = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                    SuccessStoryText = "Daisy was adopted by a family who first met her through PawConnect and followed up with the shelter the same week.",
                    Images = [new DogImage { ImageUrl = "https://placedog.net/800/500?id=55", IsMainImage = true }]
                });
            }

            if (!await context.Dogs.AnyAsync(d => d.Name == "Nala"))
            {
                context.Dogs.Add(new Dog
                {
                    ShelterId = shelter.Id,
                    Name = "Nala",
                    Breed = "Border Collie Mix",
                    Age = 2,
                    AgeYears = 2,
                    Size = DogSize.Medium,
                    Location = "Sibiu",
                    Status = DogStatus.Available,
                    PreferredFoodTypeId = AdultDryFoodTypeId,
                    DailyFoodAmountGrams = 340,
                    Description = "Bright and affectionate dog who enjoys walks, training, and gentle play.",
                    BehaviorDescription = "Smart, attentive, and eager to bond with people.",
                    MedicalStatus = "Vaccinated and healthy.",
                    Images = [new DogImage { ImageUrl = "https://placedog.net/800/500?id=66", IsMainImage = true }]
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task UpdateDemoDogImagesAsync(ApplicationDbContext context, Dog dog)
    {
        var imageUrl = dog.Name switch
        {
            "Max" => "https://placedog.net/800/500?id=11",
            "Bella" => "https://placedog.net/800/500?id=22",
            "Rocky" => "https://placedog.net/800/500?id=33",
            "Milo" => "https://placedog.net/800/500?id=44",
            "Nala" => "https://placedog.net/800/500?id=66",
            _ => null
        };

        if (imageUrl is null)
        {
            return;
        }

        var images = await context.DogImages.Where(i => i.DogId == dog.Id).ToListAsync();
        var mainImage = images.FirstOrDefault(i => i.IsMainImage) ?? images.FirstOrDefault();
        if (mainImage is null)
        {
            context.DogImages.Add(new DogImage
            {
                DogId = dog.Id,
                ImageUrl = imageUrl,
                IsMainImage = true
            });
            return;
        }

        if (mainImage.ImageUrl.Contains("placehold.co", StringComparison.OrdinalIgnoreCase))
        {
            mainImage.ImageUrl = imageUrl;
            mainImage.IsMainImage = true;
        }
    }
}
