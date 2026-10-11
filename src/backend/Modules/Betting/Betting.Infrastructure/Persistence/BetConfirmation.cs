namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>Outcome of <see cref="IBetStore.ConfirmAsync"/>.</summary>
public enum BetConfirmation
{
    /// <summary>The reservation became the accepted bet.</summary>
    Confirmed,

    /// <summary>The reservation is gone: the sweeper voided it (and reverses its debit).</summary>
    Voided,

    /// <summary>Another bet of the player took the panel meanwhile; the reservation is still pending.</summary>
    PanelTaken,
}
