namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>A wallet API request as it reached the casino, and the status it answered.</summary>
internal sealed record ArrivedWalletCall(string Path, IReadOnlyDictionary<string, string> Headers, byte[] Body, int Status)
{
    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}
