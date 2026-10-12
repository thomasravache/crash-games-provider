using Apogeu.Betting.Contracts;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary><see cref="IRoundSettlementQuery"/> on <c>betting.round_settlements</c>.</summary>
internal sealed class RoundSettlementQuery(BettingDbContext db) : IRoundSettlementQuery
{
    public async Task<IReadOnlyList<RoundSettlement>> GetAsync(RoundId roundId, CancellationToken ct)
    {
        var records = await db.RoundSettlements.AsNoTracking()
            .Where(s => s.RoundId == roundId.Value)
            .OrderBy(s => s.OperatorId)
            .ThenBy(s => s.Currency)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return [.. records.Select(s => new RoundSettlement(
            new RoundId(s.RoundId),
            new OperatorId(s.OperatorId),
            new Currency(s.Currency),
            s.BetsCount,
            new Money(s.TotalWagered),
            new Money(s.TotalPaid),
            new Money(s.TotalRefunded),
            new Money(s.Ggr),
            s.SettledAt))];
    }
}
