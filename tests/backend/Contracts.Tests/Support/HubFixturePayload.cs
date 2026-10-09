using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>Hub payload-fixture with every contract scalar (SPEC-0006 §6).</summary>
public sealed record HubFixturePayload(Money Stake, Multiplier Cashout, DateTimeOffset StartedAt, HubFixturePhase Phase);
