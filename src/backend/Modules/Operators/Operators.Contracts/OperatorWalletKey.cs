namespace Apogeu.Operators.Contracts;

/// <summary>
/// API key the Apogeu signs its calls to the operator wallet with (public contract §3): <paramref name="Id"/> goes in
/// <c>X-Apogeu-Key</c>; <paramref name="Secret"/> (UTF-8) keys the HMAC and never travels nor reaches the logs (ADR-0014).
/// </summary>
public sealed record OperatorWalletKey(string Id, string Secret)
{
    /// <summary>The key without its secret, so a log or an exception message never carries it.</summary>
    public override string ToString() => $"OperatorWalletKey {{ Id = {Id} }}";
}
