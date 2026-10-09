using System.Globalization;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>One vector of tests/vectors/fairness-v1.json (SPEC-0007), as written in the file.</summary>
internal sealed record FairnessVector(string Seed, string PreviousSeed, string Salt, string Rtp, string Cap, string CrashPoint)
{
    public decimal RtpValue => decimal.Parse(Rtp, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    public decimal CapValue => decimal.Parse(Cap, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    public decimal CrashPointValue => decimal.Parse(CrashPoint, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
}
