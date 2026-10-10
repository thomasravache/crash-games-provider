namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>A bet inserted in an open transaction (<see cref="IBetStore.ReserveAsync"/>); disposed without commit, it is undone.</summary>
public interface IBetReservation : IAsyncDisposable
{
    /// <summary>Records the balance after the debit and commits the bet.</summary>
    Task<AcceptedBet> CommitAsync(decimal balanceAfter, CancellationToken ct);
}
