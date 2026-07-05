using PawConnect.Entities;

namespace PawConnect.DTOs.Api;

public sealed record AdminPlatformSummaryApiDto(
    DateTime GeneratedAtUtc,
    int TotalShelters,
    int PublicDogs,
    int AdoptedDogs,
    int InTreatmentDogs,
    int PendingAdoptionApplications,
    NotificationOutboxSummaryApiDto NotificationOutbox);

public sealed record NotificationOutboxSummaryApiDto(
    int Total,
    int Pending,
    int Processing,
    int Sent,
    int Failed,
    int DeadLetter,
    int Cancelled);

public sealed record AdminAnalyticsSummaryApiDto(
    string RangeLabel,
    int TotalSummaryCards,
    IReadOnlyList<AdminAnalyticsSummaryCardApiDto> SummaryCards,
    int SubmittedRequests,
    int AcceptedRequests,
    int PendingRequests,
    int ShelterCount);

public sealed record AdminAnalyticsSummaryCardApiDto(
    string Label,
    string Value,
    string HelperText,
    string Tone);
