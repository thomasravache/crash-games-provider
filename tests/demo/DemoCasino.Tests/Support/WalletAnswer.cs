using System.Text.Json;

namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>A 200 answer of the wallet API (§5), as strings, plus the whole JSON for the contract checks.</summary>
internal sealed record WalletAnswer(string? Status, string? Balance, string? Currency, string? TransactionId, JsonElement Json);
