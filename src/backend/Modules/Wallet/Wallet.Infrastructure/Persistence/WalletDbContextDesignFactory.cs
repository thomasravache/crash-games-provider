using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Apogeu.Wallet.Infrastructure.Persistence;

/// <summary>
/// Design time only: <c>dotnet ef migrations add &lt;Name&gt; --project src/backend/Modules/Wallet/Wallet.Infrastructure
/// --output-dir Persistence/Migrations</c>, with <c>Microsoft.EntityFrameworkCore.Design</c> referenced only while generating
/// (as in the Fairness and Operators modules). Generating a migration never connects, so the connection string has no credentials.
/// </summary>
internal sealed class WalletDbContextDesignFactory : IDesignTimeDbContextFactory<WalletDbContext>
{
    public WalletDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WalletDbContext>();
        WalletInfrastructure.Configure(options, "Host=localhost;Database=apogeu");
        return new WalletDbContext(options.Options);
    }
}
