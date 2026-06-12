using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Tests.Tests;

public class DogBreedFormatterTests
{
    private static readonly IReadOnlyList<DogBreed> Breeds = DogBreedSeedData.CreateSeedEntities();

    [Fact]
    public void Format_SelectedBreed_ReturnsBreedName()
    {
        var dog = new Dog
        {
            DogBreed = Breeds.First(breed => breed.Name == "Labrador Retriever")
        };

        Assert.Equal("Labrador Retriever", DogBreedFormatter.Format(dog));
    }

    [Fact]
    public void Format_SelectedPrimaryAndSecondaryMixedBreed_ReturnsSpecificMixedDisplayName()
    {
        var dog = new Dog
        {
            DogBreed = Breeds.First(breed => breed.Name == "Labrador Retriever"),
            SecondaryBreed = Breeds.First(breed => breed.Name == "Border Collie"),
            IsMixedBreed = true
        };

        Assert.Equal("Labrador Retriever \u00d7 Border Collie Mix", DogBreedFormatter.Format(dog));
    }

    [Fact]
    public void Format_SpecialLookupBreed_DoesNotAppendMix()
    {
        var dog = new Dog
        {
            DogBreed = Breeds.First(breed => breed.Name == "Unknown"),
            IsMixedBreed = true
        };

        Assert.Equal("Unknown", DogBreedFormatter.Format(dog));
    }

    [Fact]
    public void Format_CustomBreed_ReturnsCustomBreed()
    {
        var dog = new Dog
        {
            CustomBreedName = "Romanian Street Dog",
            IsMixedBreed = true
        };

        Assert.Equal("Romanian Street Dog Mix", DogBreedFormatter.Format(dog));
    }

    [Fact]
    public void Parse_KnownBreedText_MapsToLookup()
    {
        var result = DogBreedFormatter.Parse("labrador mix", Breeds);
        var breed = Breeds.FirstOrDefault(item => item.Id == result.DogBreedId);

        Assert.Equal("Labrador Retriever Mix", result.DisplayName);
        Assert.Equal("Labrador Retriever", breed?.Name);
        Assert.True(result.IsMixedBreed);
        Assert.Null(result.SecondaryBreedId);
        Assert.Null(result.CustomBreedName);
    }

    [Fact]
    public void Parse_KnownMixedBreedPair_MapsPrimaryAndSecondaryBreeds()
    {
        var result = DogBreedFormatter.Parse("Labrador Retriever x Border Collie", Breeds);
        var primary = Breeds.FirstOrDefault(item => item.Id == result.DogBreedId);
        var secondary = Breeds.FirstOrDefault(item => item.Id == result.SecondaryBreedId);

        Assert.Equal("Labrador Retriever", primary?.Name);
        Assert.Equal("Border Collie", secondary?.Name);
        Assert.True(result.IsMixedBreed);
        Assert.Equal("Labrador Retriever \u00d7 Border Collie Mix", result.DisplayName);
    }

    [Fact]
    public void Parse_UnmatchedBreedText_KeepsCustomName()
    {
        var result = DogBreedFormatter.Parse("Rare Mountain Dog Mix", Breeds);

        Assert.Null(result.DogBreedId);
        Assert.True(result.IsMixedBreed);
        Assert.Equal("Rare Mountain Dog", result.CustomBreedName);
        Assert.Equal("Rare Mountain Dog Mix", result.DisplayName);
    }
}
