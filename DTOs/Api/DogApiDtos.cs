using PawConnect.Entities;

namespace PawConnect.DTOs.Api;

public sealed record DogListItemApiDto(
    int Id,
    string Name,
    string Breed,
    string? CoatColor,
    int AgeYears,
    int AgeMonths,
    string AgeDisplay,
    DogSize Size,
    DogStatus Status,
    string Location,
    int ShelterId,
    string ShelterName,
    string? ShelterNeighborhood,
    string? MainImageUrl,
    string? ShortDescription);

public sealed record DogDetailsApiDto(
    int Id,
    string Name,
    string Breed,
    string? CoatColor,
    int AgeYears,
    int AgeMonths,
    string AgeDisplay,
    DogSize Size,
    DogStatus Status,
    string Location,
    string? Description,
    string? BehaviorDescription,
    string? MedicalStatus,
    CatCompatibility CatCompatibility,
    DogCompatibility DogCompatibility,
    ChildrenCompatibility ChildrenCompatibility,
    DogActivityLevel ActivityLevel,
    DogExperienceNeeded ExperienceNeeded,
    ApartmentSuitability ApartmentSuitability,
    string? CompatibilityNotes,
    FoodInfoApiDto? PreferredFood,
    ShelterSummaryApiDto Shelter,
    IReadOnlyList<DogImageApiDto> Images,
    IReadOnlyList<MedicalRecordApiDto> MedicalRecords,
    IReadOnlyList<DogBreedInfoApiDto> BreedInformation);

public sealed record DogImageApiDto(int Id, string ImageUrl, bool IsMainImage);

public sealed record MedicalRecordApiDto(
    int Id,
    string? VaccineName,
    string? TreatmentDescription,
    string? Notes,
    DateTime RecordDate);

public sealed record DogBreedInfoApiDto(
    int Id,
    string Name,
    string? GeneralDescription,
    string? TypicalTraits,
    string? CareNotes,
    string? CommonHealthConsiderations);

public sealed record FoodInfoApiDto(
    int FoodTypeId,
    string FoodTypeName,
    int? DailyAmountGrams);
