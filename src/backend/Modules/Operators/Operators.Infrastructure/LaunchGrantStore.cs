using Apogeu.Operators.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Apogeu.Operators.Infrastructure;

internal sealed class LaunchGrantStore(OperatorsDbContext db) : ILaunchGrantStore
{
    public async Task AddAsync(LaunchGrantRecord record, CancellationToken cancellationToken)
    {
        db.LaunchGrants.Add(record);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<LaunchGrantRecord?> FindAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.LaunchGrants.AsNoTracking().SingleOrDefaultAsync(g => g.TokenHash == tokenHash, cancellationToken);

    public async Task<bool> TryMarkRedeemedAsync(string tokenHash, DateTimeOffset redeemedAt, CancellationToken cancellationToken)
    {
        // One UPDATE ... WHERE redeemed_at IS NULL: the row lock makes concurrent redemptions of the same token serialize, and
        // only the first one still sees redeemed_at NULL.
        var updated = await db.LaunchGrants
            .Where(g => g.TokenHash == tokenHash && g.RedeemedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(g => g.RedeemedAt, redeemedAt), cancellationToken)
            .ConfigureAwait(false);
        return updated == 1;
    }
}
