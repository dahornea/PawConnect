using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record MessageReportFilter(
    MessageReportStatus? Status = null,
    DateTime? From = null,
    DateTime? To = null,
    string? ReporterSearch = null);

public sealed record MessageReportDto(
    int Id,
    int MessageId,
    string MessageBody,
    string SenderDisplayName,
    string SenderRole,
    string ReporterDisplayName,
    string ReporterEmail,
    string Reason,
    string? Details,
    MessageReportStatus Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewedByAdminDisplayName,
    string? AdminNote,
    int AdoptionRequestId,
    string DogName,
    string ShelterName,
    string AdopterName);
