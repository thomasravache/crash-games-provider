using System.Globalization;

namespace Apogeu.Fairness.Tests.Support;

/// <summary>One vector of tests/vectors/fairness-v1.json, as written in the file (hex and decimal strings).</summary>
public sealed record FairnessVector(
    string Description,
    string Seed,
    string PreviousSeed,
    string Salt,
    string Rtp,
    string Cap,
    string CrashPoint)
{
    public decimal RtpValue => decimal.Parse(Rtp, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    public decimal CapValue => decimal.Parse(Cap, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    public decimal CrashPointValue => decimal.Parse(CrashPoint, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
}
