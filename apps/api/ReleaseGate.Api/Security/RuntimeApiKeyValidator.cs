using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ReleaseGate.Api.Security;

public sealed class RuntimeApiKeyValidator(IOptions<RuntimeAccessOptions> options)
{
    public const string HeaderName = "X-ReleaseGate-Key";

    private readonly RuntimeAccessOptions _options = options.Value;

    public RuntimeApiKey? FindCredential(string? providedKey)
    {
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return null;
        }

        return _options.ApiKeys.FirstOrDefault(candidate =>
            SecureEquals(candidate.Key, providedKey));
    }

    public static bool CanAccessProject(RuntimeApiKey credential, string projectKey) =>
        credential.Projects.Any(project =>
            project == "*" || string.Equals(project, projectKey, StringComparison.OrdinalIgnoreCase));

    private static bool SecureEquals(string configuredKey, string providedKey)
    {
        if (string.IsNullOrEmpty(configuredKey))
        {
            return false;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        return configuredBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }
}
