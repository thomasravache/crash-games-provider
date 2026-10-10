using System.Data.Common;
using Apogeu.Betting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// The ambiguous commit of IT-08: once armed, the next write of the module's <c>BettingDbContext</c> that reaches
/// PostgreSQL — a committed transaction, or a write command outside a transaction (auto-commit) — is reported to the caller
/// as a lost connection, although the database kept it. Added to the module's own context with
/// <c>ConfigureDbContext&lt;BettingDbContext&gt;</c> (EF Core), without touching production code.
/// </summary>
internal sealed class CommitFault : DbTransactionInterceptor, IDbCommandInterceptor
{
    private int _armed;
    private int _injected;

    /// <summary>How many writes were reported as failed after reaching the database.</summary>
    public int Injected => Volatile.Read(ref _injected);

    public static CommitFault AddTo(IServiceCollection services)
    {
        var fault = new CommitFault();

        // The context is internal to Betting.Infrastructure: ConfigureDbContext<BettingDbContext> is bound by reflection.
        var context = typeof(IBetStore).Assembly.GetType("Apogeu.Betting.Infrastructure.Persistence.BettingDbContext", throwOnError: true)!;
        var configure = typeof(EntityFrameworkServiceCollectionExtensions).GetMethods()
            .Single(m => string.Equals(m.Name, nameof(EntityFrameworkServiceCollectionExtensions.ConfigureDbContext), StringComparison.Ordinal)
                && m.GetParameters() is [_, { ParameterType: var action }, _]
                && action == typeof(Action<IServiceProvider, DbContextOptionsBuilder>))
            .MakeGenericMethod(context);
        Action<IServiceProvider, DbContextOptionsBuilder> addInterceptor = (_, options) => options.AddInterceptors(fault);
        configure.Invoke(null, [services, addInterceptor, ServiceLifetime.Scoped]);
        return fault;
    }

    public void ArmNextWrite() => Volatile.Write(ref _armed, 1);

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Fire();
        return Task.CompletedTask;
    }

    public ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Transaction is null && IsWrite(command))
        {
            Fire();
        }

        return ValueTask.FromResult(result);
    }

    public ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Transaction is null && IsWrite(command))
        {
            Fire();
        }

        return ValueTask.FromResult(result);
    }

    private static bool IsWrite(DbCommand command)
    {
        var text = command.CommandText.TrimStart();
        return text.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase);
    }

    private void Fire()
    {
        if (Interlocked.Exchange(ref _armed, 0) == 1)
        {
            Interlocked.Increment(ref _injected);
            throw new IOException("Connection lost after the write reached PostgreSQL (fault injected by IT-08).");
        }
    }
}
