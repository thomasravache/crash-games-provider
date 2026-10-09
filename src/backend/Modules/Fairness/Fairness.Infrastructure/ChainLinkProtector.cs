using System.Globalization;
using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;
using Microsoft.AspNetCore.DataProtection;

namespace Apogeu.Fairness.Infrastructure;

/// <summary>
/// Encrypts the secret links of a chain at rest with the host's Data Protection (ADR-0014). Each ciphertext is bound to
/// its chain and link index (purpose), so a stored checkpoint cannot be moved to another chain or position.
/// </summary>
internal sealed class ChainLinkProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Apogeu.Fairness.SeedChain", "v1");

    public byte[] Protect(ChainId chain, int linkIndex, Seed link)
    {
        var plain = link.ToArray();
        try
        {
            return For(chain, linkIndex).Protect(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public Seed Unprotect(ChainId chain, int linkIndex, byte[] protectedLink)
    {
        var plain = For(chain, linkIndex).Unprotect(protectedLink);
        try
        {
            return new Seed(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private IDataProtector For(ChainId chain, int linkIndex) =>
        _protector.CreateProtector(
            chain.Value.ToString("N", CultureInfo.InvariantCulture),
            linkIndex.ToString(CultureInfo.InvariantCulture));
}
