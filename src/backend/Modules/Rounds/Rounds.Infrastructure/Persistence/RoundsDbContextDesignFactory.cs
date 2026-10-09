using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Apogeu.Rounds.Infrastructure.Persistence;

/// <summary>
/// Design time only: <c>dotnet ef migrations add &lt;Name&gt; --project src/backend/Modules/Rounds/Rounds.Infrastructure
/// --output-dir Persistence/Migrations</c>, with <c>Microsoft.EntityFrameworkCore.Design</c> referenced only while generating
/// (as in Fairness: it lifts EF Core Relational above the version Npgsql resolves for the Host). Generating a migration
/// never connects, so the connection string is a placeholder without credentials.
/// </summary>
internal sealed class RoundsDbContextDesignFactory : IDesignTimeDbContextFactory<RoundsDbContext>
{
    public RoundsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RoundsDbContext>();
        RoundsInfrastructure.Configure(options, "Host=localhost;Database=apogeu");
        return new RoundsDbContext(options.Options);
    }
}
