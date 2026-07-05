using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.DTOs.Api;

public static class ApiDtoMapper
{
    public static bool IsPublicSafeDog(Dog dog)
    {
        return dog.Status is DogStatus.Available or DogStatus.Reserved;
    }

    public static DogListItemApiDto ToDogListItem(Dog dog)
    {
        return new DogListItemApiDto(
            dog.Id,
            dog.Name,
            DogBreedFormatter.Format(dog),
            NormalizeOptional(dog.CoatColor),
            dog.AgeYears,
            dog.AgeMonths,
            DogAgeFormatter.Format(dog),
            dog.Size,
            dog.Status,
            dog.Location,
            dog.ShelterId,
            dog.Shelter?.Name ?? "Unknown shelter",
            dog.Shelter?.Neighborhood,
            DogImageUrlValidator.GetPrimaryRealDogImageUrl(dog.Images),
            CreateShortDescription(dog.Description));
    }

    public static DogDetailsApiDto ToDogDetails(Dog dog)
    {
        var realImages = DogImageUrlValidator.GetRealDogImages(dog.Images)
            .Select(image => new DogImageApiDto(image.Id, image.ImageUrl, image.IsMainImage))
            .ToList();

        return new DogDetailsApiDto(
            dog.Id,
            dog.Name,
            DogBreedFormatter.Format(dog),
            NormalizeOptional(dog.CoatColor),
            dog.AgeYears,
            dog.AgeMonths,
            DogAgeFormatter.Format(dog),
            dog.Size,
            dog.Status,
            dog.Location,
            dog.Description,
            dog.BehaviorDescription,
            dog.MedicalStatus,
            dog.CatCompatibility,
            dog.DogCompatibility,
            dog.ChildrenCompatibility,
            dog.ActivityLevel,
            dog.ExperienceNeeded,
            dog.ApartmentSuitability,
            dog.CompatibilityNotes,
            ToFoodInfo(dog),
            ToShelterSummary(dog.Shelter),
            realImages,
            dog.MedicalRecords
                .OrderByDescending(record => record.RecordDate)
                .Select(record => new MedicalRecordApiDto(
                    record.Id,
                    record.VaccineName,
                    record.TreatmentDescription,
                    record.Notes,
                    record.RecordDate))
                .ToList(),
            GetBreedInformation(dog));
    }

    public static ShelterListItemApiDto ToShelterListItem(Shelter shelter)
    {
        return new ShelterListItemApiDto(
            shelter.Id,
            shelter.Name,
            shelter.Description,
            shelter.Address,
            shelter.City,
            shelter.Neighborhood,
            shelter.PhoneNumber,
            shelter.Email,
            shelter.Latitude,
            shelter.Longitude,
            shelter.Dogs.Count(IsPublicSafeDog));
    }

    public static ShelterDetailsApiDto ToShelterDetails(Shelter shelter)
    {
        return new ShelterDetailsApiDto(
            shelter.Id,
            shelter.Name,
            shelter.Description,
            shelter.Address,
            shelter.City,
            shelter.Neighborhood,
            shelter.PhoneNumber,
            shelter.Email,
            shelter.Latitude,
            shelter.Longitude,
            ToVisitSchedule(shelter),
            shelter.Dogs
                .Where(IsPublicSafeDog)
                .OrderBy(dog => dog.Name)
                .Select(ToDogListItem)
                .ToList());
    }

    public static AdoptionApplicationApiDto ToAdoptionApplication(AdoptionRequest request)
    {
        return new AdoptionApplicationApiDto(
            request.Id,
            request.DogId,
            request.Dog?.Name ?? "Unknown dog",
            request.Dog is null ? "Unknown" : DogBreedFormatter.Format(request.Dog),
            request.Dog?.ShelterId ?? 0,
            request.Dog?.Shelter?.Name ?? "Unknown shelter",
            request.Status,
            request.VisitStatus,
            request.PreferredVisitDateTime,
            request.VisitConfirmedAt,
            request.ReasonForAdoption,
            request.HoursAlonePerDay,
            request.AdditionalInformation,
            request.CreatedAt,
            request.UpdatedAt);
    }

