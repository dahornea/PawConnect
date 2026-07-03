namespace PawConnect.Services;

public interface ICopilotCriteriaComparisonService
{
    CopilotCriteriaComparisonResult Compare(
        IReadOnlyDictionary<string, IReadOnlyList<string>> expectedCriteria,
        IReadOnlyList<AdoptionCopilotConstraint> actualCriteria);
}
