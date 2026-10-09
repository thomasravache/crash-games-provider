using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Infrastructure;
using Apogeu.TestKit.Logging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Apogeu.Fairness.Tests.Unit;

/// <summary>
/// UT-08: the length guard of <c>CreateChainAsync</c>, through the public composition of the module and without a database.
/// The connection string points at a closed local port, so any attempt to persist fails with a database error instead of
/// <see cref="ArgumentOutOfRangeException"/>; the captured EF Core logs prove that no save or connection was even attempted.
/// </summary>
public sealed class ChainLengthValidationTests
{
    private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=fairness;Username=apogeu;Timeout=1;Pooling=false";

    [Theory(DisplayName = "Dado length 0 ou 1.000.001, quando CreateChainAsync é chamado, então lança ArgumentOutOfRangeException sem persistir cadeia")]
    [InlineData(0)]
    [InlineData(1_000_001)]
    [Trait("Spec", "SPEC-0007:UT-08")]
    public async Task GivenLengthOutsideOneToOneMillionWhenCreateChainThenArgumentOutOfRangeAndNothingPersisted(int length)
    {
        var ct = TestContext.Current.CancellationToken;
        var capture = new LogCapture();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddFairnessInfrastructure(UnreachableDatabase);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var fairness = scope.ServiceProvider.GetRequiredService<IFairnessService>();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => fairness.CreateChainAsync(new SalaId(Guid.NewGuid()), length, ct));

        Assert.Equal("length", exception.ParamName);
        var databaseWork = capture.Entries
            .Where(entry => entry.Category.StartsWith("Microsoft.EntityFrameworkCore.Update", StringComparison.Ordinal)
                || entry.Category.StartsWith("Microsoft.EntityFrameworkCore.Database", StringComparison.Ordinal))
            .Select(entry => entry.Category + ": " + entry.Message)
            .ToList();
        Assert.True(databaseWork.Count == 0, "CreateChainAsync tried to persist:" + Environment.NewLine + string.Join(Environment.NewLine, databaseWork));
    }
}
