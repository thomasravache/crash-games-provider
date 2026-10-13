using Apogeu.Betting.Features.Hubs;

namespace Apogeu.Betting.Features.LiveBets;

/// <summary>A <see cref="LiveBetsBatch"/> ready to be sent to the group of <paramref name="SalaId"/>.</summary>
public sealed record SalaLiveBetsBatch(string SalaId, LiveBetsBatch Batch);
