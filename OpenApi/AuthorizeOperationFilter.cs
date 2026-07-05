using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PawConnect.OpenApi;

public class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var allowsAnonymous = metadata.OfType<AllowAnonymousAttribute>().Any();
        var hasAuthorize = metadata.OfType<AuthorizeAttribute>().Any();

        if (allowsAnonymous || !hasAuthorize)
        {
            return;
        }

        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd(
            StatusCodes.Status401Unauthorized.ToString(),
            new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses.TryAdd(
            StatusCodes.Status403Forbidden.ToString(),
            new OpenApiResponse { Description = "Forbidden" });

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("PawConnectCookie", null)] = []
            }
        ];
    }
}
