using PawConnect.Entities;

namespace PawConnect.Services;

public static class DogBreedInformationFormatter
{
    public const string FallbackNote = "No detailed breed note is available yet. Please rely on this dog's description, medical records, and shelter observations.";
    public const string FallbackHealthNote = "No breed-specific health note is available. Please rely on this dog's medical records and shelter or veterinary information.";
    public const string HealthDisclaimer = "These are general breed-level considerations, not a diagnosis.";

    public static bool HasBreedNote(Dog? dog)
    {
        return GetBreedNotes(dog, breed => breed.GeneralDescription).Any();
    }

    public static string GetGeneralNote(Dog? dog)
    {
        return GetGeneralNotes(dog).First();
    }

    public static IReadOnlyList<string> GetGeneralNotes(Dog? dog)
    {
        var notes = GetBreedNotes(dog, breed => breed.GeneralDescription)
            .Take(2)
            .ToArray();
        return notes.Length == 0 ? [FallbackNote] : notes;
    }

    public static IReadOnlyList<string> GetTypicalTraitNotes(Dog? dog)
    {
        return GetBreedNotes(dog, breed => breed.TypicalTraits)
            .Take(2)
            .ToArray();
    }

    public static string GetHealthNote(Dog? dog)
    {
        var notes = GetBreedNotes(dog, breed => breed.CommonHealthConsiderations)
            .Take(2)
            .ToArray();
        return notes.Length == 0 ? FallbackHealthNote : string.Join(" ", notes);
    }

    public static string? GetCareContext(Dog? dog)
    {
        if (string.IsNullOrWhiteSpace(dog?.DogBreed?.CareNotes))
        {
            return null;
        }

        var note = dog.DogBreed.CareNotes.Trim();
        if (note.StartsWith("They may ", StringComparison.OrdinalIgnoreCase))
        {
            note = $"May {note[9..]}";
        }

        note = note
            .Replace("; ", ". ", StringComparison.Ordinal)
            .Replace(". check ", ". Check ", StringComparison.OrdinalIgnoreCase)
            .Replace("the dog's", $"{GetDogNamePossessive(dog)}", StringComparison.OrdinalIgnoreCase)
            .Replace("this dog's", $"{GetDogNamePossessive(dog)}", StringComparison.OrdinalIgnoreCase);

        return note;
    }

