using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Shelter>()
            .HasOne(s => s.OwnerUser)
            .WithMany(u => u.Shelters)
            .HasForeignKey(s => s.OwnerUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Dog>()
            .HasOne(d => d.Shelter)
            .WithMany(s => s.Dogs)
            .HasForeignKey(d => d.ShelterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DogImage>()
            .HasOne(i => i.Dog)
            .WithMany(d => d.DogImages)
            .HasForeignKey(i => i.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MedicalRecord>()
            .HasOne(m => m.Dog)
            .WithMany(d => d.MedicalRecords)
            .HasForeignKey(m => m.DogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AdoptionRequest>()
            .HasOne(a => a.Dog)
            .WithMany(d => d.AdoptionRequests)
            .HasForeignKey(a => a.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AdoptionRequest>()
            .HasOne(a => a.AdopterUser)
            .WithMany(u => u.AdoptionRequests)
            .HasForeignKey(a => a.AdopterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FavoriteDog>()
            .HasIndex(f => new { f.UserId, f.DogId })
            .IsUnique();

        builder.Entity<FavoriteDog>()
            .HasOne(f => f.User)
            .WithMany(u => u.FavoriteDogs)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FavoriteDog>()
            .HasOne(f => f.Dog)
            .WithMany(d => d.FavoriteDogs)
            .HasForeignKey(f => f.DogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ResourceStock>()
            .HasOne(r => r.Shelter)
            .WithMany(s => s.ResourceStocks)
            .HasForeignKey(r => r.ShelterId)
            .OnDelete(DeleteBehavior.Restrict);

        SeedDomainData(builder);
    }

    private static void SeedDomainData(ModelBuilder builder)
    {
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.Entity<Shelter>().HasData(new Shelter
        {
            Id = 1,
            Name = "PawConnect Demo Shelter",
            Address = "123 Shelter Street, Bucharest",
            PhoneNumber = "+40 700 000 001",
            Email = "shelter@pawconnect.test",
            Description = "A sample shelter used for development and demonstrations."
        });

        builder.Entity<Dog>().HasData(
            new Dog
            {
                Id = 1,
                Name = "Max",
                Breed = "Mixed Breed",
                Age = 3,
                Size = DogSize.Medium,
                Status = DogStatus.Available,
                Description = "Friendly and playful dog looking for an active family.",
                ShelterId = 1,
                CreatedAt = createdAt
            },
            new Dog
            {
                Id = 2,
                Name = "Bella",
                Breed = "Labrador Mix",
                Age = 5,
                Size = DogSize.Large,
                Status = DogStatus.Reserved,
                Description = "Calm, affectionate, and good with people.",
                ShelterId = 1,
                CreatedAt = createdAt
            },
            new Dog
            {
                Id = 3,
                Name = "Luna",
                Breed = "Terrier Mix",
                Age = 1,
                Size = DogSize.Small,
                Status = DogStatus.InTreatment,
                Description = "Young dog currently receiving basic medical care.",
                ShelterId = 1,
                CreatedAt = createdAt
            });

        builder.Entity<DogImage>().HasData(
            new DogImage { Id = 1, DogId = 1, ImageUrl = "https://placehold.co/800x500?text=Max", Caption = "Max main photo", IsMainImage = true },
            new DogImage { Id = 2, DogId = 2, ImageUrl = "https://placehold.co/800x500?text=Bella", Caption = "Bella main photo", IsMainImage = true },
            new DogImage { Id = 3, DogId = 3, ImageUrl = "https://placehold.co/800x500?text=Luna", Caption = "Luna main photo", IsMainImage = true });

        builder.Entity<ResourceStock>().HasData(
            new ResourceStock { Id = 1, ShelterId = 1, Name = "Dry Food", Quantity = 50, Unit = "kg", MinimumQuantity = 15 },
            new ResourceStock { Id = 2, ShelterId = 1, Name = "Blankets", Quantity = 20, Unit = "pcs", MinimumQuantity = 5 },
            new ResourceStock { Id = 3, ShelterId = 1, Name = "Medicine Kits", Quantity = 8, Unit = "pcs", MinimumQuantity = 3 });
    }
}
