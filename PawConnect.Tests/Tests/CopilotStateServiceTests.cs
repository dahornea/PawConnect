using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Tests.Tests;

public class CopilotStateServiceTests
{
    [Fact]
    public void SaveState_StoresUserFacingCopilotState()
    {
        var service = new CopilotStateService();
        var dog = new Dog { Id = 42, Name = "Bella", Breed = "Labrador Mix", Status = DogStatus.Available };
        var response = new AdoptionCopilotResponse(
            "Bella looks promising.",
            [
                new AdoptionCopilotDogResult(
                    dog.Id,
                    dog,
                    86,
                    "Good match",
                    ["Apartment-friendly size"],
                    "View details.",
                    2.4,
                    true)
            ],
            true,
            true,
            true,
            null,
            [new AdoptionCopilotConstraint("Size", "Medium")]);

        service.SaveState("user-1", "calm apartment dog", response);

        var state = service.GetState("user-1");

        Assert.NotNull(state);
        Assert.Equal("calm apartment dog", state.LastQuery);
        Assert.Equal("Bella looks promising.", state.LastAssistantMessage);
        Assert.True(state.LastUsedOpenAi);
        Assert.True(state.LastUsedSemanticSearch);
        Assert.True(state.LastUsedToolCalling);
        Assert.Single(state.LastAppliedConstraints);
        Assert.Single(state.LastResults);
        Assert.Equal(42, state.LastResults[0].DogId);
        Assert.Equal("Apartment-friendly size", state.LastResults[0].Reasons.Single());
    }

    [Fact]
    public void GetState_DoesNotReturnAnotherUsersState()
    {
        var service = new CopilotStateService();
        var response = new AdoptionCopilotResponse("No matches.", [], false, false);

        service.SaveState("user-1", "friendly dog", response);

        Assert.Null(service.GetState("user-2"));
    }

    [Fact]
    public void ClearState_RemovesSavedStateForCurrentUser()
    {
        var service = new CopilotStateService();
        var response = new AdoptionCopilotResponse("No matches.", [], false, false);
        service.SaveState("user-1", "friendly dog", response);

        service.ClearState("user-1");

        Assert.Null(service.GetState("user-1"));
    }

    [Fact]
    public void SaveState_StoresMatchedCriteria()
    {
        var service = new CopilotStateService();
        var dog = new Dog { Id = 7, Name = "Max", Breed = "Mixed Breed", Status = DogStatus.Available };
        var response = new AdoptionCopilotResponse(
            "Max matches.",
            [
                new AdoptionCopilotDogResult(
                    dog.Id,
                    dog,
                    80,
                    "Good match",
                    ["Size matches your search"],
                    "View details.",
                    MatchedCriteria: [new AdoptionCopilotConstraint("Size", "Medium")])
            ],
            false,
            false,
            AppliedConstraints: [new AdoptionCopilotConstraint("Size", "Medium")]);

        service.SaveState("user-1", "medium dog", response);

        var state = service.GetState("user-1");

        Assert.NotNull(state);
        Assert.Single(state.LastResults[0].MatchedCriteria);
        Assert.Equal("Size", state.LastResults[0].MatchedCriteria[0].Label);
        Assert.Equal("Medium", state.LastResults[0].MatchedCriteria[0].Value);
    }
}
