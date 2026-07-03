using System.Globalization;
using System.Text.RegularExpressions;

namespace PawConnect.Services;

public class CopilotCriteriaComparisonService : ICopilotCriteriaComparisonService
{
    private static readonly Regex ValueSplitRegex = new(@"\s*[,;/]\s*", RegexOptions.Compiled);

    public CopilotCriteriaComparisonResult Compare(
        IReadOnlyDictionary<string, IReadOnlyList<string>> expectedCriteria,
        IReadOnlyList<AdoptionCopilotConstraint> actualCriteria)
    {
        var expected = NormalizeCriteria(expectedCriteria);
        var actual = NormalizeCriteria(actualCriteria);
        var fields = new List<CopilotCriteriaFieldComparison>();

        foreach (var pair in expected.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            actual.TryGetValue(pair.Key, out var actualValues);
            actualValues ??= [];

            var isMissing = actualValues.Count == 0;
            var isCorrect = !isMissing && pair.Value.Overlaps(actualValues);
            fields.Add(new CopilotCriteriaFieldComparison(
                ToDisplayLabel(pair.Key),
                pair.Value.Select(ToDisplayValue).ToList(),
                actualValues.Select(ToDisplayValue).ToList(),
                isCorrect,
                isMissing));
        }

        var extraFields = actual
            .Where(pair => !expected.ContainsKey(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new CopilotCriteriaFieldComparison(
                ToDisplayLabel(pair.Key),
                [],
                pair.Value.Select(ToDisplayValue).ToList(),
                false,
                false))
            .ToList();

        var expectedFieldCount = expected.Count;
        var correctFieldCount = fields.Count(field => field.IsCorrect);
        var accuracy = expectedFieldCount == 0
            ? 0
            : correctFieldCount * 100.0 / expectedFieldCount;

        return new CopilotCriteriaComparisonResult(
            expectedFieldCount,
            correctFieldCount,
            fields.Count(field => field.IsMissing),
            extraFields.Count,
            accuracy,
            fields,
            extraFields);
    }

    private static Dictionary<string, HashSet<string>> NormalizeCriteria(
        IReadOnlyDictionary<string, IReadOnlyList<string>> criteria)
    {
        var normalized = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in criteria)
        {
            var label = NormalizeToken(pair.Key);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            foreach (var value in pair.Value.SelectMany(SplitValues).Select(NormalizeToken))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (!normalized.TryGetValue(label, out var values))
                {
                    values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    normalized[label] = values;
                }

                values.Add(value);
            }
        }

        return normalized;
    }

    private static Dictionary<string, HashSet<string>> NormalizeCriteria(
        IReadOnlyList<AdoptionCopilotConstraint> constraints)
    {
        var normalized = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var constraint in constraints)
        {
            var label = NormalizeToken(constraint.Label);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            foreach (var value in SplitValues(constraint.Value).Select(NormalizeToken))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (!normalized.TryGetValue(label, out var values))
                {
                    values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    normalized[label] = values;
                }

                values.Add(value);
            }
        }

        return normalized;
    }

    private static IEnumerable<string> SplitValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var part in ValueSplitRegex.Split(value))
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                yield return part;
            }
        }
    }

    private static string NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
    }

    private static string ToDisplayLabel(string value)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value);
    }

    private static string ToDisplayValue(string value)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value);
    }
}
