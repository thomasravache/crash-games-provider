using Apogeu.BuildingBlocks.Messaging;
using Apogeu.Rounds.Domain;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.EntityFrameworkCore;

namespace Apogeu.Rounds.Infrastructure.Persistence;

/// <summary>
/// <see cref="IRoundOutbox"/> on Wolverine's EF Core outbox of the <see cref="RoundsDbContext"/>. Each save runs in a scope
/// of its own: a Wolverine message context flushes its outgoing messages once, while a conductor lives for many rounds.
/// </summary>
internal sealed class RoundOutbox(IServiceScopeFactory scopes, IMessagingReadiness messaging) : IRoundOutbox
{
    public async Task SaveAsync(Round round, object integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        await messaging.WhenReadyAsync(ct).ConfigureAwait(false);

        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<RoundsDbContext>>();
            await RoundStore.TrackAsync(outbox.DbContext, round, ct).ConfigureAwait(false);
            await outbox.PublishAsync(integrationEvent).ConfigureAwait(false);
            await outbox.SaveChangesAndFlushMessagesAsync(ct).ConfigureAwait(false);
        }
    }
}
