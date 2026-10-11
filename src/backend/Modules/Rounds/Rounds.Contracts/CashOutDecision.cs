namespace Apogeu.Rounds.Contracts;

/// <summary>Decision of <see cref="ICashOutArbiter.DecideAsync"/>: <see cref="CashOutAccepted"/> or <see cref="CashOutTooLate"/>.</summary>
public abstract record CashOutDecision
{
    private protected CashOutDecision()
    {
    }
}
