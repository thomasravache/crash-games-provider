using System.Security.Cryptography;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.LiveBets;

/// <summary>
/// Read projection of the live bets of each room (SPEC-0025 §4, ADR-0019): records the bets and cash-outs of the current
/// round and groups them in windows of <see cref="Window"/>; each batch carries at most <see cref="MaxItems"/> bets (the
/// highest amounts) and <see cref="MaxItems"/> cash-outs, the rest only counting in the totals. Times come from the
/// injected <see cref="TimeProvider"/>. Thread-safe: the bets are recorded by concurrent commands while the broadcaster
/// takes the due batches.
/// </summary>
public sealed class LiveBetsBatcher(TimeProvider time)
{
    /// <summary>Window of a batch: at most one batch per room every 200 ms.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMilliseconds(200);

    /// <summary>Most bets (and most cash-outs) of a batch or of a snapshot.</summary>
    public const int MaxItems = 100;

    /// <summary>Random bytes of a <c>betRef</c> (base64url, 16 characters): opaque, never derived from the bet id.</summary>
    private const int BetRefBytes = 12;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, SalaState> _salas = new(StringComparer.Ordinal);

    /// <summary>Records a bet accepted in its round; a new round of the room restarts its totals.</summary>
    public void Record(LiveBetPlaced placed)
    {
        ArgumentNullException.ThrowIfNull(placed);
        lock (_gate)
        {
            var sala = RoundOf(placed.SalaId, placed.RoundId);
            if (sala.Bets.ContainsKey(placed.BetId))
            {
                return;
            }

            var bet = new LiveEntry(
                NewBetRef(),
                LiveBetNickname.Anonymize(placed.Nickname),
                placed.Amount,
                placed.Owner.Currency.Code,
                PlayerKey(placed.Owner),
                ++sala.Sequence);
            sala.Bets.Add(placed.BetId, bet);
            sala.Players[bet.Player] = sala.Players.GetValueOrDefault(bet.Player) + 1;
            sala.Wagered += placed.Amount.Value;
            sala.PendingPlaced.Add(placed.BetId);
            sala.HasNews = true;
        }
    }

    /// <summary>Records a cash-out of a bet recorded before (one of another round, or unknown, is ignored).</summary>
    public void Record(LiveBetCashedOut cashedOut)
    {
        ArgumentNullException.ThrowIfNull(cashedOut);
        lock (_gate)
        {
            if (!_salas.TryGetValue(cashedOut.SalaId, out var sala)
                || sala.RoundId != cashedOut.RoundId
                || !sala.Bets.TryGetValue(cashedOut.BetId, out var bet)
                || bet.Multiplier is not null)
            {
                return;
            }

            bet.Multiplier = cashedOut.Multiplier;
            bet.Payout = cashedOut.Payout;
            sala.PendingCashOuts.Add(cashedOut.BetId);
            sala.HasNews = true;
        }
    }

    /// <summary>
    /// Forgets a cancelled bet of the current round of <paramref name="salaId"/>: it leaves the snapshot and the totals, which
    /// the next batch carries.
    /// </summary>
    public void Forget(string salaId, Guid roundId, Guid betId)
    {
        lock (_gate)
        {
            if (!_salas.TryGetValue(salaId, out var sala) || sala.RoundId != roundId || !sala.Bets.Remove(betId, out var bet))
            {
                return;
            }

            var bets = sala.Players[bet.Player] - 1;
            if (bets == 0)
            {
                sala.Players.Remove(bet.Player);
            }
            else
            {
                sala.Players[bet.Player] = bets;
            }

            sala.Wagered -= bet.Amount.Value;
            sala.PendingPlaced.Remove(betId);
            sala.PendingCashOuts.Remove(betId);
            sala.HasNews = true;
        }
    }

    /// <summary>The room whose current round is <paramref name="roundId"/>; <see langword="null"/> when no room has it.</summary>
    public string? SalaOf(Guid roundId)
    {
        lock (_gate)
        {
            foreach (var (salaId, sala) in _salas)
            {
                if (sala.RoundId == roundId)
                {
                    return salaId;
                }
            }

            return null;
        }
    }

    /// <summary>The batches of the rooms whose window elapsed with news, removing that news from the pending window.</summary>
    public IReadOnlyList<SalaLiveBetsBatch> TakeDue()
    {
        var now = time.GetUtcNow();
        lock (_gate)
        {
            List<SalaLiveBetsBatch> due = [];
            foreach (var (salaId, sala) in _salas)
            {
                // At most one batch per room every window; the first news after a quiet window goes at once.
                if (!sala.HasNews || (sala.LastSentAt is { } last && now < last + Window))
                {
                    continue;
                }

                due.Add(new SalaLiveBetsBatch(salaId, BatchOf(sala)));
                sala.PendingPlaced.Clear();
                sala.PendingCashOuts.Clear();
                sala.HasNews = false;
                sala.LastSentAt = now;
            }

            return due;
        }
    }

