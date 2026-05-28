namespace PawConnect.Services;

public static class DogCoatColorOptions
{
    public static readonly IReadOnlyList<string> Values =
    [
        "Black",
        "Black and tan",
        "Brown",
        "Brown and white",
        "Golden",
        "Tricolor",
        "White"
    ];

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var knownValue = Values.FirstOrDefault(color => string.Equals(color, trimmed, StringComparison.OrdinalIgnoreCase));
        return knownValue ?? trimmed;
    }

    public static IReadOnlyList<string> DetectInText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalizedText = text.ToLowerInvariant();
        var matches = new List<string>();

        if (ContainsAny(normalizedText, "black and tan", "black-and-tan", "black tan"))
        {
            matches.Add("Black and tan");
        }
        else if (ContainsAny(normalizedText, "black"))
        {
            matches.Add("Black");
            matches.Add("Black and tan");
        }

        if (ContainsAny(normalizedText, "brown and white", "brown-and-white", "brown white"))
        {
            matches.Add("Brown and white");
        }
        else if (ContainsAny(normalizedText, "brown"))
        {
            matches.Add("Brown");
            matches.Add("Brown and white");
        }

        if (ContainsAny(normalizedText, "tri-color", "tri color", "tricolor", "tri-coloured", "tri coloured"))
        {
            matches.Add("Tricolor");
        }

        if (ContainsAny(normalizedText, "golden", "gold"))
        {
            matches.Add("Golden");
        }

        if (ContainsAny(normalizedText, "white"))
        {
            matches.Add("White");
            matches.Add("Brown and white");
        }

        return matches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
