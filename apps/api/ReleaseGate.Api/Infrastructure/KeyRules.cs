using System.Text.RegularExpressions;

namespace ReleaseGate.Api.Infrastructure;

public static partial class KeyRules
{
    [GeneratedRegex("^[a-z][a-z0-9-]{1,78}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();

    public static bool IsValid(string value) =>
        !string.IsNullOrWhiteSpace(value) && KeyRegex().IsMatch(value);
}
