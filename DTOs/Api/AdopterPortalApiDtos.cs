using System.ComponentModel.DataAnnotations;
using PawConnect.Entities;

namespace PawConnect.DTOs.Api;

public sealed record AdopterPortalLoginRequest(
    [param: Required, EmailAddress] string Email,
    [param: Required] string Password,
    bool RememberMe = false);

public sealed record AdopterPortalUserApiDto(
    string Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed record AntiforgeryTokenApiDto(string Token, string HeaderName);

public sealed record NotificationUnreadCountApiDto(int Count);

public sealed record AdopterProfileApiDto(
    string FullName,
    string? ProfileImageUrl,
    string? Address,
    string City,
    string? PhoneNumber,
    HousingType HousingType,
    bool HasYard,
    bool HasOtherPets,
    bool HasChildren,
    string? ExperienceWithDogs,
    string? AdditionalNotes);

public sealed record UpdateAdopterProfileApiRequest(
    [param: Required, StringLength(120)] string FullName,
    [param: Url, StringLength(500)] string? ProfileImageUrl,
    [param: StringLength(160)] string? Address,
    [param: Required, StringLength(80)] string City,
    [param: Phone, StringLength(30)] string? PhoneNumber,
    HousingType HousingType,
    bool HasYard,
    bool HasOtherPets,
    bool HasChildren,
    [param: StringLength(1000)] string? ExperienceWithDogs,
    [param: StringLength(1000)] string? AdditionalNotes);

public sealed record AdoptionCopilotApiRequest(
    [param: Required, StringLength(1000, MinimumLength = 3)] string Message);

public sealed record AdoptionCopilotResultApiDto(
    DogListItemApiDto Dog,
    int ScorePercent,
    string MatchLabel,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> DisplayTags,
    IReadOnlyList<string> CautionTags,
    string SuggestedNextAction,
    double? DistanceKm);

public sealed record AdoptionCopilotResponseApiDto(
    string AssistantMessage,
    IReadOnlyList<AdoptionCopilotResultApiDto> Results,
    IReadOnlyList<AdoptionCopilotConstraintApiDto> AppliedConstraints,
    bool UsedAiEnhancement,
    bool UsedSemanticSearch,
    string? FallbackReason);

public sealed record AdoptionCopilotConstraintApiDto(string Label, string Value);