    public static NotificationPreferenceApiDto ToNotificationPreference(NotificationPreferenceDto preference)
    {
        return new NotificationPreferenceApiDto(
            preference.NotificationType,
            preference.DisplayName,
            preference.Description,
            preference.InAppEnabled,
            preference.EmailEnabled,
            preference.DefaultInAppEnabled,
            preference.DefaultEmailEnabled);
    }

    public static NotificationOutboxSummaryApiDto ToNotificationOutboxSummary(NotificationOutboxSummaryDto summary)
    {
        return new NotificationOutboxSummaryApiDto(
            summary.Total,
            summary.Pending,
            summary.Processing,
            summary.Sent,
            summary.Failed,
            summary.DeadLetter,
            summary.Cancelled);
    }

    public static AdminAnalyticsSummaryApiDto ToAdminAnalyticsSummary(AdminAnalyticsDashboardDto analytics)
    {
        return new AdminAnalyticsSummaryApiDto(
            analytics.Range.Label,
            analytics.SummaryCards.Count,
            analytics.SummaryCards
                .Select(card => new AdminAnalyticsSummaryCardApiDto(
                    card.Label,
                    card.Value,
                    card.HelperText,
                    card.Tone))
                .ToList(),
            analytics.AdoptionFunnel.SubmittedRequests,
            analytics.AdoptionFunnel.AcceptedRequests,
            analytics.AdoptionFunnel.PendingRequests,
            analytics.ShelterWorkload.Count);
    }

    private static ShelterSummaryApiDto ToShelterSummary(Shelter? shelter)
    {
        return shelter is null
            ? new ShelterSummaryApiDto(0, "Unknown shelter", string.Empty, null, null, null, null, null)
            : new ShelterSummaryApiDto(
                shelter.Id,
                shelter.Name,
                shelter.City,
                shelter.Neighborhood,
                shelter.Email,
                shelter.PhoneNumber,
                shelter.Latitude,
                shelter.Longitude);
    }

    private static FoodInfoApiDto? ToFoodInfo(Dog dog)
    {
        return dog.PreferredFoodType is null
            ? null
            : new FoodInfoApiDto(
                dog.PreferredFoodType.Id,
                dog.PreferredFoodType.Name,
                dog.DailyFoodAmountGrams);
    }

    private static IReadOnlyList<DogBreedInfoApiDto> GetBreedInformation(Dog dog)
    {
        return new[] { dog.DogBreed, dog.SecondaryBreed }
            .Where(breed => breed is not null)
            .Select(breed => breed!)
            .DistinctBy(breed => breed.Id)
            .Select(breed => new DogBreedInfoApiDto(
                breed.Id,
                breed.Name,
                breed.GeneralDescription,
                breed.TypicalTraits,
                breed.CareNotes,
                breed.CommonHealthConsiderations))
            .ToList();
    }

    private static ShelterVisitScheduleApiDto ToVisitSchedule(Shelter shelter)
    {
        var visitDays = new List<string>();
        if (shelter.VisitsAllowedMonday) visitDays.Add("Monday");
        if (shelter.VisitsAllowedTuesday) visitDays.Add("Tuesday");
        if (shelter.VisitsAllowedWednesday) visitDays.Add("Wednesday");
        if (shelter.VisitsAllowedThursday) visitDays.Add("Thursday");
        if (shelter.VisitsAllowedFriday) visitDays.Add("Friday");
        if (shelter.VisitsAllowedSaturday) visitDays.Add("Saturday");
        if (shelter.VisitsAllowedSunday) visitDays.Add("Sunday");

        return new ShelterVisitScheduleApiDto(
            shelter.VisitStartTime?.ToString(@"hh\:mm"),
            shelter.VisitEndTime?.ToString(@"hh\:mm"),
            visitDays);
    }

    private static string? CreateShortDescription(string? description)
    {
        var normalized = NormalizeOptional(description);
        if (normalized is null || normalized.Length <= 180)
        {
            return normalized;
        }

        return $"{normalized[..177].TrimEnd()}...";
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
