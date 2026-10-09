using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Apogeu.Operators.Infrastructure.Persistence;

/// <summary>
/// Design time only: <c>dotnet ef migrations add &lt;Name&gt; --project src/backend/Modules/Operators/Operators.Infrastructure
/// --output-dir Persistence/Migrations</c>, with <c>Microsoft.EntityFrameworkCore.Design</c> referenced only while generating
/// (as in the Fairness module). Generating a migration never connects, so the connection string has no credentials.
/// </summary>
internal sealed class OperatorsDbContextDesignFactory : IDesignTimeDbContextFactory<OperatorsDbContext>
{
    public OperatorsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OperatorsDbContext>();
        OperatorsInfrastructure.Configure(options, "Host=localhost;Database=apogeu");
        return new OperatorsDbContext(options.Options);
    }
}
