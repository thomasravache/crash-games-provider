using System.Security.Cryptography;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>An environment file for the published compose filling every declared variable with a value of the test.</summary>
internal static class ComposeTestEnvironment
{
    public static Task WriteAsync(string path, IEnumerable<string> variables, CancellationToken cancellationToken) =>
        File.WriteAllLinesAsync(
            path,
            variables.Order(StringComparer.Ordinal).Select(variable => $"{variable}={TestValue(variable)}"),
            cancellationToken);

    private static string TestValue(string variable) =>
        variable.EndsWith("_IMAGE", StringComparison.Ordinal) ? $"registry.apogeu.test/{variable.ToLowerInvariant().Replace('_', '-')}:test"
        : variable.StartsWith("PUBLIC_", StringComparison.Ordinal) ? $"https://{variable.ToLowerInvariant().Replace('_', '-')}.apogeu.test"
        : RandomNumberGenerator.GetHexString(32, lowercase: true);
}
