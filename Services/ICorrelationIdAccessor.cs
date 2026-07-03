namespace PawConnect.Services;

public interface ICorrelationIdAccessor
{
    string? GetCorrelationId();
}
