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
    public const string VolunteerRole = "Volunteer";

    public const string AdminDemoEmail = "admin@mail.com";
    public const string AdminDemoPassword = "Admin1!";
    public const string AdopterDemoEmail = "adopter@mail.com";
    public const string AdopterDemoPassword = "Adopter1!";
    public const string ShelterDemoEmail = "shelter@mail.com";
    public const string ShelterDemoPassword = "Shelter1!";
    public const string VolunteerDemoEmail = "volunteer@mail.com";
    public const string VolunteerDemoPassword = "Volunteer1!";

    public const string AdopterUserId = "00000000-0000-0000-0000-000000000001";
    public const string ShelterUserId = "11111111-1111-1111-1111-111111111111";
    public const string AdminUserId = "22222222-2222-2222-2222-222222222222";
    public const string VolunteerUserId = "33333333-3333-3333-3333-333333333331";
    private const string SecondVolunteerUserId = "33333333-3333-3333-3333-333333333332";

    private const string SecondAdopterUserId = "00000000-0000-0000-0000-000000000002";
    private const string ThirdAdopterUserId = "00000000-0000-0000-0000-000000000003";
    private const string HopeTailsShelterUserId = "11111111-1111-1111-1111-111111111112";
    private const string SafeHavenShelterUserId = "11111111-1111-1111-1111-111111111113";
    private const string GreenYardShelterUserId = "11111111-1111-1111-1111-111111111114";
    private const string SecondChanceShelterUserId = "11111111-1111-1111-1111-111111111115";
    private const string FriendlyTailsShelterUserId = "11111111-1111-1111-1111-111111111116";
    private const string NorthStarShelterUserId = "11111111-1111-1111-1111-111111111117";

    private const int FoodCategoryId = 1;
    private const int MedicineCategoryId = 2;
    private const int BlanketsCategoryId = 3;
    private const int CleaningSuppliesCategoryId = 4;
    private const int AccessoriesCategoryId = 5;
    private const int AdultDryFoodTypeId = 1;
    private const int PuppyFoodTypeId = 2;
    private const int SeniorFoodTypeId = 3;
    private const int WetFoodTypeId = 4;
    private const int MedicalDietFoodTypeId = 5;

    private static readonly SeedUser[] Users =
    [
        new(AdopterUserId, AdopterDemoEmail, AdopterDemoPassword, "Ana Ionescu", AdopterRole, ["ana.ionescu@pawconnect.local"]),
        new(ShelterUserId, ShelterDemoEmail, ShelterDemoPassword, "Happy Paws Shelter Team", ShelterRole, ["happy-paws@pawconnect.local"]),
        new(AdminUserId, AdminDemoEmail, AdminDemoPassword, "PawConnect Admin", AdminRole, ["admin@pawconnect.local"]),
        new(HopeTailsShelterUserId, "hope-tails@mail.com", ShelterDemoPassword, "Hope Tails Rescue Team", ShelterRole, ["hope-tails@pawconnect.local"]),
        new(SafeHavenShelterUserId, "safe-haven@mail.com", ShelterDemoPassword, "Safe Haven Dogs Team", ShelterRole, ["safe-haven@pawconnect.local"]),
        new(GreenYardShelterUserId, "green-yard@mail.com", ShelterDemoPassword, "Green Yard Animal Care Team", ShelterRole, ["green-yard@pawconnect.local"]),
        new(SecondChanceShelterUserId, "second-chance@mail.com", ShelterDemoPassword, "Second Chance Paws Team", ShelterRole, ["second-chance@pawconnect.local"]),
        new(FriendlyTailsShelterUserId, "friendly-tails@mail.com", ShelterDemoPassword, "Friendly Tails Center Team", ShelterRole, ["friendly-tails@pawconnect.local"]),
        new(NorthStarShelterUserId, "north-star@mail.com", ShelterDemoPassword, "North Star Animal Shelter Team", ShelterRole, ["north-star@pawconnect.local"]),
        new(VolunteerUserId, VolunteerDemoEmail, VolunteerDemoPassword, "Mara Dobre", VolunteerRole, ["mara.volunteer@pawconnect.local"]),
        new(SecondVolunteerUserId, "volunteer2@mail.com", VolunteerDemoPassword, "Andrei Rusu", VolunteerRole, ["andrei.volunteer@pawconnect.local"])
    ];

    private static readonly Dictionary<string, string> DemoDogMainImageUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alma"] = "https://hips.hearstapps.com/hmg-prod/images/cocker-spaniel-685bff3474de0.jpg?crop=0.8888888888888888xw:1xh;center,top&resize=1200:*",
        ["Archie"] = "https://bestforpet.co.nz/wp-content/uploads/2025/11/Beagle.jpg",
        ["Bruno"] = "https://www.thelabradorsite.com/wp-content/uploads/2023/06/sheprador-buddy.jpg",
        ["Daisy"] = "https://www.westminsterkennelclub.org/wp-content/uploads/2025/07/golden_retriever-scaled-1-1024x681.jpg",
        ["Finn"] = "https://cdn.britannica.com/04/240504-050-4D5874D1/Scottish-Terrier-Scottie.jpg",
        ["Grace"] = "https://upload.wikimedia.org/wikipedia/commons/e/ef/GraceTheGreyhound.jpg",
        ["Hazel"] = "https://www.akc.org/wp-content/uploads/2017/11/Longhaired-Dachshund-standing-outdoors.jpg",
        ["Iris"] = "https://www.omlet.co.uk/images/cache/1024/680/Dog-Japanese_Spitz-A_healthy_adult_Japanese_Spitz_with_a_thick_soft_coat_and_bushy_tail.jpg",
        ["Kira"] = "https://cdn.britannica.com/85/232785-050-0EE871BE/Belgian-Malinois-dog.jpg",
        ["Lili"] = "https://cdn.britannica.com/80/232780-050-404D6708/Pembroke-welsh-corgi-dog.jpg",
        ["Luna"] = "https://www.zooplus.ro/ghid/wp-content/uploads/2025/01/airedale-terrier.webp",
        ["Milo"] = "https://bestforpet.co.nz/wp-content/uploads/2025/11/Beagle.jpg",
        ["Mira"] = "https://jesypet.ro/wp-content/uploads/2025/08/Bichon-Maltez-%E2%80%93-Ghid-complet-despre-ca%CC%82inele-mic-cu-inima%CC%86-mare.webp",
        ["Nala"] = "https://corgi-mixes.com/wp-content/uploads/2023/01/Border-Collie-Corgi-Mix.jpg",
        ["Nora"] = "https://www.borrowmydoggy.com/_next/image?url=https%3A%2F%2Fcdn.sanity.io%2Fimages%2F4ij0poqn%2Fproduction%2Fe24bfbd855cda99e303975f2bd2a1bf43079b320-800x600.jpg&w=1080&q=80",
        ["Ollie"] = "https://www.thesprucepets.com/thmb/Fnp6BWaLI08vIpMBxGWAc4XTzXk=/2121x0/filters:no_upscale():strip_icc()/GettyImages-1149826361-fdb297c92dfc4697b0861db53c64d35f.jpg",
        ["Oscar"] = "https://img.fera.ro/images/companies/1/seter-szkocki-fci.png?1704310402663",
        ["Pip"] = "https://cdn.britannica.com/44/233244-050-A65D4571/Chihuahua-dog.jpg",
        ["Poppy"] = "https://www.borrowmydoggy.com/_next/image?url=https%3A%2F%2Fcdn.sanity.io%2Fimages%2F4ij0poqn%2Fproduction%2Fd3c0ce0d77b3b98f45208a83814bca57b314c128-800x600.jpg&w=1080&q=80",
        ["Rex"] = "https://www.purina.com/sites/default/files/styles/social_share/public/2025-09/siberian_husky_4_1.jpg?h=f7d9296c&itok=medyY_xK",
        ["Sasha"] = "https://cms.paw-champ.com/api/assets/dog-wiki/5e6f4c0b-d87f-48c8-94b3-318508a1316e?cache=3600",
        ["Tara"] = "https://cdn.shopify.com/s/files/1/0582/8349/1478/files/romanian_shepherd.jpg?v=1751021932",
        ["Toby"] = "https://media.istockphoto.com/id/1380984414/photo/toy-poddle-on-the-bed.jpg?s=612x612&w=0&k=20&c=Vuq4VKmYQmy5F3MQ64URAlZ9H5IJKVRbTo4SF8mQKtc="
    };

    private static readonly HashSet<string> DemoDogMainImageUrlValues = new(
        DemoDogMainImageUrls.Values,
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] LegacySeedImageMarkers =
    [
        "placehold.co",
        "placedog.net",
        "images.unsplash.com",
        "thelabradorsite.com",
        "westminsterkennelclub.org",
        "cdn.britannica.com",
        "zooplus.ro",
        "bestforpet.co.nz",
        "jesypet.ro",
        "img.fera.ro",
        "purina.com",
        "/images/demo-dogs/"
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var role in new[] { AdopterRole, ShelterRole, AdminRole, VolunteerRole })
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
                foreach (var legacyEmail in seedUser.LegacyEmails)
                {
                    user = await userManager.FindByEmailAsync(legacyEmail);
                    if (user is not null)
                    {
                        break;
                    }
                }
            }

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

                await userManager.CreateAsync(user, seedUser.Password);
            }
            else
            {
                user.UserName = seedUser.Email;
                user.Email = seedUser.Email;
                user.EmailConfirmed = true;
                user.FullName = seedUser.FullName;

                await userManager.UpdateAsync(user);
                await EnsureSeedPasswordAsync(userManager, user, seedUser.Password);
            }

            if (!await userManager.IsInRoleAsync(user, seedUser.Role))
            {
                await userManager.AddToRoleAsync(user, seedUser.Role);
            }
        }

        await SeedLookupDataAsync(context);
        await SeedPresentationDataAsync(context);
        await NormalizeDogBreedLookupsAsync(context);
    }

    private static async Task EnsureSeedPasswordAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string password)
    {
        if (await userManager.HasPasswordAsync(user))
        {
            var removeResult = await userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
            {
                return;
            }
        }

        await userManager.AddPasswordAsync(user, password);
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
                new ResourceCategory { Id = 6, Name = "Other", Description = "General shelter resources." });
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
                continue;
            }

            existing.Name = seedBreed.Name;
            existing.IsActive = true;
            existing.GeneralDescription = seedBreed.GeneralDescription;
            existing.TypicalTraits = seedBreed.TypicalTraits;
            existing.CareNotes = seedBreed.CareNotes;
            existing.CommonHealthConsiderations = seedBreed.CommonHealthConsiderations;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedPresentationDataAsync(ApplicationDbContext context)
    {
        foreach (var profile in GetAdopterProfiles())
        {
            await UpsertAdopterProfileAsync(context, profile);
        }

        foreach (var shelterSeed in GetShelters())
        {
            await UpsertShelterAsync(context, shelterSeed);
        }

        await context.SaveChangesAsync();

        foreach (var dogSeed in GetDogs())
        {
            await UpsertDogAsync(context, dogSeed);
        }

        await context.SaveChangesAsync();

        await CleanSeedDogImagesAsync(context);
        await SeedDemoDogImagesAsync(context);
        await ReplaceMedicalRecordsAsync(context);
        await ReplaceShelterResourcesAsync(context);
        await ReplacePresentationRequestsAsync(context);
        await ReplacePresentationTransferRequestsAsync(context);
        await ReplacePresentationVolunteerTasksAsync(context);
        await SeedFavoritesAndRecentViewsAsync(context);
        await SeedSimulationScenariosAsync(context);

        await context.SaveChangesAsync();
    }

    private static async Task SeedSimulationScenariosAsync(ApplicationDbContext context)
    {
        var happyPawsId = await context.Shelters
            .Where(shelter => shelter.Name == "Happy Paws Shelter")
            .Select(shelter => (int?)shelter.Id)
            .FirstOrDefaultAsync();

        if (happyPawsId.HasValue && !await context.ShelterSimulationScenarios.AnyAsync(scenario => scenario.Name == "Weekend intake readiness"))
        {
            context.ShelterSimulationScenarios.Add(new ShelterSimulationScenario
            {
                Name = "Weekend intake readiness",
                Description = "Saved planning scenario for additional weekend intake and reduced volunteer coverage.",
                CreatedByUserId = ShelterUserId,
                ShelterId = happyPawsId.Value,
                ScopeType = SimulationScopeType.Shelter,
                HorizonDays = 7,
                Status = SimulationScenarioStatus.Draft,
                AssumptionsJson = "[{\"type\":0,\"quantity\":3,\"effectiveDay\":1},{\"type\":1,\"quantity\":1,\"effectiveDay\":2}]",
                IsPinned = true,
                CreatedAtUtc = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc)
            });
        }

        if (!await context.ShelterSimulationScenarios.AnyAsync(scenario => scenario.Name == "Platform volunteer coverage"))
        {
            context.ShelterSimulationScenarios.Add(new ShelterSimulationScenario
            {
                Name = "Platform volunteer coverage",
                Description = "Cross-shelter planning scenario for temporary volunteer unavailability.",
                CreatedByUserId = AdminUserId,
                ScopeType = SimulationScopeType.Platform,
                HorizonDays = 14,
                Status = SimulationScenarioStatus.Draft,
                AssumptionsJson = "[{\"type\":1,\"quantity\":4,\"effectiveDay\":1}]",
                CreatedAtUtc = new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc)
            });
        }
    }

    private static async Task UpsertAdopterProfileAsync(ApplicationDbContext context, AdopterProfileSeed seed)
    {
        var profile = await context.AdopterProfiles.FirstOrDefaultAsync(p => p.ApplicationUserId == seed.UserId);
        if (profile is null)
        {
            profile = new AdopterProfile { ApplicationUserId = seed.UserId };
            context.AdopterProfiles.Add(profile);
        }

        profile.FullName = seed.FullName;
        profile.ProfileImageUrl = null;
        profile.Address = seed.Address;
        profile.City = seed.City;
        profile.PhoneNumber = seed.PhoneNumber;
        profile.HousingType = seed.HousingType;
        profile.HasYard = seed.HasYard;
        profile.HasOtherPets = seed.HasOtherPets;
        profile.HasChildren = seed.HasChildren;
        profile.ExperienceWithDogs = seed.ExperienceWithDogs;
        profile.AdditionalNotes = seed.AdditionalNotes;
    }

    private static async Task UpsertShelterAsync(ApplicationDbContext context, ShelterSeed seed)
    {
        var shelter = await FindShelterForSeedAsync(context, seed);
        if (shelter is null)
        {
            shelter = new Shelter { Name = seed.Name };
            context.Shelters.Add(shelter);
        }

        shelter.Description = seed.Description;
        shelter.Address = seed.Address;
        shelter.City = "Cluj-Napoca";
        shelter.Neighborhood = seed.Neighborhood;
        shelter.PhoneNumber = seed.PhoneNumber;
        shelter.Email = seed.Email;
        shelter.Latitude = seed.Latitude;
        shelter.Longitude = seed.Longitude;
        shelter.VisitStartTime = seed.VisitStartTime ?? new TimeSpan(10, 0, 0);
        shelter.VisitEndTime = seed.VisitEndTime ?? new TimeSpan(17, 0, 0);
        shelter.VisitsAllowedMonday = seed.WeekdayVisits;
        shelter.VisitsAllowedTuesday = seed.WeekdayVisits;
        shelter.VisitsAllowedWednesday = seed.WeekdayVisits;
        shelter.VisitsAllowedThursday = seed.WeekdayVisits;
        shelter.VisitsAllowedFriday = seed.WeekdayVisits;
        shelter.VisitsAllowedSaturday = seed.SaturdayVisits;
        shelter.VisitsAllowedSunday = false;
        (shelter.DogCapacity, shelter.ReservedEmergencySpaces) = GetShelterCapacity(seed.Name);
        shelter.ApplicationUserId = seed.ApplicationUserId;
    }

    private static (int Capacity, int EmergencySpaces) GetShelterCapacity(string shelterName) => shelterName switch
    {
        "Happy Paws Shelter" => (18, 2),
        "Hope Tails Rescue" => (14, 2),
        "Safe Haven Dogs" => (12, 1),
        "Green Yard Animal Care" => (24, 3),
        "Second Chance Paws" => (16, 2),
        "Friendly Tails Center" => (15, 2),
        "North Star Animal Shelter" => (20, 2),
        _ => (30, 2)
    };

    private static async Task<Shelter?> FindShelterForSeedAsync(ApplicationDbContext context, ShelterSeed seed)
    {
        var shelter = await context.Shelters.FirstOrDefaultAsync(s => s.Name == seed.Name);
        if (shelter is not null)
        {
            return shelter;
        }

        if (!string.IsNullOrWhiteSpace(seed.ApplicationUserId))
        {
            shelter = await context.Shelters.FirstOrDefaultAsync(s => s.ApplicationUserId == seed.ApplicationUserId);
            if (shelter is not null)
            {
                return shelter;
            }
        }

        return seed.Name switch
        {
            "Happy Paws Shelter" => await context.Shelters.FirstOrDefaultAsync(s =>
                s.Name.Contains("Demo") ||
                s.Name.Contains("Test") ||
                s.Email == "shelter@pawconnect.test" ||
                s.Email == "u8878233525@id.gle"),
            "Green Yard Animal Care" => await context.Shelters.FirstOrDefaultAsync(s => s.Name == "Green Yard Shelter"),
            _ => null
        };
    }

    private static async Task UpsertDogAsync(ApplicationDbContext context, DogSeed seed)
    {
        var shelter = await context.Shelters.FirstOrDefaultAsync(s => s.Name == seed.ShelterName);
        if (shelter is null)
        {
            return;
        }

        var dog = await context.Dogs.FirstOrDefaultAsync(d => d.Name == seed.Name);
        if (dog is null)
        {
            dog = new Dog { Name = seed.Name };
            context.Dogs.Add(dog);
        }

        dog.ShelterId = shelter.Id;
        dog.Shelter = shelter;
        dog.Breed = seed.Breed;
        dog.CoatColor = seed.CoatColor;
        dog.Age = seed.AgeYears;
        dog.AgeYears = seed.AgeYears;
        dog.AgeMonths = seed.AgeMonths;
        dog.Size = seed.Size;
        dog.Location = "Cluj-Napoca";
        dog.Status = seed.Status;
        dog.Description = seed.Description;
        dog.BehaviorDescription = seed.BehaviorDescription;
        dog.MedicalStatus = seed.MedicalStatus;
        dog.PreferredFoodTypeId = seed.PreferredFoodTypeId;
        dog.DailyFoodAmountGrams = seed.DailyFoodAmountGrams;
        dog.AdoptedAt = seed.AdoptedAt;
        dog.SuccessStoryText = seed.SuccessStoryText;
    }

    private static async Task SeedDemoDogImagesAsync(ApplicationDbContext context)
    {
        var dogNames = DemoDogMainImageUrls.Keys.ToArray();
        var dogs = await context.Dogs
            .Include(dog => dog.Images)
            .Where(dog => dogNames.Contains(dog.Name))
            .ToDictionaryAsync(dog => dog.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var (dogName, imageUrl) in DemoDogMainImageUrls)
        {
            if (!dogs.TryGetValue(dogName, out var dog) ||
                !DogImageUrlValidator.TryNormalize(imageUrl, out var normalizedImageUrl))
            {
                continue;
            }

            var images = dog.Images.ToList();
            foreach (var image in images)
            {
                image.IsMainImage = false;
            }

            var mainImage = images.FirstOrDefault(image =>
                (image.ImageUrl ?? string.Empty).Trim().Equals(normalizedImageUrl, StringComparison.OrdinalIgnoreCase));

            if (mainImage is null)
            {
                mainImage = new DogImage
                {
                    DogId = dog.Id,
                    ImageUrl = normalizedImageUrl
                };

                context.DogImages.Add(mainImage);
            }

            mainImage.IsMainImage = true;
        }
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

    private static async Task CleanSeedDogImagesAsync(ApplicationDbContext context)
    {
        var seedDogNames = GetDogs().Select(dog => dog.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seedDogs = await context.Dogs
            .Include(dog => dog.Images)
            .Where(dog => seedDogNames.Contains(dog.Name))
            .ToListAsync();

        foreach (var dog in seedDogs)
        {
            var images = dog.Images.ToList();
            foreach (var image in images.Where(IsLegacySeedImage).ToList())
            {
                context.DogImages.Remove(image);
                images.Remove(image);
            }

            foreach (var duplicate in FindDuplicateImages(images))
            {
                context.DogImages.Remove(duplicate);
                images.Remove(duplicate);
            }

            var validImages = images
                .Where(image => DogImageUrlValidator.IsValidRealDogImageUrl(image.ImageUrl))
                .OrderByDescending(image => image.IsMainImage)
                .ThenBy(image => image.Id)
                .ToList();

            foreach (var image in images)
            {
                image.IsMainImage = false;
            }

            if (validImages.Count > 0)
            {
                validImages[0].IsMainImage = true;
            }
        }
    }

    private static async Task ReplaceMedicalRecordsAsync(ApplicationDbContext context)
    {
        var dogsByName = await context.Dogs.ToDictionaryAsync(dog => dog.Name, StringComparer.OrdinalIgnoreCase);
        var dogIds = GetMedicalRecords()
            .Where(record => dogsByName.ContainsKey(record.DogName))
            .Select(record => dogsByName[record.DogName].Id)
            .Distinct()
            .ToArray();

        var existing = await context.MedicalRecords.Where(record => dogIds.Contains(record.DogId)).ToListAsync();
        context.MedicalRecords.RemoveRange(existing);

        foreach (var seed in GetMedicalRecords())
        {
            if (!dogsByName.TryGetValue(seed.DogName, out var dog))
            {
                continue;
            }

            context.MedicalRecords.Add(new MedicalRecord
            {
                DogId = dog.Id,
                VaccineName = seed.VaccineName,
                TreatmentDescription = seed.TreatmentDescription,
                RecordDate = seed.RecordDate,
                Notes = seed.Notes
            });
        }
    }

    private static async Task ReplaceShelterResourcesAsync(ApplicationDbContext context)
    {
        var sheltersByName = await context.Shelters.ToDictionaryAsync(shelter => shelter.Name, StringComparer.OrdinalIgnoreCase);
        var shelterIds = GetShelters()
            .Where(shelter => sheltersByName.ContainsKey(shelter.Name))
            .Select(shelter => sheltersByName[shelter.Name].Id)
            .ToArray();

        var existing = await context.ResourceStocks.Where(resource => shelterIds.Contains(resource.ShelterId)).ToListAsync();
        context.ResourceStocks.RemoveRange(existing);

        foreach (var resourceSeed in GetResourceStocks())
        {
            if (!sheltersByName.TryGetValue(resourceSeed.ShelterName, out var shelter))
            {
                continue;
            }

            context.ResourceStocks.Add(new ResourceStock
            {
                ShelterId = shelter.Id,
                Name = resourceSeed.Name,
                Quantity = resourceSeed.Quantity,
                Unit = resourceSeed.Unit,
                LowStockThreshold = resourceSeed.LowStockThreshold,
                ResourceCategoryId = resourceSeed.ResourceCategoryId,
                FoodTypeId = resourceSeed.FoodTypeId,
                LastUpdatedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc)
            });
        }
    }

    private static async Task ReplacePresentationRequestsAsync(ApplicationDbContext context)
    {
        var seedDogNames = GetDogs().Select(dog => dog.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seedDogIds = await context.Dogs
            .Where(dog => seedDogNames.Contains(dog.Name))
            .Select(dog => dog.Id)
            .ToListAsync();
        var seedAdopterIds = new[] { AdopterUserId, SecondAdopterUserId, ThirdAdopterUserId };

        var existing = await context.AdoptionRequests
            .Where(request => seedDogIds.Contains(request.DogId) && seedAdopterIds.Contains(request.AdopterId))
            .ToListAsync();
        context.AdoptionRequests.RemoveRange(existing);

        var dogsByName = await context.Dogs.ToDictionaryAsync(dog => dog.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var requestSeed in GetAdoptionRequests())
        {
            if (!dogsByName.TryGetValue(requestSeed.DogName, out var dog))
            {
                continue;
            }

            context.AdoptionRequests.Add(new AdoptionRequest
            {
                DogId = dog.Id,
                AdopterId = requestSeed.AdopterId,
                Status = requestSeed.Status,
                PreferredVisitDateTime = requestSeed.PreferredVisitDateTime,
                VisitStatus = requestSeed.VisitStatus,
                VisitConfirmedAt = requestSeed.VisitConfirmedAt,
                VisitConfirmedByUserId = requestSeed.VisitConfirmedByUserId,
                Message = requestSeed.Message,
                ReasonForAdoption = requestSeed.ReasonForAdoption,
                HoursAlonePerDay = requestSeed.HoursAlonePerDay,
                AdditionalInformation = requestSeed.AdditionalInformation,
                ShelterInternalNotes = requestSeed.ShelterInternalNotes,
                CreatedAt = requestSeed.CreatedAt,
                UpdatedAt = requestSeed.UpdatedAt
            });
        }
    }


    private static async Task ReplacePresentationTransferRequestsAsync(ApplicationDbContext context)
    {
        var transferSeeds = GetTransferRequests();
        var transferDogNames = transferSeeds.Select(seed => seed.DogName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dogIds = await context.Dogs
            .Where(dog => transferDogNames.Contains(dog.Name))
            .Select(dog => dog.Id)
            .ToListAsync();

        var existing = await context.DogTransferRequests
            .Where(transfer => dogIds.Contains(transfer.DogId))
            .ToListAsync();
        context.DogTransferRequests.RemoveRange(existing);

        var dogsByName = await context.Dogs.ToDictionaryAsync(dog => dog.Name, StringComparer.OrdinalIgnoreCase);
        var sheltersByName = await context.Shelters.ToDictionaryAsync(shelter => shelter.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var seed in transferSeeds)
        {
            if (!dogsByName.TryGetValue(seed.DogName, out var dog) ||
                !sheltersByName.TryGetValue(seed.SourceShelterName, out var sourceShelter) ||
                !sheltersByName.TryGetValue(seed.DestinationShelterName, out var destinationShelter))
            {
                continue;
            }

            context.DogTransferRequests.Add(new DogTransferRequest
            {
                DogId = dog.Id,
                SourceShelterId = sourceShelter.Id,
                DestinationShelterId = destinationShelter.Id,
                RequestedByUserId = seed.RequestedByUserId,
                RespondedByUserId = seed.RespondedByUserId,
                CompletedByUserId = seed.CompletedByUserId,
                Status = seed.Status,
                Priority = seed.Priority,
                Reason = seed.Reason,
                SourceShelterNotes = seed.SourceShelterNotes,
                DestinationShelterResponseNotes = seed.DestinationShelterResponseNotes,
                AdminNotes = seed.AdminNotes,
                RequestedAtUtc = seed.RequestedAtUtc,
                RespondedAtUtc = seed.RespondedAtUtc,
                CompletedAtUtc = seed.CompletedAtUtc,
                CancelledAtUtc = seed.CancelledAtUtc,
                CreatedAtUtc = seed.RequestedAtUtc,
                UpdatedAtUtc = seed.CompletedAtUtc ?? seed.RespondedAtUtc ?? seed.CancelledAtUtc ?? seed.RequestedAtUtc
            });
        }
    }

    private static async Task ReplacePresentationVolunteerTasksAsync(ApplicationDbContext context)
    {
        foreach (var seed in GetVolunteerProfiles())
        {
            var profile = await context.VolunteerProfiles.FirstOrDefaultAsync(volunteer => volunteer.UserId == seed.UserId);
            if (profile is null)
            {
                profile = new VolunteerProfile { UserId = seed.UserId };
                context.VolunteerProfiles.Add(profile);
            }

            var preferredShelterId = seed.PreferredShelterName is null
                ? null
                : await context.Shelters
                    .Where(shelter => shelter.Name == seed.PreferredShelterName)
                    .Select(shelter => (int?)shelter.Id)
                    .FirstOrDefaultAsync();

            profile.DisplayName = seed.DisplayName;
            profile.Email = seed.Email;
            profile.PhoneNumber = seed.PhoneNumber;
            profile.PreferredShelterId = preferredShelterId;
            profile.Skills = seed.Skills;
            profile.AvailabilityNotes = seed.AvailabilityNotes;
            profile.IsActive = seed.IsActive;
            profile.UpdatedAtUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();

        var taskSeeds = GetVolunteerTasks();
        var taskTitles = taskSeeds.Select(seed => seed.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingTasks = await context.VolunteerTasks
            .Include(task => task.Activities)
            .Where(task => taskTitles.Contains(task.Title))
            .ToListAsync();
        context.VolunteerTasks.RemoveRange(existingTasks);
        await context.SaveChangesAsync();

        var sheltersByName = await context.Shelters.ToDictionaryAsync(shelter => shelter.Name, StringComparer.OrdinalIgnoreCase);
        var dogsByName = await context.Dogs.ToDictionaryAsync(dog => dog.Name, StringComparer.OrdinalIgnoreCase);
        var profilesByUserId = await context.VolunteerProfiles.ToDictionaryAsync(profile => profile.UserId, StringComparer.OrdinalIgnoreCase);

        foreach (var seed in taskSeeds)
        {
            if (!sheltersByName.TryGetValue(seed.ShelterName, out var shelter))
            {
                continue;
            }

            var dogId = seed.DogName is not null && dogsByName.TryGetValue(seed.DogName, out var dog)
                ? dog.Id
                : (int?)null;
            var assignedProfile = seed.AssignedVolunteerUserId is not null && profilesByUserId.TryGetValue(seed.AssignedVolunteerUserId, out var profile)
                ? profile
                : null;
            var now = DateTime.UtcNow;
            var task = new VolunteerTask
            {
                ShelterId = shelter.Id,
                DogId = dogId,
                CreatedByUserId = seed.CreatedByUserId,
                AssignedVolunteerProfileId = assignedProfile?.Id,
                Title = seed.Title,
                Description = seed.ShelterNotes ?? seed.Title,
                Category = seed.Category,
                Status = seed.Status,
                Priority = seed.Priority,
                ScheduledStartUtc = seed.ScheduledStartUtc,
                ScheduledEndUtc = seed.ScheduledEndUtc,
                DueAtUtc = seed.DueAtUtc,
                Location = seed.Location,
                RequiredSkills = seed.RequiredSkills,
                ShelterNotes = seed.ShelterNotes,
                VolunteerNotes = seed.VolunteerNotes,
                CompletionNotes = seed.CompletionNotes,
                AssignedAtUtc = assignedProfile is null ? null : seed.ScheduledStartUtc.AddHours(-4),
                StartedAtUtc = seed.Status is VolunteerTaskStatus.InProgress or VolunteerTaskStatus.Completed ? seed.ScheduledStartUtc : null,
                CompletedAtUtc = seed.Status == VolunteerTaskStatus.Completed ? seed.ScheduledEndUtc : null,
                CancelledAtUtc = seed.Status == VolunteerTaskStatus.Cancelled ? seed.ScheduledStartUtc.AddHours(-8) : null,
                CreatedAtUtc = seed.ScheduledStartUtc.AddDays(-2),
                UpdatedAtUtc = now
            };

            task.Activities.Add(new VolunteerTaskActivity
            {
                ActorUserId = seed.CreatedByUserId,
                ActivityType = VolunteerTaskActivityType.Created,
                Message = "Seeded volunteer task created for the demo workspace.",
                CreatedAtUtc = task.CreatedAtUtc
            });

            if (assignedProfile is not null)
            {
                task.Activities.Add(new VolunteerTaskActivity
                {
                    ActorUserId = seed.CreatedByUserId,
                    ActivityType = VolunteerTaskActivityType.Assigned,
                    Message = $"Assigned to {assignedProfile.DisplayName}.",
                    CreatedAtUtc = task.AssignedAtUtc ?? task.CreatedAtUtc
                });
            }

            if (task.StartedAtUtc.HasValue)
            {
                task.Activities.Add(new VolunteerTaskActivity
                {
                    ActorUserId = assignedProfile?.UserId,
                    ActivityType = VolunteerTaskActivityType.Started,
                    Message = "Volunteer started the task.",
                    CreatedAtUtc = task.StartedAtUtc.Value
                });
            }

            if (task.CompletedAtUtc.HasValue)
            {
                task.Activities.Add(new VolunteerTaskActivity
                {
                    ActorUserId = assignedProfile?.UserId,
                    ActivityType = VolunteerTaskActivityType.Completed,
                    Message = task.CompletionNotes ?? "Volunteer completed the task.",
                    CreatedAtUtc = task.CompletedAtUtc.Value
                });
            }

            if (task.CancelledAtUtc.HasValue)
            {
                task.Activities.Add(new VolunteerTaskActivity
                {
                    ActorUserId = seed.CreatedByUserId,
                    ActivityType = VolunteerTaskActivityType.Cancelled,
                    Message = "Task cancelled because the appointment was moved.",
                    CreatedAtUtc = task.CancelledAtUtc.Value
                });
            }

            context.VolunteerTasks.Add(task);
        }
    }
    private static async Task SeedFavoritesAndRecentViewsAsync(ApplicationDbContext context)
    {
        var dogsByName = await context.Dogs.ToDictionaryAsync(dog => dog.Name, StringComparer.OrdinalIgnoreCase);
        var favoriteNames = new[] { "Mira", "Bella", "Lili" };
        foreach (var dogName in favoriteNames)
        {
            if (!dogsByName.TryGetValue(dogName, out var dog))
            {
                continue;
            }

            var exists = await context.FavoriteDogs.AnyAsync(favorite =>
                favorite.AdopterId == AdopterUserId &&
                favorite.DogId == dog.Id);
            if (!exists)
            {
                context.FavoriteDogs.Add(new FavoriteDog
                {
                    AdopterId = AdopterUserId,
                    DogId = dog.Id,
                    CreatedAt = new DateTime(2026, 5, 12, 14, 0, 0, DateTimeKind.Utc)
                });
            }
        }

        var recentNames = new[] { "Oscar", "Nala", "Max", "Toby" };
        foreach (var dogName in recentNames)
        {
            if (!dogsByName.TryGetValue(dogName, out var dog))
            {
                continue;
            }

            var exists = await context.RecentlyViewedDogs.AnyAsync(recent =>
                recent.AdopterId == AdopterUserId &&
                recent.DogId == dog.Id);
            if (!exists)
            {
                context.RecentlyViewedDogs.Add(new RecentlyViewedDog
                {
                    AdopterId = AdopterUserId,
                    DogId = dog.Id,
                    ViewedAt = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc)
                });
            }
        }
    }

    private static bool IsLegacySeedImage(DogImage image)
    {
        var trimmedImageUrl = (image.ImageUrl ?? string.Empty).Trim();
        if (DemoDogMainImageUrlValues.Contains(trimmedImageUrl) &&
            DogImageUrlValidator.IsValidRealDogImageUrl(trimmedImageUrl))
        {
            return false;
        }

        if (!DogImageUrlValidator.IsValidRealDogImageUrl(trimmedImageUrl))
        {
            return true;
        }

        var url = trimmedImageUrl.Replace('\\', '/');
        return LegacySeedImageMarkers.Any(marker => url.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<DogImage> FindDuplicateImages(IEnumerable<DogImage> images)
    {
        return images
            .Where(image => DogImageUrlValidator.IsValidRealDogImageUrl(image.ImageUrl))
            .GroupBy(image => image.ImageUrl.Trim(), StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group
                .OrderByDescending(image => image.IsMainImage)
                .ThenBy(image => image.Id)
                .Skip(1))
            .ToList();
    }

    private static IReadOnlyList<AdopterProfileSeed> GetAdopterProfiles()
    {
        return
        [
            new(
                AdopterUserId,
                "Ana Ionescu",
                "Strada Louis Pasteur 18",
                "Cluj-Napoca",
                "0712345678",
                HousingType.Apartment,
                false,
                true,
                false,
                "Moderate dog experience; grew up with family dogs and has cared for an older dog through recovery.",
                "Lives in an apartment in Zorilor with one older recovering dog. Prefers a small or medium calm companion with low-to-moderate activity needs, about 4 hours alone per day, and gentle behavior that will not overwhelm the resident dog.")
        ];
    }

    private static IReadOnlyList<ShelterSeed> GetShelters()
    {
        return
        [
            new(
                "Happy Paws Shelter",
                "Happy Paws Shelter supports responsible adoptions in Cluj-Napoca with careful medical care, calm first meetings, and post-adoption guidance. The team keeps practical behavior notes so adopters can choose dogs whose routines and compatibility needs fit real homes.",
                "Strada Observatorului 12",
                "Zorilor",
                "0722345678",
                ShelterDemoEmail,
                46.7556,
                23.5804,
                ShelterUserId,
                new TimeSpan(10, 0, 0),
                new TimeSpan(18, 0, 0),
                true,
                true),
            new(
                "Hope Tails Rescue",
                "A foster-supported rescue profile for dogs that need patient matching and careful behavior notes. Visits are planned by appointment so adopters can meet dogs in a calm setting.",
                "Strada Fabricii 45",
                "Marasti",
                "+40 700 000 102",
                "hope-tails@pawconnect.local",
                46.7842,
                23.6157,
                HopeTailsShelterUserId),
            new(
                "Safe Haven Dogs",
                "A small shelter profile focused on quieter dogs, senior companions, and gradual introductions. The team keeps detailed notes about routine, confidence, and home fit.",
                "Strada Buna Ziua 22",
                "Buna Ziua",
                "+40 700 000 103",
                "safe-haven@pawconnect.local",
                46.7509,
                23.6022,
                SafeHavenShelterUserId),
            new(
                "Green Yard Animal Care",
                "A spacious shelter profile for dogs that enjoy outdoor time and structured activity. The team works with adopters on exercise expectations before each visit.",
                "Strada Donath 60",
                "Grigorescu",
                "+40 700 000 104",
                "green-yard@pawconnect.local",
                46.7719,
                23.5458,
                GreenYardShelterUserId),
            new(
                "Second Chance Paws",
                "A Manastur shelter profile that supports shy and recovering dogs with predictable daily routines. Adopters are encouraged to ask about introductions and transition plans.",
                "Strada Campului 132",
                "Manastur",
                "+40 700 000 105",
                "second-chance@pawconnect.local",
                46.7478,
                23.5486,
                SecondChanceShelterUserId),
            new(
                "Friendly Tails Center",
                "A family-oriented shelter profile with dogs observed around visitors, older children, and steady household routines. The team highlights both strengths and caution notes.",
                "Strada Liviu Rebreanu 32",
                "Gheorgheni",
                "+40 700 000 106",
                "friendly-tails@pawconnect.local",
                46.7624,
                23.6226,
                FriendlyTailsShelterUserId),
            new(
                "North Star Animal Shelter",
                "A north-side shelter profile for dogs from Iris and nearby industrial areas. The staff records activity needs, confidence around people, and compatibility observations.",
                "Strada Oasului 86",
                "Iris",
                "+40 700 000 107",
                "north-star@pawconnect.local",
                46.7974,
                23.6042,
                NorthStarShelterUserId)
        ];
    }

    private static IReadOnlyList<DogSeed> GetDogs()
    {
        return
        [
            new(
                "Happy Paws Shelter",
                "Bella",
                "Labrador Retriever",
                "Golden",
                6,
                3,
                DogSize.Medium,
                DogStatus.Reserved,
                "Bella enjoys slow walks and settles down quickly after exploring. She likes staying close to people without demanding constant activity. A predictable routine and relaxed evenings suit her well, even in a smaller home.",
                "Gentle and patient during handling. She is friendly with familiar people and prefers quiet routines with soft attention. Calm dogs are easier for her than pushy playmates.",
                "Vaccinated and healthy.",
                SeniorFoodTypeId,
                340),
            new(
                "Happy Paws Shelter",
                "Nala",
                "Border Collie Ã— Corgi Mix",
                "Brown and white",
                2,
                0,
                DogSize.Medium,
                DogStatus.Reserved,
                "Nala enjoys short daily walks, gentle play, and indoor rest after she has had time with people. She approaches visitors with curiosity and a wagging tail, then settles close by. Her medium size and steady routine make her easy to imagine in a quieter home.",
                "Friendly and attentive around people. She has done well around older children during supervised visits and responds well to positive handling.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                330),
            new(
                "Happy Paws Shelter",
                "Max",
                "Shar Pei Ã— German Shepherd Mix",
                "Black and tan",
                3,
                0,
                DogSize.Medium,
                DogStatus.Available,
                "Max enjoys longer walks and games that let him use his energy. He likes exploring open areas, then settles well with people he knows when his day has had a clear rhythm. He may need apartment adopters to plan regular outdoor time.",
                "Playful and social with familiar volunteers. He responds well to praise, training games, and structured walks. Fast-moving cats or smaller animals can hold his attention too much.",
                "Vaccinated and dewormed.",
                AdultDryFoodTypeId,
                360),
            new(
                "Hope Tails Rescue",
                "Mira",
                "Bichon",
                "White",
                3,
                0,
                DogSize.Small,
                DogStatus.Available,
                "Mira enjoys short neighborhood walks and quiet evenings indoors. During feeding time she has passed the shelter cats calmly, then returned her attention to the handler. She settles near familiar people when the daily rhythm is predictable.",
                "Gentle handling suits her well. She walks politely beside familiar calm dogs and takes guidance easily.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                170),
            new(
                "Hope Tails Rescue",
                "Sasha",
                "Spaniel",
                "Brown",
                2,
                0,
                DogSize.Medium,
                DogStatus.Reserved,
                "Sasha watches new people carefully before approaching, then relaxes when the routine becomes familiar. She notices cats and smaller animals but can be redirected with treats and a calm voice. A patient adopter would see more of her playful side over time.",
                "She prefers steady dogs over bouncy playmates and needs slow introductions. Quiet encouragement works better than pressure.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                280),
            new(
                "Hope Tails Rescue",
                "Oscar",
                "Setter",
                "Tricolor",
                2,
                0,
                DogSize.Medium,
                DogStatus.Available,
                "Oscar greets familiar volunteers with a loose body and a wagging tail. He enjoys play sessions with steady dogs and settles after a medium walk. He could fit an adopter who wants a sociable companion without extreme activity needs.",
                "He likes playful dogs that respect pauses. He is easy to redirect with praise and simple cues.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                310),
            new(
                "Safe Haven Dogs",
                "Lili",
                "Corgi",
                "Tricolor",
                5,
                0,
                DogSize.Small,
                DogStatus.Available,
                "Lili enjoys short walks, soft praise, and resting close to people in the evening. She has stayed relaxed during supervised visits with older children when playtime was guided. Her predictable habits make her a strong fit for a calmer home.",
                "She takes treats gently and responds well to routine. Pushy dogs can make her retreat, so introductions should stay calm.",
                "Vaccinated and healthy.",
                SeniorFoodTypeId,
                220),
            new(
                "Safe Haven Dogs",
                "Toby",
                "Poodle",
                "White",
                2,
                0,
                DogSize.Small,
                DogStatus.Available,
                "Toby likes leash walks and quiet indoor rest. He enjoys gentle interaction but needs a little time before fully relaxing with new people. He may suit someone looking for a softer companion outside the busiest parts of the city.",
                "Friendly once introduced slowly. He is more comfortable with calm dogs than very energetic ones.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                190),
            new(
                "Safe Haven Dogs",
                "Pip",
                "Chihuahua",
                "Brown",
                4,
                4,
                DogSize.Small,
                DogStatus.Available,
                "Pip enjoys short predictable walks and spends much of the afternoon curled near a familiar person. Sudden changes can make him pause, but he relaxes with a gentle voice and a steady routine. He is better suited to a quiet home than to constant activity.",
                "He can be worried by noisy visitors and fast movement. Calm older children may be easier for him than toddlers.",
                "Vaccinated; dental check recommended at next routine visit.",
                SeniorFoodTypeId,
                140),
            new(
                "Safe Haven Dogs",
                "Iris",
                "Japanese Spitz",
                "White",
                1,
                10,
                DogSize.Small,
                DogStatus.Available,
                "Iris is bright and observant, with a soft routine around familiar handlers. She enjoys short training moments and rests quietly after gentle play. New places can make her cautious at first.",
                "She is not pushy with other dogs and prefers gentle, low-pressure interactions. A calm first meeting would help her feel secure around a senior or sensitive dog.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                180),
            new(
                "Green Yard Animal Care",
                "Bruno",
                "Labrador Retriever Ã— German Shepherd Mix",
                "Brown",
                4,
                0,
                DogSize.Large,
                DogStatus.Available,
                "Bruno likes longer walks, fetch, and training games that give him a job to do. He would benefit from regular outdoor play and enough room to run before settling. Fast-moving small animals hold his attention too much for homes with cats.",
                "He enjoys sturdy, playful dogs after a proper introduction. Noisy, chaotic play with very young children may be too much for him.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                520),
            new(
                "Green Yard Animal Care",
                "Rocky",
                "German Shepherd",
                "Black and tan",
                4,
                0,
                DogSize.Large,
                DogStatus.Available,
                "Rocky enjoys training games, brisk walks, and chances to stretch his legs outside. He would benefit from space to run and an adopter who likes working with smart, energetic dogs. He bonds strongly once he understands the routine, but a very quiet flat would not be his best match.",
                "Alert, clever, and motivated by structured activity. He is better suited to an experienced adopter who can offer consistent outdoor play. He can be too intense for shy dogs or very noisy young children.",
                "Vaccinated.",
                AdultDryFoodTypeId,
                510),
            new(
                "Green Yard Animal Care",
                "Rex",
                "Siberian Husky",
                "White",
                3,
                0,
                DogSize.Large,
                DogStatus.Available,
                "Rex is happiest when he has a chance to move, sniff, and work through training games. He needs regular outdoor play and space to run before he can fully settle. Quick cats or small animals are likely too exciting for him.",
                "He can overwhelm shy dogs and does best with confident handling. He is a weaker fit for quiet flats or very low-activity homes.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                540),
            new(
                "Green Yard Animal Care",
                "Zara",
                "Border Collie",
                "Black",
                2,
                8,
                DogSize.Medium,
                DogStatus.Available,
                "Zara enjoys longer walks, fetch, and training games that let her think. She settles better after a session that includes movement and simple tasks. A home with a yard or an active adopter would suit her best.",
                "She is quick to learn and enjoys working with people. Without enough activity she can become restless and vocal.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                330),
            new(
                "Green Yard Animal Care",
                "Cooper",
                "Boxer",
                "Brown and white",
                3,
                5,
                DogSize.Large,
                DogStatus.Available,
                "Cooper enjoys outdoor play, tug games, and learning simple cues. He likes people, but his body language can be bouncy when he is excited. He would do best with adopters who enjoy active routines and can guide play calmly.",
                "Older children who understand dog space are a better fit than very young children. He may be too rough for fragile or shy dogs.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                500),
            new(
                "Second Chance Paws",
                "Luna",
                "Airedale Terrier",
                "Brown and white",
                1,
                6,
                DogSize.Medium,
                DogStatus.InTreatment,
                "Luna is currently receiving basic medical care and is not ready for adopter searches. She can be cautious in new places but relaxes when routines are predictable. Her record helps show shelter-side treatment visibility.",
                "Curious but shy around new people. She needs calm introductions and patient handling while she recovers.",
                "Under treatment for a minor skin irritation.",
                MedicalDietFoodTypeId,
                260),
            new(
                "Second Chance Paws",
                "Finn",
                "Terrier",
                "Brown",
                2,
                3,
                DogSize.Small,
                DogStatus.Available,
                "Finn watches new people carefully before approaching and relaxes once the routine becomes familiar. He enjoys slow walks and sniffing games, especially when the environment is not too busy. A patient adopter would help him build confidence.",
                "He can become overwhelmed by sudden noise and fast movement. Very young children may be too intense unless introductions are carefully managed.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                200),
            new(
                "Second Chance Paws",
                "Hazel",
                "Dachshund",
                "Brown and white",
                6,
                0,
                DogSize.Small,
                DogStatus.Reserved,
                "Hazel enjoys short walks and quiet evenings near familiar people. She notices cats through the kennel area but can be redirected with treats and a calm voice. Slow, supervised introductions would still be important in a home with cats.",
                "She takes treats gently and prefers calm handling. Bouncy dogs can make her step away, so steady canine company is better.",
                "Vaccinated; mild dental tartar noted for monitoring.",
                SeniorFoodTypeId,
                170),
            new(
                "Second Chance Paws",
                "Radu",
                "Romanian Mioritic Shepherd",
                "White",
                5,
                7,
                DogSize.Large,
                DogStatus.InTreatment,
                "Radu is being monitored after a minor leg strain and is not part of public adopter suggestions yet. He enjoys calm attention and short recovery walks. His profile helps shelter staff show treatment and follow-up records.",
                "Gentle with familiar handlers, but he needs restricted activity until cleared by the shelter veterinarian.",
                "In treatment; activity restricted during recovery.",
                MedicalDietFoodTypeId,
                560),
            new(
                "Second Chance Paws",
                "Grace",
                "Greyhound",
                "Black",
                5,
                0,
                DogSize.Large,
                DogStatus.Available,
                "Grace enjoys calm indoor time and short bursts of movement outside. She rests quietly after a predictable walk and likes a soft place near people. Fast-moving small animals can trigger too much interest.",
                "She is polite with calm dogs after slow introductions. A home with cats or small pets should discuss her chase interest carefully with the shelter.",
                "Vaccinated and healthy.",
                SeniorFoodTypeId,
                430),
            new(
                "Friendly Tails Center",
                "Alma",
                "Cocker Spaniel",
                "Golden",
                4,
                0,
                DogSize.Medium,
                DogStatus.Available,
                "Alma enjoys daily walks, soft praise, and being near people during quiet parts of the day. She has stayed relaxed during supervised visits with older children and takes treats gently. A family routine with guided play would suit her well.",
                "She can meet calm dogs politely when introductions are slow. Cats should be introduced carefully because she becomes curious when they move quickly.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                300),
            new(
                "Friendly Tails Center",
                "Archie",
                "Beagle",
                "Tricolor",
                3,
                2,
                DogSize.Medium,
                DogStatus.Available,
                "Archie enjoys daily walks, scent games, and food puzzles. He is friendly with familiar visitors and settles well after a walk that lets him sniff. His routine would work best with adopters who enjoy regular outings.",
                "He has been relaxed around older children when food games were structured. Fast-moving cats may be too interesting for him.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                320),
            new(
                "Friendly Tails Center",
                "Daisy",
                "Golden Retriever",
                "Golden",
                3,
                0,
                DogSize.Medium,
                DogStatus.Adopted,
                "Daisy quickly became a favorite with volunteers because she enjoyed gentle attention and predictable routines. She did well around older children during supervised visits before adoption. Her profile keeps the success stories page populated with a warm, realistic adoption example.",
                "Friendly and relaxed with familiar people. She was comfortable around children who approached calmly.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                360,
                new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                "Daisy was adopted by a family in Gheorgheni after two calm visits and a successful weekend transition plan."),
            new(
                "Friendly Tails Center",
                "Milo",
                "Beagle",
                "Tricolor",
                2,
                0,
                DogSize.Medium,
                DogStatus.Adopted,
                "Milo enjoyed puzzle toys and food games while waiting for adoption. He liked longer sniffing walks and gentle training sessions. His adopted profile verifies that adopted dogs stay visible only in success-story contexts.",
                "Playful and food motivated with people he knows. He responded well to simple cues and guided family play.",
                "Healthy.",
                WetFoodTypeId,
                300,
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                "Milo found a patient family who enjoys scent walks and short training games."),
            new(
                "North Star Animal Shelter",
                "Nora",
                "Labrador Retriever",
                "Black",
                2,
                9,
                DogSize.Medium,
                DogStatus.Available,
                "Nora enjoys medium-length walks and quiet time after she has greeted familiar people. She has passed the shelter cats without pulling when the handler used treats and a calm voice. She would still need slow introductions in a home with cats.",
                "She is friendly without being pushy and walks politely near calm dogs. She responds well to routine and gentle guidance.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                340),
            new(
                "North Star Animal Shelter",
                "Kira",
                "Belgian Malinois",
                "Brown",
                2,
                4,
                DogSize.Large,
                DogStatus.Available,
                "Kira is happiest with training games, longer walks, and clear tasks. She needs an adopter who enjoys structure and has time for daily outdoor work. She is not a good fit for a low-activity routine.",
                "Quick, intense, and eager to work. She can overwhelm shy dogs and would suit experienced handling better than a first-time adopter.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                500),
            new(
                "North Star Animal Shelter",
                "Tara",
                "Romanian Carpathian Shepherd",
                "Black and tan",
                4,
                6,
                DogSize.Large,
                DogStatus.Reserved,
                "Tara enjoys yard time, patrol walks, and predictable handling. She settles when she has space and a clear routine, but she may feel crowded in a busy apartment. Her reserved status may change, so adopters should confirm availability.",
                "She prefers calm introductions and may do best as the only dog or with a steady dog that respects space.",
                "Vaccinated and healthy.",
                AdultDryFoodTypeId,
                540),
            new(
                "North Star Animal Shelter",
                "Ollie",
                "Poodle Ã— Bichon Mix",
                "White",
                1,
                8,
                DogSize.Small,
                DogStatus.Available,
                "Ollie enjoys short walks, simple training games, and resting near people after play. New situations can make him pause at first, but he is easy to redirect with praise and treats. His small size and predictable routine make him approachable for careful first-time adopters.",
                "He takes guidance well and is gentle during handling. Cat history is limited, so adopters with cats should ask the shelter before deciding.",
                "Vaccinated and healthy.",
                PuppyFoodTypeId,
                180),
            new(
                "North Star Animal Shelter",
                "Poppy",
                "Maltese",
                "White",
                7,
                0,
                DogSize.Small,
                DogStatus.Available,
                "Poppy enjoys short walks, soft bedding, and a quiet evening routine. She likes being close to people but does not demand constant activity. Her pace may suit a calm apartment home.",
                "She is gentle with handling and prefers visitors who move slowly. No direct cat history is available, so the shelter should be asked before a cat home.",
                "Vaccinated; senior wellness check completed.",
                SeniorFoodTypeId,
                150)
        ];
    }

    private static IReadOnlyList<MedicalSeed> GetMedicalRecords()
    {
        return
        [
            new("Bella", "Annual vaccination", "Routine vaccination and wellness check.", new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc), "No complications reported."),
            new("Max", "Rabies vaccine", "Rabies vaccination and deworming.", new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Utc), "Energy level normal after visit."),
            new("Mira", "Annual vaccination", "Vaccination and dental screening.", new DateTime(2026, 5, 5, 0, 0, 0, DateTimeKind.Utc), "Dental follow-up not urgent."),
            new("Sasha", null, "Routine check and parasite prevention.", new DateTime(2026, 4, 26, 0, 0, 0, DateTimeKind.Utc), "Appetite and weight stable."),
            new("Luna", null, "Skin treatment plan started.", new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), "Follow-up recommended after two weeks."),
            new("Radu", null, "Leg strain follow-up.", new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc), "Restricted activity until recheck."),
            new("Hazel", null, "Dental check.", new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc), "Mild tartar noted; monitor at next visit."),
            new("Daisy", "Rabies vaccine", "Pre-adoption vaccination review.", new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc), "Records transferred with adopter packet."),
            new("Milo", "Annual vaccination", "Pre-adoption routine check.", new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc), "No issues noted."),
            new("Poppy", null, "Senior wellness check.", new DateTime(2026, 5, 8, 0, 0, 0, DateTimeKind.Utc), "Weight and appetite monitored.")
        ];
    }

    private static IReadOnlyList<ResourceSeed> GetResourceStocks()
    {
        var resources = new List<ResourceSeed>();
        foreach (var shelter in GetShelters())
        {
            resources.AddRange(
            [
                new ResourceSeed(shelter.Name, "Adult dry food", 64, "kg", 20, FoodCategoryId, AdultDryFoodTypeId),
                new ResourceSeed(shelter.Name, "Wet food cans", 38, "pcs", 18, FoodCategoryId, WetFoodTypeId),
                new ResourceSeed(shelter.Name, "Puppy food", 16, "kg", 12, FoodCategoryId, PuppyFoodTypeId),
                new ResourceSeed(shelter.Name, "Medical diet food", shelter.Name == "Second Chance Paws" ? 5 : 9, "kg", 8, FoodCategoryId, MedicalDietFoodTypeId),
                new ResourceSeed(shelter.Name, "Clean blankets", 24, "pcs", 10, BlanketsCategoryId, null),
                new ResourceSeed(shelter.Name, "Disinfectant", shelter.Name == "North Star Animal Shelter" ? 3 : 11, "liters", 5, CleaningSuppliesCategoryId, null),
                new ResourceSeed(shelter.Name, "Adjustable leashes", 14, "pcs", 6, AccessoriesCategoryId, null),
                new ResourceSeed(shelter.Name, "Basic medicine kits", shelter.Name == "Hope Tails Rescue" ? 2 : 6, "pcs", 3, MedicineCategoryId, null)
            ]);
        }

        return resources;
    }



    private static IReadOnlyList<VolunteerProfileSeed> GetVolunteerProfiles()
    {
        return
        [
            new(
                VolunteerUserId,
                "Mara Dobre",
                VolunteerDemoEmail,
                "+40 700 000 301",
                "Happy Paws Shelter",
                "Dog walking, calm introductions, feeding support",
                "Weekday mornings and Saturday adoption events.",
                true),
            new(
                SecondVolunteerUserId,
                "Andrei Rusu",
                "volunteer2@mail.com",
                "+40 700 000 302",
                null,
                "Transport support, cleaning shifts, event desk support",
                "Usually available evenings and Sunday mornings.",
                true)
        ];
    }

    private static IReadOnlyList<VolunteerTaskSeed> GetVolunteerTasks()
    {
        var today = DateTime.UtcNow.Date;
        return
        [
            new(
                "Morning walk for Bella",
                "Happy Paws Shelter",
                "Bella",
                ShelterUserId,
                VolunteerUserId,
                VolunteerTaskCategory.DogWalking,
                VolunteerTaskStatus.Assigned,
                VolunteerTaskPriority.Normal,
                today.AddHours(8),
                today.AddHours(9),
                today.AddHours(9),
                "Zorilor quiet walking route",
                "Comfortable with calm, reserved dogs",
                "Bella prefers a steady pace and quiet streets.",
                null,
                null),
            new(
                "Prepare wet food portions",
                "Happy Paws Shelter",
                null,
                ShelterUserId,
                null,
                VolunteerTaskCategory.Feeding,
                VolunteerTaskStatus.Open,
                VolunteerTaskPriority.High,
                today.AddHours(12),
                today.AddHours(13),
                today.AddHours(13),
                "Happy Paws kitchen",
                "Food handling",
                "Portions are labelled by kennel row.",
                null,
                null),
            new(
                "Socialization support for Buddy",
                "Happy Paws Shelter",
                "Buddy",
                ShelterUserId,
                SecondVolunteerUserId,
                VolunteerTaskCategory.Socialization,
                VolunteerTaskStatus.InProgress,
                VolunteerTaskPriority.Normal,
                today.AddHours(-1),
                today.AddHours(1),
                today.AddHours(1),
                "Happy Paws training yard",
                "Confident handler, treat-based guidance",
                "Keep sessions short and structured.",
                "Buddy settled after the first few minutes.",
                null),
            new(
                "Vet transport support for Luna",
                "Hope Tails Rescue",
                "Luna",
                HopeTailsShelterUserId,
                SecondVolunteerUserId,
                VolunteerTaskCategory.Transport,
                VolunteerTaskStatus.Cancelled,
                VolunteerTaskPriority.Urgent,
                today.AddDays(1).AddHours(10),
                today.AddDays(1).AddHours(12),
                today.AddDays(1).AddHours(10),
                "Hope Tails Rescue reception",
                "Car transport, crate handling",
                "Original visit moved by the clinic.",
                null,
                null),
            new(
                "Adoption event welcome desk",
                "Friendly Tails Center",
                null,
                FriendlyTailsShelterUserId,
                SecondVolunteerUserId,
                VolunteerTaskCategory.AdoptionEventSupport,
                VolunteerTaskStatus.Completed,
                VolunteerTaskPriority.Low,
                today.AddDays(-2).AddHours(9),
                today.AddDays(-2).AddHours(12),
                today.AddDays(-2).AddHours(12),
                "Gheorgheni community hall",
                "Visitor greeting, basic adopter guidance",
                "Share dog profiles and direct visitors to staff for application questions.",
                null,
                "Visitor questions were recorded for the shelter team."),
            new(
                "Evening kennel cleaning support",
                "Green Yard Animal Care",
                null,
                GreenYardShelterUserId,
                null,
                VolunteerTaskCategory.Cleaning,
                VolunteerTaskStatus.Open,
                VolunteerTaskPriority.Normal,
                today.AddHours(18),
                today.AddHours(20),
                today.AddHours(20),
                "Grigorescu kennel block B",
                "Cleaning supplies, comfortable around large dogs",
                "Staff will prepare disinfectant and gloves.",
                null,
                null)
        ];
    }
    private static IReadOnlyList<TransferSeed> GetTransferRequests()
    {
        return
        [
            new(
                "Bruno",
                "Green Yard Animal Care",
                "Happy Paws Shelter",
                GreenYardShelterUserId,
                DogTransferStatus.Pending,
                DogTransferPriority.Normal,
                "Green Yard is close to capacity this week and Bruno may benefit from a calmer kennel while a potential adopter in Zorilor reviews his profile.",
                "Bruno travels calmly in a crate and should keep his usual feeding routine during the first days.",
                null,
                null,
                new DateTime(2026, 5, 21, 9, 15, 0, DateTimeKind.Utc),
                null,
                null,
                null,
                null,
                null),
            new(
                "Luna",
                "Hope Tails Rescue",
                "Happy Paws Shelter",
                HopeTailsShelterUserId,
                DogTransferStatus.Pending,
                DogTransferPriority.Urgent,
                "Luna needs access to Happy Paws' quieter recovery kennels while her skin follow-up is monitored.",
                "Please review her treatment notes before accepting because she needs a low-stress space.",
                null,
                "Admin flagged this as urgent because medical follow-up capacity is limited at the source shelter.",
                new DateTime(2026, 5, 21, 11, 40, 0, DateTimeKind.Utc),
                null,
                null,
                null,
                null,
                null),
            new(
                "Bella",
                "Happy Paws Shelter",
                "Safe Haven Dogs",
                ShelterUserId,
                DogTransferStatus.Pending,
                DogTransferPriority.High,
                "Safe Haven has a quieter environment and may be better placed to continue Bella's calm-adopter matching process.",
                "Bella is reserved, so please confirm availability before planning transport.",
                null,
                null,
                new DateTime(2026, 5, 20, 14, 10, 0, DateTimeKind.Utc),
                null,
                null,
                null,
                null,
                null),
            new(
                "Buddy",
                "Happy Paws Shelter",
                "Second Chance Paws",
                ShelterUserId,
                DogTransferStatus.Approved,
                DogTransferPriority.Normal,
                "Second Chance has kennel space for larger dogs and can support Buddy's structured outdoor routine.",
                "Buddy should travel after his morning walk and food should be sent with him for the first week.",
                "Approved. We can receive Buddy after 11:00 and will prepare a larger run.",
                null,
                new DateTime(2026, 5, 19, 8, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 19, 15, 20, 0, DateTimeKind.Utc),
                SecondChanceShelterUserId,
                null,
                null,
                null),
            new(
                "Nala",
                "Safe Haven Dogs",
                "Happy Paws Shelter",
                SafeHavenShelterUserId,
                DogTransferStatus.Completed,
                DogTransferPriority.Normal,
                "Nala was moved closer to a Zorilor adopter visit and to keep related adoption notes with the Happy Paws team.",
                "Nala settled well during previous car rides and prefers a quiet first hour after arrival.",
                "Approved after kennel check. Happy Paws confirmed space for a medium dog.",
                null,
                new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 13, 12, 30, 0, DateTimeKind.Utc),
                ShelterUserId,
                new DateTime(2026, 5, 14, 9, 45, 0, DateTimeKind.Utc),
                ShelterUserId,
                null),
            new(
                "Oscar",
                "Hope Tails Rescue",
                "Happy Paws Shelter",
                HopeTailsShelterUserId,
                DogTransferStatus.Rejected,
                DogTransferPriority.Low,
                "Hope Tails asked whether Oscar could move closer to a potential adopter, but the timing did not fit Happy Paws kennel capacity.",
                "Oscar can wait safely at Hope Tails if Happy Paws cannot receive him this week.",
                "Rejected for now because Happy Paws has limited medium-dog space this week.",
                null,
                new DateTime(2026, 5, 12, 9, 20, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 12, 16, 0, 0, DateTimeKind.Utc),
                ShelterUserId,
                null,
                null,
                null),
            new(
                "Max",
                "Happy Paws Shelter",
                "North Star Animal Shelter",
                ShelterUserId,
                DogTransferStatus.Cancelled,
                DogTransferPriority.High,
                "North Star was considered for Max because of outdoor space, but the source shelter paused the request after adopter interest changed.",
                "Cancelled after internal review; keep Max listed at Happy Paws for now.",
                null,
                null,
                new DateTime(2026, 5, 10, 13, 30, 0, DateTimeKind.Utc),
                null,
                null,
                null,
                null,
                new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc))
        ];
    }
    private static IReadOnlyList<AdoptionRequestSeed> GetAdoptionRequests()
    {
        return
        [
            new(
                "Bella",
                AdopterUserId,
                AdoptionRequestStatus.VisitConfirmed,
                AdoptionVisitStatus.Confirmed,
                new DateTime(2026, 5, 18, 13, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 16, 10, 30, 0, DateTimeKind.Utc),
                ShelterUserId,
                "Bella seems calm enough for my apartment and I would like to discuss how she meets cats.",
                "I am looking for a calm companion and can offer daily walks and a predictable routine.",
                3,
                "I have a resident cat, so I would like advice about slow introductions.",
                "Visit confirmed. Ask about cat introductions and evening routine.",
                new DateTime(2026, 5, 14, 12, 20, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 16, 10, 30, 0, DateTimeKind.Utc)),
            new(
                "Mira",
                AdopterUserId,
                AdoptionRequestStatus.Pending,
                AdoptionVisitStatus.Requested,
                new DateTime(2026, 5, 23, 11, 0, 0, DateTimeKind.Utc),
                null,
                null,
                "Mira sounds gentle around cats and calm dogs, which may work for our senior dog.",
                "We want a respectful dog who will not overwhelm an older dog recovering at home.",
                4,
                "Our children are older and can follow calm introduction rules.",
                "Review senior-dog fit before confirming the visit.",
                new DateTime(2026, 5, 19, 9, 40, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 19, 9, 40, 0, DateTimeKind.Utc)),
            new(
                "Max",
                AdopterUserId,
                AdoptionRequestStatus.Pending,
                AdoptionVisitStatus.Requested,
                new DateTime(2026, 5, 24, 15, 0, 0, DateTimeKind.Utc),
                null,
                null,
                "I enjoy longer walks and training games, and Max seems like a good active match.",
                "I can provide outdoor time and structured activity during the week.",
                5,
                "No cats or small pets at home.",
                "Ask adopter about daily schedule and exercise plan.",
                new DateTime(2026, 5, 20, 8, 15, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 20, 8, 15, 0, DateTimeKind.Utc)),
            new(
                "Daisy",
                AdopterUserId,
                AdoptionRequestStatus.Accepted,
                AdoptionVisitStatus.Completed,
                new DateTime(2026, 4, 26, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 23, 10, 0, 0, DateTimeKind.Utc),
                ShelterUserId,
                "Daisy met our family calmly and our children followed the shelter guidance.",
                "We wanted a gentle medium-sized dog for a family routine.",
                2,
                "Transition plan completed successfully.",
                "Adoption finalized after successful visit.",
                new DateTime(2026, 4, 21, 16, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 28, 13, 0, 0, DateTimeKind.Utc)),
            new(
                "Rocky",
                AdopterUserId,
                AdoptionRequestStatus.Rejected,
                AdoptionVisitStatus.Cancelled,
                new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Utc),
                null,
                null,
                "Rocky is beautiful, but I was unsure whether his energy fits my apartment.",
                "I was considering a larger dog but need a calm home fit.",
                6,
                "The shelter suggested a quieter dog may be better.",
                "Rejected because current home routine did not match Rocky's activity needs.",
                new DateTime(2026, 5, 11, 17, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 13, 9, 30, 0, DateTimeKind.Utc)),
            new(
                "Toby",
                AdopterUserId,
                AdoptionRequestStatus.Cancelled,
                AdoptionVisitStatus.Cancelled,
                new DateTime(2026, 5, 17, 10, 30, 0, DateTimeKind.Utc),
                null,
                null,
                "Toby sounded very sweet, but my schedule changed.",
                "I wanted to meet a quieter small dog.",
                4,
                "Cancelled by adopter before visit confirmation.",
                "No shelter action needed.",
                new DateTime(2026, 5, 12, 13, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 13, 16, 0, 0, DateTimeKind.Utc)),
            new(
                "Sasha",
                AdopterUserId,
                AdoptionRequestStatus.VisitConfirmed,
                AdoptionVisitStatus.Confirmed,
                new DateTime(2026, 5, 22, 12, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 20, 11, 0, 0, DateTimeKind.Utc),
                ShelterUserId,
                "I would like to ask about Sasha's slow cat introductions and patient adopter needs.",
                "I can offer calm routines and supervised introductions.",
                3,
                "Resident cat at home.",
                "Visit confirmed. Discuss cat plan and confidence-building routine.",
                new DateTime(2026, 5, 18, 15, 40, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 20, 11, 0, 0, DateTimeKind.Utc))
        ];
    }

    private sealed record AdopterProfileSeed(
        string UserId,
        string FullName,
        string Address,
        string City,
        string PhoneNumber,
        HousingType HousingType,
        bool HasYard,
        bool HasOtherPets,
        bool HasChildren,
        string ExperienceWithDogs,
        string AdditionalNotes);

    private sealed record ShelterSeed(
        string Name,
        string Description,
        string Address,
        string Neighborhood,
        string PhoneNumber,
        string Email,
        double Latitude,
        double Longitude,
        string? ApplicationUserId = null,
        TimeSpan? VisitStartTime = null,
        TimeSpan? VisitEndTime = null,
        bool WeekdayVisits = true,
        bool SaturdayVisits = false);

    private sealed record DogSeed(
        string ShelterName,
        string Name,
        string Breed,
        string CoatColor,
        int AgeYears,
        int AgeMonths,
        DogSize Size,
        DogStatus Status,
        string Description,
        string BehaviorDescription,
        string MedicalStatus,
        int PreferredFoodTypeId,
        int DailyFoodAmountGrams,
        DateTime? AdoptedAt = null,
        string? SuccessStoryText = null);

    private sealed record MedicalSeed(
        string DogName,
        string? VaccineName,
        string TreatmentDescription,
        DateTime RecordDate,
        string Notes);

    private sealed record ResourceSeed(
        string ShelterName,
        string Name,
        int Quantity,
        string Unit,
        int LowStockThreshold,
        int ResourceCategoryId,
        int? FoodTypeId);


    private sealed record VolunteerProfileSeed(
        string UserId,
        string DisplayName,
        string Email,
        string? PhoneNumber,
        string? PreferredShelterName,
        string? Skills,
        string? AvailabilityNotes,
        bool IsActive);

    private sealed record VolunteerTaskSeed(
        string Title,
        string ShelterName,
        string? DogName,
        string CreatedByUserId,
        string? AssignedVolunteerUserId,
        VolunteerTaskCategory Category,
        VolunteerTaskStatus Status,
        VolunteerTaskPriority Priority,
        DateTime ScheduledStartUtc,
        DateTime ScheduledEndUtc,
        DateTime? DueAtUtc,
        string? Location,
        string? RequiredSkills,
        string? ShelterNotes,
        string? VolunteerNotes,
        string? CompletionNotes);
    private sealed record TransferSeed(
        string DogName,
        string SourceShelterName,
        string DestinationShelterName,
        string RequestedByUserId,
        DogTransferStatus Status,
        DogTransferPriority Priority,
        string Reason,
        string? SourceShelterNotes,
        string? DestinationShelterResponseNotes,
        string? AdminNotes,
        DateTime RequestedAtUtc,
        DateTime? RespondedAtUtc,
        string? RespondedByUserId,
        DateTime? CompletedAtUtc,
        string? CompletedByUserId,
        DateTime? CancelledAtUtc);

    private sealed record AdoptionRequestSeed(
        string DogName,
        string AdopterId,
        AdoptionRequestStatus Status,
        AdoptionVisitStatus VisitStatus,
        DateTime? PreferredVisitDateTime,
        DateTime? VisitConfirmedAt,
        string? VisitConfirmedByUserId,
        string Message,
        string ReasonForAdoption,
        int HoursAlonePerDay,
        string AdditionalInformation,
        string ShelterInternalNotes,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private sealed record SeedUser(
        string Id,
        string Email,
        string Password,
        string FullName,
        string Role,
        string[] LegacyEmails);
}





