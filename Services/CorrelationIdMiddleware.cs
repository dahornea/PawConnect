using System.Diagnostics;

namespace PawConnect.Services;

public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[CorrelationIdAccessor.ItemsKey] = correlationId;
        context.Response.Headers[CorrelationIdAccessor.HeaderName] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdAccessor.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId
               }))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdAccessor.HeaderName, out var values))
        {
            var incoming = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(incoming))
            {
                return Normalize(incoming);
            }
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    private static string Normalize(string value)
    {
        var clean = new string(value
            .Trim()
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')
            .Take(100)
            .ToArray());

        return string.IsNullOrWhiteSpace(clean)
            ? Guid.NewGuid().ToString("N")
            : clean;
    }
}
