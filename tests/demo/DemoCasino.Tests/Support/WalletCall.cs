namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>One call of the wallet API: its path, the exact body bytes (what is signed) and its transaction id (none for balance).</summary>
internal sealed record WalletCall(string Path, byte[] Body, string? TransactionId);
