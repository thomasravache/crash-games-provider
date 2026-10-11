using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>
/// Design time only: <c>dotnet ef migrations add &lt;Name&gt; --project src/backend/Modules/Betting/Betting.Infrastructure
/// --output-dir Persistence/Migrations</c>, with <c>Microsoft.EntityFrameworkCore.Design</c> referenced only while generating
/// (as in Rounds). Generating a migration never connects, so the connection string is a placeholder without credentials.
/// </summary>
internal sealed class BettingDbContextDesignFactory : IDesignTimeDbContextFactory<BettingDbContext>
{
    public BettingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BettingDbContext>();
        BettingInfrastructure.Configure(options, "Host=localhost;Database=apogeu");
        return new BettingDbContext(options.Options);
    }
}
