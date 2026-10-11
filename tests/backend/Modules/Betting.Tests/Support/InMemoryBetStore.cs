using System.Reflection;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Infrastructure.Persistence;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// In-memory double of <see cref="IBetStore"/> for the unit tests of the cancellation (UT-07) and of the cash-out settlement
/// (SPEC-0023 UT-05), built as a <see cref="DispatchProxy"/> so it keeps compiling when the store gains members: it answers
/// <c>FindAsync</c>, <c>FindByIdAsync</c>, <c>TransitionAsync</c>, <c>CashOutAsync</c> and <c>ConfirmCreditAsync</c> over the bets
/// the test seeded, and any other member fails the test naming it.
/// </summary>
public class InMemoryBetStore : DispatchProxy
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, (AcceptedBet Accepted, BetStatus Status)> _bets = [];
    private readonly Dictionary<Guid, CashOutRecord> _cashOuts = [];

    public static (IBetStore Store, InMemoryBetStore State) Create()
    {
        var store = Create<IBetStore, InMemoryBetStore>();
        return (store, (InMemoryBetStore)(object)store);
    }

    public void Seed(AcceptedBet accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        lock (_gate)
        {
            _bets[accepted.Bet.Id] = (accepted, accepted.Bet.Status);
        }
    }

    public BetStatus StatusOf(Guid betId)
    {
        lock (_gate)
        {
            return _bets[betId].Status;
        }
    }

    /// <summary>The cash-out stored for <paramref name="betId"/>, if any (multiplier, payout and whether it was automatic).</summary>
    public CashOutRecord? CashOutOf(Guid betId)
    {
        lock (_gate)
        {
            return _cashOuts.GetValueOrDefault(betId);
        }
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        ArgumentNullException.ThrowIfNull(args);
        return targetMethod.Name switch
        {
            nameof(IBetStore.FindAsync) => Task.FromResult(Find((BetOwner)args[0]!, (Guid)args[1]!)),
            nameof(IBetStore.FindByIdAsync) => Task.FromResult(Find(owner: null, (Guid)args[0]!)),
            nameof(IBetStore.CashOutAsync) => Task.FromResult(CashOut((Bet)args[0]!, (bool)args[1]!)),
            nameof(IBetStore.ConfirmCreditAsync) => Task.FromResult(ConfirmCredit((Guid)args[0]!, (decimal)args[1]!)),
            nameof(IBetStore.TransitionAsync) => Task.FromResult(Transition((Bet)args[0]!, (BetStatus)args[1]!)),
            _ => throw new NotSupportedException($"The UT-07 store double does not answer IBetStore.{targetMethod.Name}."),
        };
    }

    private AcceptedBet? Find(BetOwner? owner, Guid betId)
    {
        lock (_gate)
        {
            if (!_bets.TryGetValue(betId, out var entry) || (owner is not null && entry.Accepted.Bet.Owner != owner))
            {
                return null;
            }

            var bet = entry.Accepted.Bet;
            var current = Bet.Restore(
                bet.Id,
                bet.Owner,
                new BetRequest(bet.ClientBetId, bet.Panel, bet.Amount, bet.AutoCashOut),
                entry.Status,
                bet.RoundId,
                bet.RoundNumber,
                _cashOuts.GetValueOrDefault(betId)?.Multiplier,
                _cashOuts.GetValueOrDefault(betId)?.Payout);
            return entry.Accepted with { Bet = current, CashOut = _cashOuts.GetValueOrDefault(betId) };
        }
    }

    private bool Transition(Bet bet, BetStatus expected)
    {
        lock (_gate)
        {
            if (!_bets.TryGetValue(bet.Id, out var entry) || entry.Status != expected)
            {
                return false;
            }

            _bets[bet.Id] = (entry.Accepted, bet.Status);
            return true;
        }
    }

    private bool CashOut(Bet bet, bool automatic)
    {
        lock (_gate)
        {
            if (!_bets.TryGetValue(bet.Id, out var entry) || entry.Status != BetStatus.Active || bet.Status != BetStatus.CashedOut)
            {
                return false;
            }

            _bets[bet.Id] = (entry.Accepted, BetStatus.CashedOut);
            _cashOuts[bet.Id] = new CashOutRecord(bet.CashOutMultiplier!.Value, bet.Payout!.Value, automatic, Credited: false, Balance: null);
            return true;
        }
    }

    private bool ConfirmCredit(Guid betId, decimal balance)
    {
        lock (_gate)
        {
            if (!_cashOuts.TryGetValue(betId, out var cashOut) || cashOut.Credited)
            {
                return false;
            }

            _cashOuts[betId] = cashOut with { Credited = true, Balance = balance };
            return true;
        }
    }
}
