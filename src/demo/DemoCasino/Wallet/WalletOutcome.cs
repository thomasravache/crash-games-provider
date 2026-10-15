namespace Apogeu.DemoCasino.Wallet;

/// <summary>The result of one wallet operation: a status of the contract and the balance it reports.</summary>
internal sealed record WalletOutcome(string Status, decimal Balance);