    public static IReadOnlyList<string> GetHealthConsiderationItems(Dog? dog)
    {
        var notes = GetBreedNotes(dog, breed => breed.CommonHealthConsiderations)
            .Take(2)
            .ToArray();
        if (notes.Length == 0)
        {
            return [];
        }

        return notes
            .Select(GetFirstSentence)
            .Select(ExtractHealthConsiderationPhrase)
            .Where(phrase => !string.IsNullOrWhiteSpace(phrase))
            .SelectMany(phrase => SplitHealthConsiderations(phrase!))
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    public static string GetHealthFollowUp(Dog? dog)
    {
        var note = GetHealthNote(dog);
        if (string.Equals(note, FallbackHealthNote, StringComparison.Ordinal))
        {
            return FallbackHealthNote;
        }

        if (HasTwoKnownBreeds(dog))
        {
            return $"Because this is a mixed-breed dog, these are general considerations only. Review {GetDogNamePossessive(dog)} actual medical records and ask the shelter or veterinarian.";
        }

        return $"This does not mean {GetDogName(dog)} has these conditions; review the medical records and ask the shelter or veterinarian.";
    }

    public static string GetHealthConsiderationsIntro(Dog? dog)
    {
        if (HasTwoKnownBreeds(dog))
        {
            return "General breed-level considerations may include:";
        }

        var breedName = dog?.DogBreed?.Name;
        if (string.IsNullOrWhiteSpace(breedName) ||
            string.Equals(breedName, "Mixed Breed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(breedName, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "Common breed-level considerations may include:";
        }

        return $"{breedName}-type dogs may be more prone to:";
    }

    public static string GetImportantNote(Dog? dog)
    {
        return $"{HealthDisclaimer} {GetDogNamePossessive(dog)} actual medical status and medical records are the source of truth.";
    }

    public static bool IsMixedBreed(Dog? dog)
    {
        if (dog is null)
        {
            return false;
        }

        var formattedBreed = DogBreedFormatter.Format(dog);
        return dog.IsMixedBreed ||
            string.Equals(dog.DogBreed?.Name, "Mixed Breed", StringComparison.OrdinalIgnoreCase) ||
            formattedBreed.Contains(" Mix", StringComparison.OrdinalIgnoreCase) ||
            formattedBreed.Contains("Mixed Breed", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetDisclaimer(Dog? dog)
    {
        if (dog is not null && IsMixedBreed(dog))
        {
            return $"Because {dog.Name} is mixed-breed, individual shelter observations and medical history matter more than general breed expectations.";
        }

        return "Use this as general breed context; individual behavior and shelter observations matter most.";
    }

    private static string GetDogName(Dog? dog)
    {
        return string.IsNullOrWhiteSpace(dog?.Name) ? "this dog" : dog.Name;
    }

    private static string GetDogNamePossessive(Dog? dog)
    {
        var name = GetDogName(dog);
        return string.Equals(name, "this dog", StringComparison.Ordinal)
            ? "this dog's"
            : name.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? $"{name}'" : $"{name}'s";
    }

    private static IEnumerable<string> GetBreedNotes(Dog? dog, Func<DogBreed, string?> selector)
    {
        foreach (var breed in GetBreedSequence(dog))
        {
            var note = selector(breed);
            if (!string.IsNullOrWhiteSpace(note))
            {
                yield return note.Trim();
            }
        }
    }

    private static IEnumerable<DogBreed> GetBreedSequence(Dog? dog)
    {
        if (dog?.DogBreed is not null)
        {
            yield return dog.DogBreed;
        }

        if (dog?.SecondaryBreed is not null && dog.SecondaryBreed.Id != dog.DogBreed?.Id && IsSpecificKnownBreed(dog.SecondaryBreed))
        {
            yield return dog.SecondaryBreed;
        }
    }

    private static bool HasTwoKnownBreeds(Dog? dog)
    {
        return dog?.DogBreed is not null &&
            IsSpecificKnownBreed(dog.DogBreed) &&
            dog.SecondaryBreed is not null &&
            dog.SecondaryBreed.Id != dog.DogBreed.Id &&
            IsSpecificKnownBreed(dog.SecondaryBreed);
    }

    private static bool IsSpecificKnownBreed(DogBreed breed)
    {
        return !breed.Name.Equals("Mixed Breed", StringComparison.OrdinalIgnoreCase) &&
            !breed.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFirstSentence(string note)
    {
        var periodIndex = note.IndexOf('.', StringComparison.Ordinal);
        return periodIndex < 0 ? note.Trim() : note[..periodIndex].Trim();
    }

    private static string? ExtractHealthConsiderationPhrase(string sentence)
    {
        var phrase = ExtractAfter(sentence,
            "may be more prone to ",
            "can be prone to ",
            "common considerations include ",
            "can be associated with ",
            "may need ");

        if (string.IsNullOrWhiteSpace(phrase))
        {
            return null;
        }

        phrase = TrimAfter(phrase, " and may ", " depending ", " because ", " so ");
        return phrase.Trim().TrimEnd('.');
    }

    private static string? ExtractAfter(string sentence, params string[] markers)
    {
        foreach (var marker in markers)
        {
            var index = sentence.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return sentence[(index + marker.Length)..];
            }
        }

        return null;
    }

    private static string TrimAfter(string value, params string[] markers)
    {
        foreach (var marker in markers)
        {
            var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                value = value[..index];
            }
        }

        return value;
    }

    private static IEnumerable<string> SplitHealthConsiderations(string phrase)
    {
        return phrase
            .Replace(", and ", ",", StringComparison.OrdinalIgnoreCase)
            .Replace(" and ", ",", StringComparison.OrdinalIgnoreCase)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanHealthItem);
    }

    private static string CleanHealthItem(string item)
    {
        return item
            .Replace("regular coat and ear care", "regular coat care, ear care", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .TrimEnd('.');
    }
}
