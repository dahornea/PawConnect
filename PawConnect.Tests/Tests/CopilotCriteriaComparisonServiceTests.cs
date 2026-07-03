using PawConnect.Services;

namespace PawConnect.Tests.Tests;

public class CopilotCriteriaComparisonServiceTests
{
    [Fact]
    public void Compare_ExactMatchGivesFullAccuracy()
    {
        var service = new CopilotCriteriaComparisonService();
        var expected = BuildExpected(("Size", ["Small"]), ("Home", ["Apartment"]));
        var actual = new[]
        {
            new AdoptionCopilotConstraint("Size", "Small"),
            new AdoptionCopilotConstraint("Home", "Apartment")
        };

        var result = service.Compare(expected, actual);

        Assert.Equal(2, result.ExpectedFieldCount);
        Assert.Equal(2, result.CorrectFieldCount);
        Assert.Equal(100, result.AccuracyPercent);
        Assert.Equal(0, result.MissingFieldCount);
    }

    [Fact]
    public void Compare_PartialMatchCalculatesPercentage()
    {
        var service = new CopilotCriteriaComparisonService();
        var expected = BuildExpected(
            ("Size", ["Small"]),
            ("Lifestyle", ["Low activity"]),
            ("Compatibility", ["Children"]));
        var actual = new[]
        {
            new AdoptionCopilotConstraint("Size", "Small"),
            new AdoptionCopilotConstraint("Lifestyle", "Moderate activity"),
            new AdoptionCopilotConstraint("Compatibility", "Children")
        };

        var result = service.Compare(expected, actual);

        Assert.Equal(3, result.ExpectedFieldCount);
        Assert.Equal(2, result.CorrectFieldCount);
        Assert.Equal(66.66666666666667, result.AccuracyPercent, precision: 5);
    }

    [Fact]
    public void Compare_CountsMissingAndExtraFields()
    {
        var service = new CopilotCriteriaComparisonService();
        var expected = BuildExpected(
            ("Size", ["Medium"]),
            ("Temperament", ["Calm"]));
        var actual = new[]
        {
            new AdoptionCopilotConstraint("Size", "Medium"),
            new AdoptionCopilotConstraint("Status", "Available, Reserved")
        };

        var result = service.Compare(expected, actual);

        Assert.Equal(1, result.CorrectFieldCount);
        Assert.Equal(1, result.MissingFieldCount);
        Assert.Equal(1, result.ExtraFieldCount);
        Assert.Contains(result.Fields, field => field.Field == "Temperament" && field.IsMissing);
        Assert.Contains(result.ExtraFields, field => field.Field == "Status");
    }

    [Fact]
    public void Compare_NormalizesStringBooleanAndListValues()
    {
        var service = new CopilotCriteriaComparisonService();
        var expected = BuildExpected(
            ("ApartmentFriendly", ["true"]),
            ("Activity", ["Longer walks"]));
        var actual = new[]
        {
            new AdoptionCopilotConstraint(" apartmentfriendly ", " TRUE "),
            new AdoptionCopilotConstraint("Activity", "Daily walks, longer walks")
        };

        var result = service.Compare(expected, actual);

        Assert.Equal(2, result.CorrectFieldCount);
        Assert.Equal(100, result.AccuracyPercent);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildExpected(
        params (string Field, string[] Values)[] fields)
    {
        return fields.ToDictionary(
            field => field.Field,
            field => (IReadOnlyList<string>)field.Values,
            StringComparer.OrdinalIgnoreCase);
    }
}
