using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>
/// Double of the SPEC-0020@1 contract <see cref="IOperatorLimits"/>: the demo operator has the BRL limits 1,00 / 1.000,00 /
/// 100.000,00; any other operator or currency is <c>CURRENCY_NOT_ENABLED</c>, as the real port.
/// </summary>
internal sealed class FixedOperatorLimits : IOperatorLimits
{
    private static readonly BetLimits Brl = new(
        WalletExpectations.Brl,
        new Money(WalletExpectations.MinBet),
        new Money(1_000.00m),
        new Money(100_000.00m));

    public BetLimits Get(OperatorId op, Currency currency) =>
        op == WalletExpectations.Demo && currency == WalletExpectations.Brl
            ? Brl
            : throw new CurrencyNotEnabledException(op, currency);
}
