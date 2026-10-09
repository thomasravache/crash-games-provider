namespace Apogeu.Security.Tests.Support;

/// <summary>Reads the launch token out of <c>launchUrl = {gameBaseUrl}/?lt={launchToken}</c> (SPEC-0009 §6).</summary>
internal static class LaunchUrls
{
    public static string TokenOf(Uri launchUrl)
    {
        ArgumentNullException.ThrowIfNull(launchUrl);
        foreach (var pair in launchUrl.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], "lt", StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        throw new Xunit.Sdk.XunitException($"The launch URL has no 'lt' parameter: {launchUrl}");
    }
}
