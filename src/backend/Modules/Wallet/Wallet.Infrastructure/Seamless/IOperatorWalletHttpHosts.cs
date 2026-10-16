using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>
/// Hosts an operator wallet may be called on over plain http besides the loopback (SPEC-0065 Emenda v2: the DemoCasino by
/// its service name on the internal network of the production compose). Implemented by the settings that know them; with
/// settings that do not, only the loopback is accepted over http.
/// </summary>
internal interface IOperatorWalletHttpHosts
{
    /// <summary>Whether <paramref name="host"/> is listed for plain http for <paramref name="op"/>.</summary>
    bool AllowsHttp(OperatorId op, string host);
}
