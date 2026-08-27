using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ReleaseGate.Api.Security;

public sealed class ControlPlaneAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ControlPlaneAuthOptions> controlPlaneOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Bearer token is required."));
        }

        var principal = controlPlaneOptions.Value.Tokens.FirstOrDefault(candidate =>
            TokenMatches(candidate.Token, token));

        if (principal is null || string.IsNullOrWhiteSpace(principal.Subject))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid control-plane token."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, principal.Subject),
            new(ClaimTypes.Name, principal.DisplayName?.Trim() is { Length: > 0 } displayName
                ? displayName
                : principal.Subject)
        };

        claims.AddRange(principal.Roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => new Claim(ClaimTypes.Role, role.Trim().ToLowerInvariant())));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool TokenMatches(string configuredToken, string suppliedToken)
    {
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            return false;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredToken);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);

        return configuredBytes.Length == suppliedBytes.Length
               && CryptographicOperations.FixedTimeEquals(configuredBytes, suppliedBytes);
    }
}
