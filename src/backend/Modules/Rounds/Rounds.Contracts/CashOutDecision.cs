namespace Apogeu.Rounds.Contracts;

/// <summary>Decision of <see cref="ICashOutArbiter.DecideAsync"/>: <see cref="CashOutAccepted"/>, <see cref="CashOutTooLate"/> or <see cref="CashOutUndecided"/>.</summary>
public abstract record CashOutDecision
{
    private protected CashOutDecision()
    {
    }
}
