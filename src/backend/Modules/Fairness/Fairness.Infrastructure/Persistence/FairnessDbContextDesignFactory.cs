using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Apogeu.Fairness.Infrastructure.Persistence;

/// <summary>
/// Design time only: <c>dotnet ef migrations add &lt;Name&gt; --project src/backend/Modules/Fairness/Fairness.Infrastructure
/// --output-dir Persistence/Migrations</c>, with <c>Microsoft.EntityFrameworkCore.Design</c> referenced only while generating
/// (kept out of the project: it lifts EF Core Relational above the version Npgsql resolves for the Host). Generating a
/// migration never connects, so the connection string is a placeholder without credentials.
/// </summary>
internal sealed class FairnessDbContextDesignFactory : IDesignTimeDbContextFactory<FairnessDbContext>
{
    public FairnessDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FairnessDbContext>();
        FairnessInfrastructure.Configure(options, "Host=localhost;Database=apogeu");
        return new FairnessDbContext(options.Options);
    }
}
