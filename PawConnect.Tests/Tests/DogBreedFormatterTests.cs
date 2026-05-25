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
    public void Format_SelectedBreedMarkedMixed_ReturnsMixedDisplayName()
    {
        var dog = new Dog
        {
            DogBreed = Breeds.First(breed => breed.Name == "Poodle"),
            IsMixedBreed = true
        };

        Assert.Equal("Poodle Mix", DogBreedFormatter.Format(dog));
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

    [Theory]
    [InlineData("Mixed Breed", "Mixed Breed")]
    [InlineData("Unknown", "Unknown")]
    public void Format_SpecialLookupBreed_DoesNotAppendMix(string breedName, string expected)
    {
        var dog = new Dog
        {
            DogBreed = Breeds.First(breed => breed.Name == breedName),
            IsMixedBreed = true
        };

        Assert.Equal(expected, DogBreedFormatter.Format(dog));
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Mixed Breed")]
    public void Format_SecondarySpecialBreed_DoesNotCreateAwkwardMixedDisplayName(string secondaryBreedName)
    {
        var dog = new Dog
        {
            DogBreed = Breeds.First(breed => breed.Name == "Labrador Retriever"),
            SecondaryBreed = Breeds.First(breed => breed.Name == secondaryBreedName),
            IsMixedBreed = true
        };

        Assert.Equal("Labrador Retriever Mix", DogBreedFormatter.Format(dog));
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

    [Theory]
    [InlineData("labrador mix", "Labrador Retriever Mix", "Labrador Retriever", true)]
    [InlineData("Mixed", "Mixed Breed", "Mixed Breed", false)]
    [InlineData("unknown", "Unknown", "Unknown", false)]
    [InlineData("Setter Mix", "Setter Mix", "Setter", true)]
    [InlineData("pitbull mix", "Pit Bull Terrier Mix", "Pit Bull Terrier", true)]
    [InlineData("Westie", "West Highland White Terrier", "West Highland White Terrier", false)]
    [InlineData("St. Bernard", "Saint Bernard", "Saint Bernard", false)]
    [InlineData("Shar-Pei mix", "Shar Pei Mix", "Shar Pei", true)]
    public void Parse_KnownBreedText_MapsToLookup(string input, string expectedDisplay, string expectedBreedName, bool expectedMixed)
    {
        var result = DogBreedFormatter.Parse(input, Breeds);
        var breed = Breeds.FirstOrDefault(item => item.Id == result.DogBreedId);

        Assert.Equal(expectedDisplay, result.DisplayName);
        Assert.Equal(expectedBreedName, breed?.Name);
        Assert.Equal(expectedMixed, result.IsMixedBreed);
        Assert.Null(result.SecondaryBreedId);
        Assert.Null(result.CustomBreedName);
    }

    [Theory]
    [InlineData("Labrador Retriever x Border Collie")]
    [InlineData("Labrador Retriever \u00d7 Border Collie")]
    [InlineData("Labrador Retriever / Border Collie")]
    [InlineData("Labrador Border Collie Mix")]
    public void Parse_KnownMixedBreedPair_MapsPrimaryAndSecondaryBreeds(string input)
    {
        var result = DogBreedFormatter.Parse(input, Breeds);
        var primary = Breeds.FirstOrDefault(item => item.Id == result.DogBreedId);
        var secondary = Breeds.FirstOrDefault(item => item.Id == result.SecondaryBreedId);

        Assert.Equal("Labrador Retriever", primary?.Name);
        Assert.Equal("Border Collie", secondary?.Name);
        Assert.True(result.IsMixedBreed);
        Assert.Null(result.CustomBreedName);
        Assert.Equal("Labrador Retriever \u00d7 Border Collie Mix", result.DisplayName);
    }

    [Fact]
    public void SeedData_IncludesExpandedBreedCatalog()
    {
        Assert.Contains(Breeds, breed => breed.Name == "Pit Bull Terrier");
        Assert.Contains(Breeds, breed => breed.Name == "West Highland White Terrier");
        Assert.Contains(Breeds, breed => breed.Name == "Saint Bernard");
        Assert.Contains(Breeds, breed => breed.Name == "Miniature Poodle");
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
