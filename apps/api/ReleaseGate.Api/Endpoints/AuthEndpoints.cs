using System.Security.Claims;
using ReleaseGate.Api.Contracts;

namespace ReleaseGate.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/api/auth/me", GetCurrentIdentity)
            .WithTags("Authentication")
            .RequireAuthorization();

        return endpoints;
    }

    private static IResult GetCurrentIdentity(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Results.Unauthorized();
        }

        var roles = user.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role)
            .ToList();

        return Results.Ok(new ControlPlaneIdentityResponse(
            subject,
            user.Identity?.Name ?? subject,
            roles));
    }
}
