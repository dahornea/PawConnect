using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PawConnect.Entities;
using PawConnect.Services;

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

    private static readonly Dictionary<string, string> DemoDogMainImageUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bruno"] = "https://www.thelabradorsite.com/wp-content/uploads/2023/06/sheprador-buddy.jpg",
        ["Daisy"] = "https://www.westminsterkennelclub.org/wp-content/uploads/2025/07/golden_retriever-scaled-1-1024x681.jpg",
        ["Lili"] = "https://cdn.britannica.com/80/232780-050-404D6708/Pembroke-welsh-corgi-dog.jpg",
        ["Luna"] = "https://www.zooplus.ro/ghid/wp-content/uploads/2025/01/airedale-terrier.webp",
        ["Milo"] = "https://bestforpet.co.nz/wp-content/uploads/2025/11/Beagle.jpg",
        ["Mira"] = "https://jesypet.ro/wp-content/uploads/2025/08/Bichon-Maltez-%E2%80%93-Ghid-complet-despre-ca%CC%82inele-mic-cu-inima%CC%86-mare.webp",
        ["Oscar"] = "https://img.fera.ro/images/companies/1/seter-szkocki-fci.png?1704310402663",
        ["Rex"] = "https://www.purina.com/sites/default/files/styles/social_share/public/2025-09/siberian_husky_4_1.jpg?h=f7d9296c&itok=medyY_xK"
    };

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
        await NormalizeDogBreedLookupsAsync(context);
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

        foreach (var seedBreed in DogBreedSeedData.CreateSeedEntities())
        {
            var existing = await context.DogBreeds.FirstOrDefaultAsync(breed => breed.Id == seedBreed.Id);
            if (existing is null)
            {
                context.DogBreeds.Add(seedBreed);
            }
            else
            {
                existing.Name = seedBreed.Name;
                existing.IsActive = true;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task NormalizeDogBreedLookupsAsync(ApplicationDbContext context)
    {
        var breeds = await context.DogBreeds.AsNoTracking().ToListAsync();
        if (breeds.Count == 0)
        {
            return;
        }

        var dogs = await context.Dogs.ToListAsync();
        foreach (var dog in dogs)
        {
            var parsed = DogBreedFormatter.Parse(dog.Breed, breeds);
            dog.DogBreedId = parsed.DogBreedId;
            dog.SecondaryBreedId = parsed.SecondaryBreedId;
            dog.IsMixedBreed = parsed.IsMixedBreed;
            dog.CustomBreedName = parsed.CustomBreedName;
            dog.Breed = parsed.DisplayName;
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
            Name = "Happy Paws Shelter",
            Description = "Fictional demo shelter used for PawConnect development and presentation data.",
            Address = "Strada Observatorului 12",
            City = "Cluj-Napoca",
            Neighborhood = "Zorilor",
            PhoneNumber = "+40 700 000 001",
            Email = "u8878233525@id.gle",
            Latitude = 46.7556,
            Longitude = 23.5804,
            VisitStartTime = new TimeSpan(10, 0, 0),
            VisitEndTime = new TimeSpan(17, 0, 0),
            VisitsAllowedMonday = true,
            VisitsAllowedTuesday = true,
            VisitsAllowedWednesday = true,
            VisitsAllowedThursday = true,
            VisitsAllowedFriday = true,
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
                Location = "Cluj-Napoca",
                Status = DogStatus.Available,
                PreferredFoodTypeId = AdultDryFoodTypeId,
                DailyFoodAmountGrams = 350,
                Description = "Max is a lively dog who enjoys longer walks and games that let him use his energy. He likes exploring open areas and would do best with an adopter who enjoys regular outdoor time. After activity, he settles well with people he knows, especially when his day has included a clear routine.",
                BehaviorDescription = "Playful and social with familiar volunteers. He responds well to praise, training games, and structured walks. Fast-moving cats or smaller animals can hold his attention too much.",
                MedicalStatus = "Vaccinated and dewormed.",
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
                Size = DogSize.Medium,
                Location = "Cluj-Napoca",
                Status = DogStatus.Reserved,
                PreferredFoodTypeId = SeniorFoodTypeId,
                DailyFoodAmountGrams = 320,
                Description = "Bella enjoys slow walks and settles down quickly after exploring. She likes staying close to people without demanding constant activity. A predictable routine and relaxed evenings suit her well, even in a smaller home.",
                BehaviorDescription = "Gentle and patient during handling. She is friendly with familiar people and prefers quiet routines with soft attention. Calm dogs are easier for her than pushy playmates.",
                MedicalStatus = "Healthy."
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
                Description = "Luna is a young dog currently receiving basic medical care. She can be cautious in new places but relaxes when routines are predictable. Her profile is kept in the demo data to test that dogs in treatment stay out of public adopter searches.",
                BehaviorDescription = "Curious but shy around new people. She needs calm introductions and patient handling.",
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
                Location = "Cluj-Napoca",
                Status = DogStatus.Available,
                PreferredFoodTypeId = AdultDryFoodTypeId,
                DailyFoodAmountGrams = 500,
                Description = "Rocky enjoys training games, brisk walks, and chances to stretch his legs outside. He would benefit from space to run and an adopter who likes working with smart, energetic dogs. He bonds strongly once he understands the routine, but a very quiet flat would not be his best match.",
                BehaviorDescription = "Alert, clever, and motivated by structured activity. He is better suited to an experienced adopter who can offer consistent outdoor play. He can be too intense for shy dogs or very noisy young children.",
                MedicalStatus = "Vaccinated."
            },
            new Dog
            {
                Name = "Nala",
                Breed = "Border Collie Mix",
                Age = 2,
                AgeYears = 2,
                Size = DogSize.Medium,
                Location = "Cluj-Napoca",
                Status = DogStatus.Available,
                PreferredFoodTypeId = AdultDryFoodTypeId,
                DailyFoodAmountGrams = 340,
                Description = "Nala enjoys short daily walks, gentle play, and indoor rest after she has had time with people. She approaches visitors with curiosity and a wagging tail, then settles close by. Her medium size and steady routine make her easy to imagine in a quieter home or a calm family setting.",
                BehaviorDescription = "Friendly and attentive around people. She has done well around older children during supervised visits and responds well to positive handling.",
                MedicalStatus = "Vaccinated and healthy."
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
                Description = "Milo is a cheerful dog used as an adopted example in demo data. He enjoys puzzle toys and food games, but he should no longer appear in adopter-facing search results. His record helps verify that adopted dogs remain hidden from Copilot suggestions.",
                BehaviorDescription = "Playful and food motivated with people he knows.",
                MedicalStatus = "Healthy.",
                AdoptedAt = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                SuccessStoryText = "Milo found a patient family who loves long walks and training games."
            }
        ];

        shelter.ResourceStocks =
        [
            new ResourceStock { Name = "Adult Dry Food Bags", Quantity = 50, Unit = "kg", LowStockThreshold = 15, ResourceCategoryId = FoodCategoryId, FoodTypeId = AdultDryFoodTypeId },
            new ResourceStock { Name = "Medicine Kits", Quantity = 2, Unit = "pcs", LowStockThreshold = 3, ResourceCategoryId = MedicineCategoryId },
            new ResourceStock { Name = "Blankets", Quantity = 20, Unit = "pcs", LowStockThreshold = 5, ResourceCategoryId = BlanketsCategoryId },
            new ResourceStock { Name = "Disinfectant", Quantity = 8, Unit = "liters", LowStockThreshold = 4, ResourceCategoryId = CleaningSuppliesCategoryId }
        ];

        context.Shelters.AddRange(
            shelter,
            new Shelter
            {
                Name = "Hope Tails Rescue",
                Description = "Fictional demo rescue profile for map and listing demonstrations.",
                Address = "Strada Fabricii 45",
                City = "Cluj-Napoca",
                Neighborhood = "Marasti",
                PhoneNumber = "+40 700 000 002",
                Email = "hope-tails@example.test",
                Latitude = 46.7842,
                Longitude = 23.6157,
                VisitStartTime = new TimeSpan(10, 0, 0),
                VisitEndTime = new TimeSpan(17, 0, 0),
                VisitsAllowedMonday = true,
                VisitsAllowedTuesday = true,
                VisitsAllowedWednesday = true,
                VisitsAllowedThursday = true,
                VisitsAllowedFriday = true
            },
            new Shelter
            {
                Name = "Safe Haven Dogs",
                Description = "Fictional demo shelter profile with an approximate Cluj-Napoca marker.",
                Address = "Strada Buna Ziua 22",
                City = "Cluj-Napoca",
                Neighborhood = "Buna Ziua",
                PhoneNumber = "+40 700 000 003",
                Email = "safe-haven@example.test",
                Latitude = 46.7509,
                Longitude = 23.6022,
                VisitStartTime = new TimeSpan(10, 0, 0),
                VisitEndTime = new TimeSpan(17, 0, 0),
                VisitsAllowedMonday = true,
                VisitsAllowedTuesday = true,
                VisitsAllowedWednesday = true,
                VisitsAllowedThursday = true,
                VisitsAllowedFriday = true
            },
            new Shelter
            {
                Name = "Green Yard Shelter",
                Description = "Fictional demo shelter profile for public shelter map testing.",
                Address = "Strada Donath 60",
                City = "Cluj-Napoca",
                Neighborhood = "Grigorescu",
                PhoneNumber = "+40 700 000 004",
                Email = "green-yard@example.test",
                Latitude = 46.7719,
                Longitude = 23.5458,
                VisitStartTime = new TimeSpan(10, 0, 0),
                VisitEndTime = new TimeSpan(17, 0, 0),
                VisitsAllowedMonday = true,
                VisitsAllowedTuesday = true,
                VisitsAllowedWednesday = true,
                VisitsAllowedThursday = true,
                VisitsAllowedFriday = true
            });
        await EnsureAdditionalDemoDogsAsync(context);
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
        if (shelter is not null)
        {
            shelter.Name = "Happy Paws Shelter";
            shelter.Description = "Fictional demo shelter used for PawConnect development and presentation data.";
            shelter.Address = "Strada Observatorului 12";
            shelter.City = "Cluj-Napoca";
            shelter.Neighborhood = "Zorilor";
            shelter.PhoneNumber = "+40 700 000 001";
            shelter.Email = "u8878233525@id.gle";
            shelter.Latitude = 46.7556;
            shelter.Longitude = 23.5804;
            ApplyDefaultVisitingHours(shelter);

            await EnsureDemoShelterAsync(
                context,
                "Hope Tails Rescue",
                "Fictional demo rescue profile for map and listing demonstrations.",
                "Strada Fabricii 45",
                "Marasti",
                "+40 700 000 002",
                "hope-tails@example.test",
                46.7842,
                23.6157);

            await EnsureDemoShelterAsync(
                context,
                "Safe Haven Dogs",
                "Fictional demo shelter profile with an approximate Cluj-Napoca marker.",
                "Strada Buna Ziua 22",
                "Buna Ziua",
                "+40 700 000 003",
                "safe-haven@example.test",
                46.7509,
                23.6022);

            await EnsureDemoShelterAsync(
                context,
                "Green Yard Shelter",
                "Fictional demo shelter profile for public shelter map testing.",
                "Strada Donath 60",
                "Grigorescu",
                "+40 700 000 004",
                "green-yard@example.test",
                46.7719,
                23.5458);
        }

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
            ApplyDemoDogSearchText(dog);

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
                    CoatColor = "Golden",
                    Age = 3,
                    AgeYears = 3,
                    Size = DogSize.Medium,
                    Location = "Bucharest",
                    Status = DogStatus.Adopted,
                    PreferredFoodTypeId = AdultDryFoodTypeId,
                    DailyFoodAmountGrams = 360,
                    Description = "Daisy quickly became a favorite with volunteers because she enjoys gentle attention and predictable routines. She has done well around older children during supervised visits. Her adopted record helps keep success stories visible without affecting public search results.",
                    BehaviorDescription = "Friendly and relaxed with familiar people. She is comfortable around children who approach calmly.",
                    MedicalStatus = "Vaccinated and healthy.",
                    AdoptedAt = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                    SuccessStoryText = "Daisy was adopted by a family who first met her through PawConnect and followed up with the shelter the same week."
                });
            }

            if (!await context.Dogs.AnyAsync(d => d.Name == "Nala"))
            {
                context.Dogs.Add(new Dog
                {
                    ShelterId = shelter.Id,
                    Name = "Nala",
                    Breed = "Border Collie Mix",
                    CoatColor = "Brown and white",
                    Age = 2,
                    AgeYears = 2,
                    Size = DogSize.Medium,
                    Location = "Cluj-Napoca",
                    Status = DogStatus.Available,
                    PreferredFoodTypeId = AdultDryFoodTypeId,
                    DailyFoodAmountGrams = 340,
                    Description = "Nala enjoys short daily walks, gentle play, and indoor rest after she has had time with people. She approaches visitors with curiosity and a wagging tail, then settles close by. Her medium size and steady routine make her easy to imagine in a quieter home or a calm family setting.",
                    BehaviorDescription = "Friendly and attentive around people. She has done well around older children during supervised visits and responds well to positive handling.",
                    MedicalStatus = "Vaccinated and healthy."
                });
            }

            await EnsureAdditionalDemoDogsAsync(context);
        }

        await context.SaveChangesAsync();
    }

    private static void ApplyDemoDogSearchText(Dog dog)
    {
        switch (dog.Name)
        {
            case "Max":
                dog.Size = DogSize.Medium;
                dog.CoatColor = "Black and tan";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Max is a lively dog who enjoys longer walks and games that let him use his energy. He likes exploring open areas and would do best with an adopter who enjoys regular outdoor time. After activity, he settles well with people he knows, especially when his day has included a clear routine.";
                dog.BehaviorDescription = "Playful and social with familiar volunteers. He responds well to praise, training games, and structured walks. Fast-moving cats or smaller animals can hold his attention too much.";
                break;
            case "Bella":
                dog.Size = DogSize.Medium;
                dog.CoatColor = "Golden";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Bella enjoys slow walks and settles down quickly after exploring. She likes staying close to people without demanding constant activity. A predictable routine and relaxed evenings suit her well, even in a smaller home.";
                dog.BehaviorDescription = "Gentle and patient during handling. She is friendly with familiar people and prefers quiet routines with soft attention. Calm dogs are easier for her than pushy playmates.";
                break;
            case "Rocky":
                dog.Size = DogSize.Large;
                dog.CoatColor = "Black and tan";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Rocky enjoys training games, brisk walks, and chances to stretch his legs outside. He would benefit from space to run and an adopter who likes working with smart, energetic dogs. He bonds strongly once he understands the routine, but a very quiet flat would not be his best match.";
                dog.BehaviorDescription = "Alert, clever, and motivated by structured activity. He is better suited to an experienced adopter who can offer consistent outdoor play. He can be too intense for shy dogs or very noisy young children.";
                break;
            case "Nala":
                dog.Size = DogSize.Medium;
                dog.CoatColor = "Brown and white";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Nala enjoys short daily walks, gentle play, and indoor rest after she has had time with people. She approaches visitors with curiosity and a wagging tail, then settles close by. Her medium size and steady routine make her easy to imagine in a quieter home or a calm family setting.";
                dog.BehaviorDescription = "Friendly and attentive around people. She has done well around older children during supervised visits and responds well to positive handling.";
                break;
            case "Buddy":
                dog.Size = DogSize.Large;
                dog.CoatColor = "Golden";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Buddy is a cheerful Labrador-type dog who enjoys people, fetch, and structured walks. He is included with two demo photos so the Dog Details gallery and lightbox can be tested. His energy makes him a better fit for adopters who can offer regular activity.";
                dog.BehaviorDescription = "Friendly with familiar volunteers and responsive to praise. He may be more energetic than a quiet apartment adopter expects.";
                break;
            case "Luna":
                dog.CoatColor = "Brown and white";
                dog.Description = "Luna is a young dog currently receiving basic medical care. She can be cautious in new places but relaxes when routines are predictable. Her profile is kept in the demo data to test that dogs in treatment stay out of public adopter searches.";
                dog.BehaviorDescription = "Curious but shy around new people. She needs calm introductions and patient handling.";
                break;
            case "Milo":
                dog.CoatColor = "Brown and white";
                dog.Description = "Milo is a cheerful dog used as an adopted example in demo data. He enjoys puzzle toys and food games, but he should no longer appear in adopter-facing search results. His record helps verify that adopted dogs remain hidden from Copilot suggestions.";
                dog.BehaviorDescription = "Playful and food motivated with people he knows.";
                break;
            case "Toby":
                dog.Size = DogSize.Small;
                dog.CoatColor = "White";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Toby is a small dog from Buna Ziua who likes leash walks and quiet indoor rest. He enjoys gentle interaction but needs a little time before fully relaxing with new people. He may suit someone looking for a softer companion outside the busiest parts of the city.";
                dog.BehaviorDescription = "Friendly once introduced slowly. He is more comfortable with calm dogs than very energetic ones.";
                break;
            case "Mira":
                dog.Size = DogSize.Small;
                dog.CoatColor = "White";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Mira is a small dog from Marasti who enjoys short neighborhood walks and quiet evenings indoors. During feeding time she has passed the shelter cats calmly, then returned her attention to the handler. She settles near familiar people when the daily rhythm is predictable.";
                dog.BehaviorDescription = "Gentle handling suits her well. She walks politely beside familiar calm dogs and takes guidance easily.";
                break;
            case "Bruno":
                dog.Size = DogSize.Large;
                dog.CoatColor = "Brown";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Bruno likes longer walks, fetch, and training games that give him a job to do. He would benefit from regular outdoor play and enough room to run before settling. Fast-moving small animals hold his attention too much for homes with cats.";
                dog.BehaviorDescription = "He enjoys sturdy, playful dogs after a proper introduction. Noisy, chaotic play with very young children may be too much for him.";
                break;
            case "Sasha":
                dog.Size = DogSize.Medium;
                dog.CoatColor = "Brown";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Sasha watches new people carefully before approaching, then relaxes when the routine becomes familiar. She notices cats and smaller animals but can be redirected with treats and a calm voice. A patient adopter would see more of her playful side over time.";
                dog.BehaviorDescription = "She prefers steady dogs over bouncy playmates and needs slow introductions. Quiet encouragement works better than pressure.";
                break;
            case "Lili":
                dog.Size = DogSize.Small;
                dog.CoatColor = "Tricolor";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Lili enjoys short walks, soft praise, and resting close to people in the evening. She has stayed relaxed during supervised visits with older children when playtime was guided. Her predictable habits make her a useful demo match for smaller homes.";
                dog.BehaviorDescription = "She takes treats gently and responds well to routine. Pushy dogs can make her retreat, so introductions should stay calm.";
                break;
            case "Rex":
                dog.Size = DogSize.Large;
                dog.CoatColor = "White";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Rex is happiest when he has a chance to move, sniff, and work through training games. He needs regular outdoor play and space to run before he can fully settle. Quick cats or small animals are likely too exciting for him.";
                dog.BehaviorDescription = "He can overwhelm shy dogs and does best with confident handling. He is a weaker fit for quiet flats or very low-activity homes.";
                break;
            case "Oscar":
                dog.Size = DogSize.Medium;
                dog.CoatColor = "Tricolor";
                dog.Location = "Cluj-Napoca";
                dog.Description = "Oscar greets familiar volunteers with a loose body and a wagging tail. He enjoys play sessions with steady dogs and settles after a medium walk. He could fit an adopter who wants a sociable companion without extreme activity needs.";
                dog.BehaviorDescription = "He likes playful dogs that respect pauses. He is easy to redirect with praise and simple cues.";
                break;
        }
    }

    private static async Task EnsureAdditionalDemoDogsAsync(ApplicationDbContext context)
    {
        await EnsureDemoDogAsync(context, "Safe Haven Dogs", new Dog
        {
            Name = "Toby",
            Breed = "Poodle Mix",
            Age = 2,
            AgeYears = 2,
            Size = DogSize.Small,
            Location = "Cluj-Napoca",
            Status = DogStatus.Available,
            PreferredFoodTypeId = AdultDryFoodTypeId,
            DailyFoodAmountGrams = 190,
            MedicalStatus = "Vaccinated and healthy."
        });

        await EnsureDemoDogAsync(context, "Happy Paws Shelter", new Dog
        {
            Name = "Buddy",
            Breed = "Labrador Mix",
            Age = 2,
            AgeYears = 2,
            AgeMonths = 6,
            Size = DogSize.Large,
            Location = "Cluj-Napoca",
            Status = DogStatus.Reserved,
            PreferredFoodTypeId = AdultDryFoodTypeId,
            DailyFoodAmountGrams = 480,
            MedicalStatus = "Vaccinated and healthy."
        });

        await EnsureDemoDogAsync(context, "Hope Tails Rescue", new Dog
        {
            Name = "Mira",
            Breed = "Bichon Mix",
            Age = 3,
            AgeYears = 3,
            Size = DogSize.Small,
            Location = "Cluj-Napoca",
            Status = DogStatus.Available,
            PreferredFoodTypeId = AdultDryFoodTypeId,
            DailyFoodAmountGrams = 180,
            MedicalStatus = "Vaccinated and healthy."
        });

        await EnsureDemoDogAsync(context, "Green Yard Shelter", new Dog
        {
            Name = "Bruno",
            Breed = "Labrador Shepherd Mix",
            Age = 4,
            AgeYears = 4,
            Size = DogSize.Large,
            Location = "Cluj-Napoca",
            Status = DogStatus.Available,
            PreferredFoodTypeId = AdultDryFoodTypeId,
            DailyFoodAmountGrams = 520,
            MedicalStatus = "Vaccinated and healthy."
        });

        await EnsureDemoDogAsync(context, "Hope Tails Rescue", new Dog
        {
            Name = "Sasha",
            Breed = "Spaniel Mix",
            Age = 2,
            AgeYears = 2,
            Size = DogSize.Medium,
            Location = "Cluj-Napoca",
            Status = DogStatus.Reserved,
            PreferredFoodTypeId = AdultDryFoodTypeId,
            DailyFoodAmountGrams = 280,
            MedicalStatus = "Vaccinated and healthy."
        });

        await EnsureDemoDogAsync(context, "Safe Haven Dogs", new Dog
        {
            Name = "Lili",
            Breed = "Corgi Mix",
            Age = 5,
            AgeYears = 5,
            Size = DogSize.Small,
            Location = "Cluj-Napoca",
            Status = DogStatus.Available,
            PreferredFoodTypeId = SeniorFoodTypeId,
            DailyFoodAmountGrams = 220,
            MedicalStatus = "Vaccinated and healthy."
        });

        await EnsureDemoDogAsync(context, "Green Yard Shelter", new Dog
        {
            Name = "Rex",
            Breed = "Husky Mix",
            Age = 3,
            AgeYears = 3,
            Size = DogSize.Large,
            Location = "Cluj-Napoca",
            Status = DogStatus.Available,
            PreferredFoodTypeId = AdultDryFoodTypeId,
            DailyFoodAmountGrams = 540,
            MedicalStatus = "Vaccinated and healthy."
        });

        await EnsureDemoDogAsync(context, "Hope Tails Rescue", new Dog
        {
            Name = "Oscar",
            Breed = "Setter Mix",
            Age = 2,
            AgeYears = 2,
            Size = DogSize.Medium,
            Location = "Cluj-Napoca",
            Status = DogStatus.Available,
            PreferredFoodTypeId = AdultDryFoodTypeId,
            DailyFoodAmountGrams = 310,
            MedicalStatus = "Vaccinated and healthy."
        });
    }

    private static async Task EnsureDemoDogAsync(ApplicationDbContext context, string shelterName, Dog demoDog)
    {
        var shelter = context.Shelters.Local.FirstOrDefault(s => s.Name == shelterName) ??
            await context.Shelters.FirstOrDefaultAsync(s => s.Name == shelterName);

        if (shelter is null)
        {
            return;
        }

        var existingDog = await context.Dogs.FirstOrDefaultAsync(d => d.Name == demoDog.Name);
        if (existingDog is null)
        {
            demoDog.Shelter = shelter;
            ApplyDemoDogSearchText(demoDog);
            AddConfiguredDemoMainImage(demoDog);
            context.Dogs.Add(demoDog);
            return;
        }

        existingDog.Shelter = shelter;
        existingDog.ShelterId = shelter.Id;
        existingDog.Breed = demoDog.Breed;
        existingDog.Age = demoDog.Age;
        existingDog.AgeYears = demoDog.AgeYears;
        existingDog.AgeMonths = demoDog.AgeMonths;
        existingDog.Size = demoDog.Size;
        existingDog.Location = demoDog.Location;
        existingDog.Status = demoDog.Status;
        existingDog.PreferredFoodTypeId = demoDog.PreferredFoodTypeId;
        existingDog.DailyFoodAmountGrams = demoDog.DailyFoodAmountGrams;
        existingDog.MedicalStatus = demoDog.MedicalStatus;
        ApplyDemoDogSearchText(existingDog);
        await UpdateDemoDogImagesAsync(context, existingDog);
    }

    private static async Task EnsureDemoShelterAsync(
        ApplicationDbContext context,
        string name,
        string description,
        string address,
        string neighborhood,
        string phoneNumber,
        string email,
        double latitude,
        double longitude)
    {
        var shelter = await context.Shelters.FirstOrDefaultAsync(s => s.Name == name);
        if (shelter is null)
        {
            shelter = new Shelter
            {
                Name = name,
                Description = description,
                Address = address,
                City = "Cluj-Napoca",
                Neighborhood = neighborhood,
                PhoneNumber = phoneNumber,
                Email = email,
                Latitude = latitude,
                Longitude = longitude
            };
            ApplyDefaultVisitingHours(shelter);
            context.Shelters.Add(shelter);
            return;
        }

        shelter.Description = description;
        shelter.Address = address;
        shelter.City = "Cluj-Napoca";
        shelter.Neighborhood = neighborhood;
        shelter.PhoneNumber = phoneNumber;
        shelter.Email = email;
        shelter.Latitude = latitude;
        shelter.Longitude = longitude;
        ApplyDefaultVisitingHours(shelter);
    }

    private static void ApplyDefaultVisitingHours(Shelter shelter)
    {
        shelter.VisitStartTime ??= new TimeSpan(10, 0, 0);
        shelter.VisitEndTime ??= new TimeSpan(17, 0, 0);

        if (shelter.VisitsAllowedMonday ||
            shelter.VisitsAllowedTuesday ||
            shelter.VisitsAllowedWednesday ||
            shelter.VisitsAllowedThursday ||
            shelter.VisitsAllowedFriday ||
            shelter.VisitsAllowedSaturday ||
            shelter.VisitsAllowedSunday)
        {
            return;
        }

        shelter.VisitsAllowedMonday = true;
        shelter.VisitsAllowedTuesday = true;
        shelter.VisitsAllowedWednesday = true;
        shelter.VisitsAllowedThursday = true;
        shelter.VisitsAllowedFriday = true;
    }

    private static async Task UpdateDemoDogImagesAsync(ApplicationDbContext context, Dog dog)
    {
        var images = await context.DogImages.Where(i => i.DogId == dog.Id).ToListAsync();
        foreach (var image in images.Where(image => IsInvalidDemoImage(image) || IsLocalDemoDogImage(image.ImageUrl)).ToList())
        {
            context.DogImages.Remove(image);
            images.Remove(image);
        }

        foreach (var duplicate in FindDuplicateDemoImages(images))
        {
            context.DogImages.Remove(duplicate);
            images.Remove(duplicate);
        }

        if (TryGetConfiguredDemoMainImageUrl(dog.Name, out var configuredMainImageUrl))
        {
            var configuredImage = images.FirstOrDefault(image =>
                string.Equals(image.ImageUrl?.Trim(), configuredMainImageUrl, StringComparison.OrdinalIgnoreCase));
            if (configuredImage is null)
            {
                configuredImage = new DogImage
                {
                    DogId = dog.Id,
                    ImageUrl = configuredMainImageUrl
                };
                context.DogImages.Add(configuredImage);
                images.Add(configuredImage);
            }
            else
            {
                configuredImage.ImageUrl = configuredMainImageUrl;
            }

            foreach (var image in images)
            {
                image.IsMainImage = ReferenceEquals(image, configuredImage);
            }

            configuredImage.IsMainImage = true;
            return;
        }

        var realImages = images
            .Where(image => DogImageUrlValidator.IsValidRealDogImageUrl(image.ImageUrl))
            .OrderByDescending(image => image.IsMainImage)
            .ThenBy(image => image.Id)
            .ToList();
        if (realImages.Count == 0)
        {
            return;
        }

        foreach (var image in images)
        {
            image.IsMainImage = false;
        }

        realImages[0].IsMainImage = true;
    }

    private static IReadOnlyList<DogImage> FindDuplicateDemoImages(IEnumerable<DogImage> images)
    {
        return images
            .Where(image => DogImageUrlValidator.IsValidRealDogImageUrl(image.ImageUrl))
            .GroupBy(image => image.ImageUrl.Trim(), StringComparer.OrdinalIgnoreCase)
            .SelectMany(group =>
                group
                    .OrderByDescending(image => image.IsMainImage)
                    .ThenBy(image => image.Id)
                    .Skip(1))
            .ToList();
    }

    private static void AddConfiguredDemoMainImage(Dog dog)
    {
        if (!TryGetConfiguredDemoMainImageUrl(dog.Name, out var imageUrl))
        {
            return;
        }

        dog.Images.Add(new DogImage
        {
            ImageUrl = imageUrl,
            IsMainImage = true
        });
    }

    private static bool TryGetConfiguredDemoMainImageUrl(string dogName, out string imageUrl)
    {
        imageUrl = string.Empty;
        return DemoDogMainImageUrls.TryGetValue(dogName, out var configuredImageUrl) &&
            DogImageUrlValidator.TryNormalize(configuredImageUrl, out imageUrl);
    }

    private static bool IsInvalidDemoImage(DogImage image)
    {
        return string.IsNullOrWhiteSpace(image.ImageUrl) ||
            !DogImageUrlValidator.IsValidRealDogImageUrl(image.ImageUrl) ||
            image.ImageUrl.Contains("placedog.net", StringComparison.OrdinalIgnoreCase) ||
            image.ImageUrl.Contains("images.unsplash.com", StringComparison.OrdinalIgnoreCase) ||
            image.ImageUrl.Contains("placehold.co", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalDemoDogImage(string? imageUrl)
    {
        return !string.IsNullOrWhiteSpace(imageUrl) &&
            imageUrl.Trim().Replace('\\', '/').StartsWith("/images/demo-dogs/", StringComparison.OrdinalIgnoreCase);
    }
}