    /// <summary>The snapshot of the current round of <paramref name="salaId"/>; <see langword="null"/> when none was recorded.</summary>
    public LiveBetsSnapshot? SnapshotOf(string salaId)
    {
        lock (_gate)
        {
            if (!_salas.TryGetValue(salaId, out var sala))
            {
                return null;
            }

            var bets = Highest(sala.Bets.Values)
                .Select(b => new LiveBet(b.BetRef, b.Nickname, b.Amount, b.Currency, b.Multiplier, b.Payout))
                .ToList();
            return new LiveBetsSnapshot(sala.RoundId, sala.Bets.Count, sala.Players.Count, new Money(sala.Wagered), bets);
        }
    }

    /// <summary>The news of the pending window: the highest bets and payouts first, with the totals of the round.</summary>
    private static LiveBetsBatch BatchOf(SalaState sala)
    {
        var placed = Highest(sala.PendingPlaced.Select(id => sala.Bets[id]))
            .Select(b => new LiveBet(b.BetRef, b.Nickname, b.Amount, b.Currency, Multiplier: null, Payout: null))
            .ToList();
        var cashedOut = sala.PendingCashOuts
            .Select(id => sala.Bets[id])
            .Where(b => b is { Multiplier: not null, Payout: not null })
            .OrderByDescending(b => b.Payout!.Value.Value)
            .ThenBy(b => b.Sequence)
            .Take(MaxItems)
            .Select(b => new LiveCashOut(b.BetRef, b.Multiplier!.Value, b.Payout!.Value))
            .ToList();
        return new LiveBetsBatch(sala.RoundId, placed, cashedOut, sala.Bets.Count, sala.Players.Count, new Money(sala.Wagered));
    }

    /// <summary>The <see cref="MaxItems"/> highest amounts, ties in the order they were recorded.</summary>
    private static IEnumerable<LiveEntry> Highest(IEnumerable<LiveEntry> bets) =>
        bets.OrderByDescending(b => b.Amount.Value).ThenBy(b => b.Sequence).Take(MaxItems);

    /// <summary>The state of <paramref name="salaId"/> for <paramref name="roundId"/>: a new round starts it over (keeping the pace).</summary>
    private SalaState RoundOf(string salaId, Guid roundId)
    {
        if (_salas.TryGetValue(salaId, out var sala) && sala.RoundId == roundId)
        {
            return sala;
        }

        // News of the previous round not sent yet is dropped: the room moved on to the new round.
        var next = new SalaState(roundId) { LastSentAt = sala?.LastSentAt };
        _salas[salaId] = next;
        return next;
    }

    private static string NewBetRef() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(BetRefBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Distinct players per operator: the same player id at two operators are two players.</summary>
    private static string PlayerKey(BetOwner owner) => owner.OperatorId.Value + "\u001f" + owner.PlayerId.Value;

    /// <summary>The current round of a room: its bets, its totals and the news of the pending window.</summary>
    private sealed class SalaState(Guid roundId)
    {
        public Guid RoundId { get; } = roundId;

        public Dictionary<Guid, LiveEntry> Bets { get; } = [];

        /// <summary>Bets of the round per player.</summary>
        public Dictionary<string, int> Players { get; } = new(StringComparer.Ordinal);

        public decimal Wagered { get; set; }

        public long Sequence { get; set; }

        public HashSet<Guid> PendingPlaced { get; } = [];

        public HashSet<Guid> PendingCashOuts { get; } = [];

        /// <summary>Something changed since the last batch (a bet, a cash-out or a cancellation).</summary>
        public bool HasNews { get; set; }

        public DateTimeOffset? LastSentAt { get; set; }
    }

    /// <summary>A bet as the room sees it, with the player it counts for.</summary>
    private sealed class LiveEntry(string betRef, string nickname, Money amount, string currency, string player, long sequence)
    {
        public string BetRef { get; } = betRef;

        public string Nickname { get; } = nickname;

        public Money Amount { get; } = amount;

        public string Currency { get; } = currency;

        public string Player { get; } = player;

        public long Sequence { get; } = sequence;

        public Multiplier? Multiplier { get; set; }

        public Money? Payout { get; set; }
    }
}
