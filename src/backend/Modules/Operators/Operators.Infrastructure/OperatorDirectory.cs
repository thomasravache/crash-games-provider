using Apogeu.Operators.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Apogeu.Operators.Infrastructure;

internal sealed class OperatorDirectory(OperatorsDbContext db) : IOperatorDirectory
{
    public Task<OperatorRecord?> FindAsync(string operatorId, CancellationToken cancellationToken) =>
        db.Operators.AsNoTracking().SingleOrDefaultAsync(o => o.Id == operatorId, cancellationToken);

    public async Task EnsureAsync(OperatorRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var existing = await db.Operators.SingleOrDefaultAsync(o => o.Id == record.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            db.Operators.Add(record);
        }
        else
        {
            existing.Name = record.Name;
            existing.SalaId = record.SalaId;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
