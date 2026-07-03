using PawConnect.Entities;

namespace PawConnect.Services;

public static class DogCompatibilityFormatter
{
    public const string UnknownPublicLabel = "Ask shelter";

    public static string FormatCat(CatCompatibility value) => value switch
    {
        CatCompatibility.Yes => "Yes",
        CatCompatibility.SlowIntroductions => "Slow introductions",
        CatCompatibility.No => "No",
        _ => UnknownPublicLabel
    };

    public static string FormatDog(DogCompatibility value) => value switch
    {
        DogCompatibility.Yes => "Yes",
        DogCompatibility.CalmDogsOnly => "Calm dogs only",
        DogCompatibility.SlowIntroductions => "Slow introductions",
        DogCompatibility.OnlyDog => "Only dog",
        DogCompatibility.No => "No",
        _ => UnknownPublicLabel
    };

    public static string FormatChildren(ChildrenCompatibility value) => value switch
    {
        ChildrenCompatibility.Yes => "Yes",
        ChildrenCompatibility.OlderChildrenOnly => "Older children only",
        ChildrenCompatibility.No => "No",
        _ => UnknownPublicLabel
    };

    public static string FormatActivity(DogActivityLevel value) => value switch
    {
        DogActivityLevel.Low => "Low",
        DogActivityLevel.Medium => "Medium",
        DogActivityLevel.High => "High",
        _ => UnknownPublicLabel
    };

    public static string FormatExperience(DogExperienceNeeded value) => value switch
    {
        DogExperienceNeeded.Beginner => "Beginner",
        DogExperienceNeeded.SomeExperience => "Some experience",
        DogExperienceNeeded.Experienced => "Experienced adopter",
        _ => UnknownPublicLabel
    };

    public static string FormatApartment(ApartmentSuitability value) => value switch
    {
        ApartmentSuitability.Suitable => "Suitable",
        ApartmentSuitability.MaybeWithRoutine => "Maybe with routine",
        ApartmentSuitability.NotRecommended => "Not recommended",
        _ => UnknownPublicLabel
    };
}
