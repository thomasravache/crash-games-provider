using System.Collections.Concurrent;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// An <see cref="IRoundStore"/> for the snapshot reads of a room (G4 delta review of IT-07): read <c>n</c> (one
/// <c>FindLatestAsync</c>) answers a round whose number is <c>n</c>, so a snapshot tells which read produced it. When gated,
/// read <c>n</c> waits until the test releases (or fails) it, or until its token is cancelled, as a database read honours
/// its deadline; otherwise every read answers at once.
/// </summary>
internal sealed class GatedRoundStore(bool gated = true) : IRoundStore
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource> _started = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<Exception?>> _gates = new();
    private readonly ConcurrentDictionary<int, CancellationToken> _tokens = new();
    private int _reads;
    private int _active;
    private int _maxActive;

    /// <summary>How many reads started.</summary>
    public int Reads => Volatile.Read(ref _reads);

    /// <summary>The most reads that ran at the same time.</summary>
    public int MaxConcurrentReads => Volatile.Read(ref _maxActive);

    /// <summary>The token read <paramref name="read"/> was given.</summary>
    public CancellationToken TokenOf(int read) => _tokens[read];

    /// <summary>Completes once read <paramref name="read"/> started.</summary>
    public Task StartedAsync(int read) => Started(read).Task.WaitAsync(TimeSpan.FromSeconds(10));

    /// <summary>Lets read <paramref name="read"/> answer.</summary>
    public void Release(int read) => Gate(read).TrySetResult(null);

    /// <summary>Makes read <paramref name="read"/> throw <paramref name="error"/>.</summary>
    public void Fail(int read, Exception error) => Gate(read).TrySetResult(error);

    public async Task<Round?> FindLatestAsync(string salaId, CancellationToken ct)
    {
        var read = Interlocked.Increment(ref _reads);
        _tokens[read] = ct;
        var active = Interlocked.Increment(ref _active);
        InterlockedMax(ref _maxActive, active);
        try
        {
            Started(read).TrySetResult();
            var error = await Gate(read).Task.WaitAsync(ct).ConfigureAwait(false);
            if (error is not null)
            {
                throw error;
            }

            return Round.OpenBetting(new RoundId(Guid.NewGuid()), salaId, read, Guid.NewGuid(), 1, $"commitment-{read}", DateTimeOffset.UnixEpoch);
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }

    public Task<IReadOnlyList<Round>> ListCrashedAsync(string salaId, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Round>>([]);

    public Task<Round?> FindAsync(RoundId id, CancellationToken ct) => throw new NotSupportedException();

    public Task SaveAsync(Round round, CancellationToken ct) => throw new NotSupportedException();

    private TaskCompletionSource Started(int read) =>
        _started.GetOrAdd(read, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

    private TaskCompletionSource<Exception?> Gate(int read) =>
        _gates.GetOrAdd(read, _ =>
        {
            var gate = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!gated)
            {
                gate.SetResult(null);
            }

            return gate;
        });

    private static void InterlockedMax(ref int target, int value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var seen = Interlocked.CompareExchange(ref target, value, current);
            if (seen == current)
            {
                return;
            }

            current = seen;
        }
    }
}
