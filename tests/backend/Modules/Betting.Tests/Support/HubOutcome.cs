using System.Text.Json;

namespace Apogeu.Betting.Tests.Support;

/// <summary>What one hub invocation returned: its raw JSON result, or the message of the <c>HubException</c>.</summary>
internal sealed record HubOutcome(string Method, JsonElement? Result, string? Error)
{
    /// <summary>The result; fails the test with the error when the invocation was refused.</summary>
    public JsonElement Value => Result ?? throw new Xunit.Sdk.XunitException($"{Method} was refused: {Error}");

    /// <summary>Whether the invocation failed with <paramref name="code"/> (the <c>HubException</c> message carries the code).</summary>
    public bool FailedWith(string code) => Error is not null && Error.Contains(code, StringComparison.Ordinal);

    public override string ToString() => Result is { } result ? $"{Method} → {result.GetRawText()}" : $"{Method} ✗ {Error}";
}
