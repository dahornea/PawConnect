namespace PawConnect.DTOs.Api;

public sealed record ApiErrorResponse(string Message, string? Detail = null);
