using System.Globalization;
using System.Text.RegularExpressions;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public static partial class DogBreedFormatter
{
    public static string Format(Dog? dog)
    {
        return dog is null
            ? "Unknown"
            : Format(dog.DogBreed?.Name, dog.SecondaryBreed?.Name, dog.IsMixedBreed, dog.CustomBreedName, dog.Breed);
    }

    public static string Format(string? selectedBreedName, bool isMixedBreed, string? customBreedName, string? legacyBreed = null)
    {
        return Format(selectedBreedName, null, isMixedBreed, customBreedName, legacyBreed);
    }

    public static string Format(string? selectedBreedName, string? secondaryBreedName, bool isMixedBreed, string? customBreedName, string? legacyBreed = null)
    {
        var selected = Clean(selectedBreedName);
        var secondary = Clean(secondaryBreedName);
        var custom = Clean(customBreedName);
        var legacy = Clean(legacyBreed);

        if (!string.IsNullOrWhiteSpace(custom) &&
            (string.IsNullOrWhiteSpace(selected) || IsUnknown(selected)))
        {
            return isMixedBreed && !AlreadyMix(custom) ? $"{custom} Mix" : custom;
        }

        if (!string.IsNullOrWhiteSpace(selected))
        {
            if (IsUnknown(selected) || IsMixedBreed(selected))
            {
                return selected;
            }

            if (isMixedBreed &&
                !string.IsNullOrWhiteSpace(secondary) &&
                !IsUnknown(secondary) &&
                !IsMixedBreed(secondary) &&
                !string.Equals(selected, secondary, StringComparison.OrdinalIgnoreCase))
            {
                return $"{selected} \u00d7 {secondary} Mix";
            }

            return isMixedBreed && !AlreadyMix(selected) ? $"{selected} Mix" : selected;
        }

        return string.IsNullOrWhiteSpace(legacy) ? "Unknown" : legacy;
    }

    public static DogBreedParseResult Parse(string? breedText, IReadOnlyList<DogBreed> breeds)
    {
        var cleaned = Clean(breedText);
        var unknown = FindByName(breeds, "Unknown");
        var mixedBreed = FindByName(breeds, "Mixed Breed");

        if (string.IsNullOrWhiteSpace(cleaned) || IsUnknown(cleaned))
        {
            return new DogBreedParseResult(unknown?.Id, null, false, null, "Unknown");
        }

        if (IsMixedAlias(cleaned))
        {
            return new DogBreedParseResult(mixedBreed?.Id, null, false, null, "Mixed Breed");
        }

        var splitBreed = TryParseKnownBreedPair(cleaned, breeds);
        if (splitBreed is not null)
        {
            return splitBreed;
        }

        var isMixed = AlreadyMix(cleaned) || HasCrossSeparator(cleaned);
        var baseName = RemoveMixWords(cleaned);
        var matchedBreed = FindBestMatch(baseName, breeds);
        if (matchedBreed is not null)
        {
            return new DogBreedParseResult(
                matchedBreed.Id,
                null,
                isMixed && !IsMixedBreed(matchedBreed.Name),
                null,
                Format(matchedBreed.Name, isMixed, null, cleaned));
        }

        var customName = ToDisplayName(string.IsNullOrWhiteSpace(baseName) ? cleaned : baseName);
        return new DogBreedParseResult(null, null, isMixed, customName, Format(null, isMixed, customName, cleaned));
    }

    public static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WhitespaceRegex().Replace(value.Trim(), " ").ToUpperInvariant();
    }

    private static DogBreed? FindBestMatch(string value, IReadOnlyList<DogBreed> breeds)
    {
        var normalized = NormalizeKey(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var exact = breeds.FirstOrDefault(breed => NormalizeKey(breed.Name) == normalized);
        if (exact is not null)
        {
            return exact;
        }

        var alias = normalized switch
        {
            "LABRADOR" or "LAB" => "Labrador Retriever",
            "GERMAN SHEPHERD DOG" => "German Shepherd",
            "GOLDEN" => "Golden Retriever",
            "COLLIE" => "Border Collie",
            "POODLE MIX" => "Poodle",
            "BICHON FRISE" or "BICHON MIX" => "Bichon",
            "CORGI MIX" => "Corgi",
            "SPANIEL MIX" => "Spaniel",
            "SETTER MIX" => "Setter",
            "TERRIER MIX" => "Terrier",
            "HUSKY MIX" => "Husky",
            "PITBULL" or "PIT BULL" => "Pit Bull Terrier",
            "AMSTAFF" or "AMERICAN STAFFY" => "American Staffordshire Terrier",
            "STAFFY" or "STAFFIE" => "Staffordshire Bull Terrier",
            "DOBERMAN" => "Doberman Pinscher",
            "MALAMUTE" => "Alaskan Malamute",
            "JACK RUSSELL" => "Jack Russell Terrier",
            "MINI SCHNAUZER" => "Miniature Schnauzer",
            "MINIATURE SCHNAUZER MIX" => "Miniature Schnauzer",
            "WESTIE" => "West Highland White Terrier",
            "SAINT BERNARD" or "ST BERNARD" or "ST. BERNARD" => "Saint Bernard",
            "DOGO ARGENTINO" => "Argentine Dogo",
            "KANGAL" => "Kangal Shepherd",
            "BUCOVINA SHEPHERD" or "ROMANIAN BUCOVINA" => "Romanian Bucovina Shepherd",
            "COCKER" => "Cocker Spaniel",
            "ENGLISH COCKER" => "English Cocker Spaniel",
            "MIN PIN" => "Miniature Pinscher",
            "MINIATURE PINCHER" => "Miniature Pinscher",
            "PORTUGUESE WATERDOG" => "Portuguese Water Dog",
            "SHAR-PEI" or "CHINESE SHAR PEI" or "CHINESE SHAR-PEI" => "Shar Pei",
            "SHELTIE" => "Shetland Sheepdog",
            _ => null
        };

        if (alias is not null)
        {
            return FindByName(breeds, alias);
        }

        return breeds
            .Where(breed => !IsUnknown(breed.Name) && !IsMixedBreed(breed.Name))
            .OrderByDescending(breed => NormalizeKey(breed.Name).Length)
            .FirstOrDefault(breed =>
            {
                var breedKey = NormalizeKey(breed.Name);
                return normalized == breedKey ||
                    normalized.Contains(breedKey, StringComparison.OrdinalIgnoreCase) ||
                    breedKey.Contains(normalized, StringComparison.OrdinalIgnoreCase);
            });
    }

    private static DogBreedParseResult? TryParseKnownBreedPair(string value, IReadOnlyList<DogBreed> breeds)
    {
        var cleanedWithoutMix = RemoveMixWords(value);
        var separatedParts = CrossSeparatorRegex()
            .Split(cleanedWithoutMix)
            .Select(Clean)
            .Where(part => part.Length > 0)
            .ToArray();

        if (separatedParts.Length >= 2)
        {
            var primary = FindBestMatch(separatedParts[0], breeds);
            var secondary = FindBestMatch(separatedParts[1], breeds);
            if (IsUsableKnownBreed(primary) && IsUsableKnownBreed(secondary) && primary!.Id != secondary!.Id)
            {
                return new DogBreedParseResult(
                    primary.Id,
                    secondary.Id,
                    true,
                    null,
                    Format(primary.Name, secondary.Name, true, null, value));
            }
        }

        if (!AlreadyMix(value) && !HasCrossSeparator(value))
        {
            return null;
        }

        var contained = FindContainedBreedMatches(cleanedWithoutMix, breeds).ToArray();
        if (contained.Length >= 2)
        {
            var primary = contained[0].Breed;
            var secondary = contained[1].Breed;
            return new DogBreedParseResult(
                primary.Id,
                secondary.Id,
                true,
                null,
                Format(primary.Name, secondary.Name, true, null, value));
        }

        return null;
    }

    private static IEnumerable<(DogBreed Breed, int Index)> FindContainedBreedMatches(string value, IReadOnlyList<DogBreed> breeds)
    {
        var normalized = NormalizeKey(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        var matches = new List<(DogBreed Breed, int Index, int Length)>();
        foreach (var breed in breeds.Where(IsUsableKnownBreed))
        {
            var breedKey = NormalizeKey(breed.Name);
            var index = normalized.IndexOf(breedKey, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                matches.Add((breed, index, breedKey.Length));
            }
        }

        foreach (var (alias, breedName) in AliasMappings())
        {
            var index = normalized.IndexOf(alias, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var breed = FindByName(breeds, breedName);
            if (breed is not null && matches.All(match => match.Breed.Id != breed.Id))
            {
                matches.Add((breed, index, alias.Length));
            }
        }

        foreach (var match in matches
            .GroupBy(match => match.Breed.Id)
            .Select(group => group.OrderBy(match => match.Index).ThenByDescending(match => match.Length).First())
            .OrderBy(match => match.Index)
            .ThenByDescending(match => match.Length))
        {
            yield return (match.Breed, match.Index);
        }
    }

    private static bool IsUsableKnownBreed(DogBreed? breed)
    {
        return breed is not null && !IsUnknown(breed.Name) && !IsMixedBreed(breed.Name);
    }

    private static bool HasCrossSeparator(string value)
    {
        return CrossSeparatorRegex().IsMatch(value);
    }

    private static DogBreed? FindByName(IReadOnlyList<DogBreed> breeds, string name)
    {
        var normalized = NormalizeKey(name);
        return breeds.FirstOrDefault(breed => NormalizeKey(breed.Name) == normalized);
    }

    private static IReadOnlyDictionary<string, string> AliasMappings()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LABRADOR"] = "Labrador Retriever",
            ["LAB"] = "Labrador Retriever",
            ["GERMAN SHEPHERD DOG"] = "German Shepherd",
            ["GOLDEN"] = "Golden Retriever",
            ["COLLIE"] = "Border Collie",
            ["BICHON FRISE"] = "Bichon",
            ["PITBULL"] = "Pit Bull Terrier",
            ["PIT BULL"] = "Pit Bull Terrier",
            ["AMSTAFF"] = "American Staffordshire Terrier",
            ["AMERICAN STAFFY"] = "American Staffordshire Terrier",
            ["STAFFY"] = "Staffordshire Bull Terrier",
            ["STAFFIE"] = "Staffordshire Bull Terrier",
            ["DOBERMAN"] = "Doberman Pinscher",
            ["MALAMUTE"] = "Alaskan Malamute",
            ["JACK RUSSELL"] = "Jack Russell Terrier",
            ["MINI SCHNAUZER"] = "Miniature Schnauzer",
            ["MINIATURE PINCHER"] = "Miniature Pinscher",
            ["WESTIE"] = "West Highland White Terrier",
            ["SAINT BERNARD"] = "Saint Bernard",
            ["ST BERNARD"] = "Saint Bernard",
            ["ST. BERNARD"] = "Saint Bernard",
            ["DOGO ARGENTINO"] = "Argentine Dogo",
            ["KANGAL"] = "Kangal Shepherd",
            ["BUCOVINA SHEPHERD"] = "Romanian Bucovina Shepherd",
            ["ROMANIAN BUCOVINA"] = "Romanian Bucovina Shepherd",
            ["COCKER"] = "Cocker Spaniel",
            ["ENGLISH COCKER"] = "English Cocker Spaniel",
            ["MIN PIN"] = "Miniature Pinscher",
            ["PORTUGUESE WATERDOG"] = "Portuguese Water Dog",
            ["SHAR-PEI"] = "Shar Pei",
            ["CHINESE SHAR PEI"] = "Shar Pei",
            ["CHINESE SHAR-PEI"] = "Shar Pei",
            ["SHELTIE"] = "Shetland Sheepdog"
        };
    }

    private static string RemoveMixWords(string value)
    {
        return Clean(MixWordsRegex().Replace(value, " "));
    }

    private static string ToDisplayName(string value)
    {
        var lower = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        return lower
            .Replace(" Ii ", " II ", StringComparison.Ordinal)
            .Replace(" Iii ", " III ", StringComparison.Ordinal);
    }

    private static string Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex().Replace(value.Trim(), " ");
    }

    private static bool IsUnknown(string value)
    {
        var normalized = NormalizeKey(value);
        return normalized is "UNKNOWN" or "UNSPECIFIED" or "NOT SURE";
    }

    private static bool IsMixedBreed(string value)
    {
        return NormalizeKey(value) == "MIXED BREED";
    }

    private static bool IsMixedAlias(string value)
    {
        var normalized = NormalizeKey(value);
        return normalized is "MIXED" or "MIXED BREED" or "MUTT" or "CROSSBREED" or "CROSS BREED";
    }

    private static bool AlreadyMix(string value)
    {
        var normalized = NormalizeKey(value);
        return normalized.Contains(" MIX", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("MIXED", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("MUTT", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\b(mix|mixed|breed|mutt|crossbreed|cross\s+breed)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MixWordsRegex();

    [GeneratedRegex(@"\s*(?:\u00d7|/|\+)\s*|\s+(?:x|and)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex CrossSeparatorRegex();
}

public sealed record DogBreedParseResult(int? DogBreedId, int? SecondaryBreedId, bool IsMixedBreed, string? CustomBreedName, string DisplayName);
