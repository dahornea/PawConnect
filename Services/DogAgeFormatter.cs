using PawConnect.Entities;

namespace PawConnect.Services;

public static class DogAgeFormatter
{
    public static string Format(Dog dog)
    {
        var years = dog.AgeYears;
        var months = dog.AgeMonths;

        if (years == 0 && months == 0 && dog.Age > 0)
        {
            years = dog.Age;
        }

        return Format(years, months);
    }

    public static string Format(int years, int months)
    {
        if (years <= 0 && months <= 0)
        {
            return "Age not set";
        }

        if (years <= 0)
        {
            return $"{months} {(months == 1 ? "month" : "months")} old";
        }

        if (months <= 0)
        {
            return $"{years} {(years == 1 ? "year" : "years")} old";
        }

        return $"{years} {(years == 1 ? "year" : "years")}, {months} {(months == 1 ? "month" : "months")} old";
    }
}
