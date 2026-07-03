using System.Diagnostics;

namespace PawConnect.Services;

public class CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor) : ICorrelationIdAccessor
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemsKey = "__PawConnectCorrelationId";

    public string? GetCorrelationId()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(ItemsKey, out var value) == true &&
            value is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return Activity.Current?.TraceId.ToString();
    }
}
