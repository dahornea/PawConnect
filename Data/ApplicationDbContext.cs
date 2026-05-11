using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PawConnect.Entities;

namespace PawConnect.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<Shelter> Shelters => Set<Shelter>();

    public DbSet<Dog> Dogs => Set<Dog>();

    public DbSet<DogImage> DogImages => Set<DogImage>();

    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();

    public DbSet<AdoptionRequest> AdoptionRequests => Set<AdoptionRequest>();

    public DbSet<FavoriteDog> FavoriteDogs => Set<FavoriteDog>();

    public DbSet<ResourceStock> ResourceStocks => Set<ResourceStock>();

    public DbSet<ResourceCategory> ResourceCategories => Set<ResourceCategory>();

    public DbSet<FoodType> FoodTypes => Set<FoodType>();

    public DbSet<AdopterProfile> AdopterProfiles => Set<AdopterProfile>();

    public DbSet<DogStatusHistory> DogStatusHistories => Set<DogStatusHistory>();

    public DbSet<RecentlyViewedDog> RecentlyViewedDogs => Set<RecentlyViewedDog>();

    public DbSet<ShelterRegistrationRequest> ShelterRegistrationRequests => Set<ShelterRegistrationRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Shelter>()
            .HasOne(s => s.ApplicationUser)
            .WithOne(u => u.Shelter)
            .HasForeignKey<Shelter>(s => s.ApplicationUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AdopterProfile>()
            .HasOne(p => p.ApplicationUser)
            .WithOne(u => u.AdopterProfile)
            .HasForeignKey<AdopterProfile>(p => p.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AdopterProfile>()
            .HasIndex(p => p.ApplicationUserId)
            .IsUnique();

        builder.Entity<Dog>()
            .HasOne(d => d.Shelter)
            .WithMany(s => s.Dogs)
            .HasForeignKey(d => d.ShelterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Dog>()
            .HasOne(d => d.PreferredFoodType)
            .WithMany(f => f.Dogs)
            .HasForeignKey(d => d.PreferredFoodTypeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DogImage>()
            .HasOne(i => i.Dog)
            .WithMany(d => d.Images)
            .HasForeignKey(i => i.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MedicalRecord>()
            .HasOne(m => m.Dog)
            .WithMany(d => d.MedicalRecords)
            .HasForeignKey(m => m.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DogStatusHistory>()
            .HasOne(h => h.Dog)
            .WithMany(d => d.StatusHistories)
            .HasForeignKey(h => h.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DogStatusHistory>()
            .HasOne(h => h.ChangedByUser)
            .WithMany(u => u.DogStatusHistories)
            .HasForeignKey(h => h.ChangedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<DogStatusHistory>()
            .Property(h => h.ChangedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<AdoptionRequest>()
            .HasOne(a => a.Dog)
            .WithMany(d => d.AdoptionRequests)
            .HasForeignKey(a => a.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AdoptionRequest>()
            .HasOne(a => a.Adopter)
            .WithMany(u => u.AdoptionRequests)
            .HasForeignKey(a => a.AdopterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AdoptionRequest>()
            .HasIndex(a => new { a.AdopterId, a.DogId })
            .HasFilter("[Status] = 0")
            .IsUnique();

        builder.Entity<AdoptionRequest>()
            .Property(a => a.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<AdoptionRequest>()
            .Property(a => a.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<FavoriteDog>()
            .HasIndex(f => new { f.AdopterId, f.DogId })
            .IsUnique();

        builder.Entity<FavoriteDog>()
            .HasOne(f => f.Adopter)
            .WithMany(u => u.FavoriteDogs)
            .HasForeignKey(f => f.AdopterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FavoriteDog>()
            .HasOne(f => f.Dog)
            .WithMany(d => d.FavoriteDogs)
            .HasForeignKey(f => f.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FavoriteDog>()
            .Property(f => f.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<RecentlyViewedDog>()
            .HasIndex(v => new { v.AdopterId, v.DogId })
            .IsUnique();

        builder.Entity<RecentlyViewedDog>()
            .HasOne(v => v.Adopter)
            .WithMany(u => u.RecentlyViewedDogs)
            .HasForeignKey(v => v.AdopterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RecentlyViewedDog>()
            .HasOne(v => v.Dog)
            .WithMany(d => d.RecentlyViewedDogs)
            .HasForeignKey(v => v.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RecentlyViewedDog>()
            .Property(v => v.ViewedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<ResourceStock>()
            .HasOne(r => r.Shelter)
            .WithMany(s => s.ResourceStocks)
            .HasForeignKey(r => r.ShelterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ResourceStock>()
            .HasOne(r => r.ResourceCategory)
            .WithMany(c => c.ResourceStocks)
            .HasForeignKey(r => r.ResourceCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ResourceStock>()
            .HasOne(r => r.FoodType)
            .WithMany(f => f.ResourceStocks)
            .HasForeignKey(r => r.FoodTypeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ResourceStock>()
            .Property(r => r.LastUpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<ShelterRegistrationRequest>()
            .HasIndex(r => r.Email)
            .HasFilter("[Status] = 0")
            .IsUnique();

        builder.Entity<ShelterRegistrationRequest>()
            .HasOne(r => r.ReviewedByUser)
            .WithMany(u => u.ReviewedShelterRegistrationRequests)
            .HasForeignKey(r => r.ReviewedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ShelterRegistrationRequest>()
            .HasOne(r => r.CreatedShelter)
            .WithMany()
            .HasForeignKey(r => r.CreatedShelterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ShelterRegistrationRequest>()
            .Property(r => r.SubmittedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        SeedLookupData(builder);
    }

    private static void SeedLookupData(ModelBuilder builder)
    {
        builder.Entity<ResourceCategory>().HasData(
            new ResourceCategory { Id = 1, Name = "Food", Description = "Food supplies for dogs." },
            new ResourceCategory { Id = 2, Name = "Medicine", Description = "Medication and medical supplies." },
            new ResourceCategory { Id = 3, Name = "Blankets", Description = "Blankets and bedding materials." },
            new ResourceCategory { Id = 4, Name = "Cleaning Supplies", Description = "Cleaning and sanitation products." },
            new ResourceCategory { Id = 5, Name = "Accessories", Description = "Leashes, collars, bowls, and similar items." },
            new ResourceCategory { Id = 6, Name = "Other", Description = "General shelter resources." });

        builder.Entity<FoodType>().HasData(
            new FoodType { Id = 1, Name = "Adult dry food", Description = "Standard dry food for adult dogs." },
            new FoodType { Id = 2, Name = "Puppy food", Description = "Food suitable for puppies." },
            new FoodType { Id = 3, Name = "Senior food", Description = "Food suitable for older dogs." },
            new FoodType { Id = 4, Name = "Wet food", Description = "Canned or wet dog food." },
            new FoodType { Id = 5, Name = "Medical diet food", Description = "Special diet food recommended by a veterinarian." });
    }
}
