namespace PawConnect.Services;

internal static class AdoptionCopilotConstraintNormalizer
{
    private static readonly string[] OrderedLabels =
    [
        "Status",
        "Home",
        "Activity",
        "Lifestyle",
        "Size",
        "Breed",
        "Coat color",
        "Location",
        "Shelter",
        "Age",
        "Near",
        "Temperament",
        "Compatibility"
    ];

    public static IReadOnlyList<AdoptionCopilotConstraint> Normalize(IEnumerable<AdoptionCopilotConstraint> constraints)
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var constraint in constraints)
        {
            var label = NormalizeLabel(constraint.Label);
            var values = SplitValues(constraint.Value);
            if (values.Count == 0 && !string.IsNullOrWhiteSpace(label))
            {
                AddValue(grouped, label, string.Empty);
                continue;
            }

            foreach (var value in values)
            {
                var normalized = NormalizeValue(label, value);
                if (normalized is null)
                {
                    continue;
                }

                AddValue(grouped, normalized.Value.Label, normalized.Value.Value);
            }
        }

        return grouped
            .OrderBy(pair => GetLabelOrder(pair.Key))
            .ThenBy(pair => pair.Key)
            .Select(pair => new AdoptionCopilotConstraint(pair.Key, string.Join(", ", pair.Value)))
            .Where(constraint => !string.IsNullOrWhiteSpace(constraint.Label) || !string.IsNullOrWhiteSpace(constraint.Value))
            .ToList();
    }

    private static void AddValue(Dictionary<string, List<string>> grouped, string label, string value)
    {
        if (!grouped.TryGetValue(label, out var values))
        {
            values = [];
            grouped[label] = values;
        }

        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(value);
        }
    }

    private static (string Label, string Value)? NormalizeValue(string label, string value)
    {
        var clean = value.Trim();
        if (string.IsNullOrWhiteSpace(clean))
        {
            return (label, clean);
        }

        var lower = clean.ToLowerInvariant();

        if (ContainsAny(lower, ["longer walk", "long walk", "active walk", "brisk walk"]))
        {
            return ("Activity", "Longer walks");
        }

        if (ContainsAny(lower, ["short walk", "short daily walk", "slow walk", "leash walk"]))
        {
            return ("Activity", "Short walks");
        }

        if (ContainsAny(lower, ["daily walk", "regular walk"]))
        {
            return ("Activity", "Daily walks");
        }

        if (ContainsAny(lower, ["moderate activity", "medium activity", "moderate exercise"]) ||
            clean.Equals("Medium", StringComparison.OrdinalIgnoreCase) && label.Equals("Lifestyle", StringComparison.OrdinalIgnoreCase))
        {
            return ("Lifestyle", "Moderate activity");
        }

        if (ContainsAny(lower, ["low activity", "low energy", "less activity"]))
        {
            return ("Lifestyle", "Low activity");
        }

        if (ContainsAny(lower, ["high activity", "high energy"]))
        {
            return ("Lifestyle", "High activity");
        }

        if (ContainsAny(lower, ["quiet routine", "indoor rest"]))
        {
            return ("Lifestyle", ToTitleCase(clean));
        }

        if (ContainsAny(lower, ["apartment", "flat"]))
        {
            return ("Home", "Apartment");
        }

        if (ContainsAny(lower, ["house with yard"]))
        {
            return ("Home", "House with yard");
        }

        if (lower is "house")
        {
            return ("Home", "House");
        }

        if (lower is "yard" or "garden")
        {
            return ("Home", "Yard");
        }

        if (label.Equals("Activity", StringComparison.OrdinalIgnoreCase) &&
            ContainsAny(lower, ["activity", "energy", "routine", "indoor"]))
        {
            return ("Lifestyle", ToTitleCase(clean));
        }

        if (label.Equals("Temperament", StringComparison.OrdinalIgnoreCase) &&
            ContainsAny(lower, ["activity", "walk", "exercise", "apartment", "house", "yard", "indoor", "routine"]))
        {
            return null;
        }

        return (label, CanonicalizeSimpleValue(label, clean));
    }

    private static string NormalizeLabel(string? label)
    {
        var clean = label?.Trim() ?? string.Empty;
        return clean switch
        {
            var text when text.Equals("Behavior", StringComparison.OrdinalIgnoreCase) => "Temperament",
            var text when text.Equals("CoatColor", StringComparison.OrdinalIgnoreCase) => "Coat color",
            _ => clean
        };
    }

    private static string CanonicalizeSimpleValue(string label, string value)
    {
        if (label.Equals("Temperament", StringComparison.OrdinalIgnoreCase))
        {
            return value switch
            {
                var text when text.Equals("calm", StringComparison.OrdinalIgnoreCase) => "Calm",
                var text when text.Equals("gentle", StringComparison.OrdinalIgnoreCase) => "Gentle",
                var text when text.Equals("friendly", StringComparison.OrdinalIgnoreCase) => "Friendly",
                var text when text.Equals("social", StringComparison.OrdinalIgnoreCase) => "Social",
                var text when text.Equals("patient", StringComparison.OrdinalIgnoreCase) => "Patient",
                var text when text.Equals("shy", StringComparison.OrdinalIgnoreCase) => "Shy",
                _ => value
            };
        }

        return value;
    }

    private static IReadOnlyList<string> SplitValues(string? value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();
    }

    private static int GetLabelOrder(string label)
    {
        var index = Array.FindIndex(OrderedLabels, ordered => ordered.Equals(label, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : OrderedLabels.Length;
    }

    private static bool ContainsAny(string value, IEnumerable<string> terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }
}
