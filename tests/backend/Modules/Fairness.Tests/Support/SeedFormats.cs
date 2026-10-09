namespace Apogeu.Fairness.Tests.Support;

/// <summary>The textual forms under which raw seed bytes could leak: hex (both cases), dashed hex and base64 (both alphabets).</summary>
internal static class SeedFormats
{
    public static IEnumerable<string> Of(byte[] seed)
    {
        var hex = Convert.ToHexString(seed);
        yield return hex;
        yield return hex.ToLowerInvariant();
        yield return BitConverter.ToString(seed);
        var base64 = Convert.ToBase64String(seed);
        yield return base64;
        yield return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
