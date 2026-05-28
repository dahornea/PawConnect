using PawConnect.Entities;

namespace PawConnect.Services;

public sealed class SemanticDogSearchOptions
{
    public int? ShelterId { get; init; }

    public DogSize? Size { get; init; }

    public DogStatus? Status { get; init; }

    public string? Location { get; init; }

    public string? Neighborhood { get; init; }

    public IReadOnlyList<string>? CoatColors { get; init; }

    public double? OriginLatitude { get; init; }

    public double? OriginLongitude { get; init; }

    public int? RadiusKm { get; init; }
}

public sealed record SemanticDogSearchResult(
    int DogId,
    Dog Dog,
    int ScorePercent,
    string MatchLabel,
    IReadOnlyList<string> Reasons,
    string ShortSummary,
    double? DistanceKm = null,
    bool UsedSemanticEmbeddings = false);
