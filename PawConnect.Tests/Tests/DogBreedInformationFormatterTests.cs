using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Tests.Tests;

public class DogBreedInformationFormatterTests
{
    [Fact]
    public void GetGeneralNote_WhenBreedHasNoNote_ReturnsFallback()
    {
        var dog = new Dog
        {
            Name = "Scout",
            DogBreed = new DogBreed { Name = "Whippet" }
        };

        Assert.Equal(DogBreedInformationFormatter.FallbackNote, DogBreedInformationFormatter.GetGeneralNote(dog));
        Assert.False(DogBreedInformationFormatter.HasBreedNote(dog));
    }

    [Fact]
    public void GetDisclaimer_ForMixedBreed_EmphasizesIndividualBehavior()
    {
        var dog = new Dog
        {
            Name = "Bella",
            DogBreed = DogBreedSeedData.CreateSeedEntities().First(breed => breed.Name == "Labrador Retriever"),
            IsMixedBreed = true
        };

        var disclaimer = DogBreedInformationFormatter.GetDisclaimer(dog);

        Assert.Contains("mixed-breed", disclaimer);
        Assert.Contains("medical history", disclaimer);
        Assert.DoesNotContain("diagnosis", disclaimer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetHealthNote_WhenBreedHasNoHealthNote_ReturnsMedicalRecordsFallback()
    {
        var dog = new Dog
        {
            Name = "Scout",
            DogBreed = new DogBreed { Name = "Whippet" }
        };

        var note = DogBreedInformationFormatter.GetHealthNote(dog);

        Assert.Equal(DogBreedInformationFormatter.FallbackHealthNote, note);
        Assert.Contains("medical records", note);
    }

    [Fact]
    public void GetHealthNote_WhenBreedHasHealthNote_IsEducationalNotDiagnostic()
    {
        var dog = new Dog
        {
            Name = "Bella",
            DogBreed = DogBreedSeedData.CreateSeedEntities().First(breed => breed.Name == "Labrador Retriever")
        };

        var note = DogBreedInformationFormatter.GetHealthNote(dog);

        Assert.Contains("may be more prone", note);
        Assert.Contains("does not mean this dog has", note);
    }

    [Fact]
    public void GetHealthConsiderationItems_ForLabrador_ReturnsScannableItems()
    {
        var dog = new Dog
        {
            Name = "Bella",
            DogBreed = DogBreedSeedData.CreateSeedEntities().First(breed => breed.Name == "Labrador Retriever")
        };

        var items = DogBreedInformationFormatter.GetHealthConsiderationItems(dog);

        Assert.Contains("joint issues", items);
        Assert.Contains("weight gain", items);
        Assert.Contains("ear problems", items);
        Assert.Contains("does not mean Bella has these conditions", DogBreedInformationFormatter.GetHealthFollowUp(dog));
    }

    [Fact]
    public void GetGeneralNotes_ForKnownMixedBreedPair_ReturnsNotesForBothBreeds()
    {
        var breeds = DogBreedSeedData.CreateSeedEntities();
        var dog = new Dog
        {
            Name = "Bailey",
            DogBreed = breeds.First(breed => breed.Name == "Labrador Retriever"),
            SecondaryBreed = breeds.First(breed => breed.Name == "Border Collie"),
            IsMixedBreed = true
        };

        var notes = DogBreedInformationFormatter.GetGeneralNotes(dog);

        Assert.Equal(2, notes.Count);
        Assert.Contains(notes, note => note.Contains("Labrador Retriever-type dogs"));
        Assert.Contains(notes, note => note.Contains("Border Collie-type dogs"));
    }

    [Fact]
    public void GetHealthConsiderationItems_ForKnownMixedBreedPair_CombinesBreedLevelItems()
    {
        var breeds = DogBreedSeedData.CreateSeedEntities();
        var dog = new Dog
        {
            Name = "Bailey",
            DogBreed = breeds.First(breed => breed.Name == "Labrador Retriever"),
            SecondaryBreed = breeds.First(breed => breed.Name == "Border Collie"),
            IsMixedBreed = true
        };

        var items = DogBreedInformationFormatter.GetHealthConsiderationItems(dog);

        Assert.Contains("joint issues", items);
        Assert.Contains("weight gain", items);
        Assert.Contains("high exercise needs", items);
        Assert.Contains("general considerations only", DogBreedInformationFormatter.GetHealthFollowUp(dog));
    }

    [Fact]
    public void GetCareContext_UsesDogNameForActivityReminder()
    {
        var dog = new Dog
        {
            Name = "Bella",
            DogBreed = DogBreedSeedData.CreateSeedEntities().First(breed => breed.Name == "Labrador Retriever")
        };

        var careContext = DogBreedInformationFormatter.GetCareContext(dog);

        Assert.Equal("May enjoy regular exercise and enrichment. Check Bella's own activity level before assuming fit.", careContext);
    }

    [Fact]
    public void GetImportantNote_PointsToActualMedicalRecords()
    {
        var dog = new Dog { Name = "Bella" };

        var note = DogBreedInformationFormatter.GetImportantNote(dog);

        Assert.Contains("not a diagnosis", note);
        Assert.Contains("Bella's actual medical status and medical records are the source of truth", note);
    }

    [Fact]
    public void SeedData_MainDemoBreedIncludesCarefulNote()
    {
        var breed = DogBreedSeedData.CreateSeedEntities().First(breed => breed.Name == "Border Collie");

        Assert.Contains("commonly", breed.GeneralDescription);
        Assert.Contains("individual energy level matters", breed.CareNotes);
        Assert.Contains("medical records should be reviewed", breed.CommonHealthConsiderations);
    }
}
