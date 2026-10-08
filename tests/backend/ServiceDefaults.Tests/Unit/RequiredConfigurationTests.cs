using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Apogeu.ServiceDefaults.Tests.Unit;

public sealed class RequiredConfigurationTests
{
    private const string MissingKey = "ConnectionStrings:apogeu";

    [Fact(DisplayName = "Dada a configuração sem ConnectionStrings:apogeu, quando o Host valida as opções na inicialização, então lança erro citando a chave faltante")]
    [Trait("Spec", "SPEC-0005:UT-02")]
    public void GivenConfigurationWithoutApogeuConnectionStringWhenHostStartsThenStartupFailsNamingTheMissingKey()
    {
        // "Testing" keeps developer user-secrets (loaded only in Development) out of the configuration under test;
        // every other connection string the Host may need is present, so only the apogeu one is missing.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.UseSetting("ConnectionStrings:redis", "127.0.0.1:1,abortConnect=false,connectTimeout=200");
        });

        var exception = Record.Exception(() => factory.CreateClient().Dispose());

        Assert.True(exception is not null, "The Host started without ConnectionStrings:apogeu; it must fail at startup.");
        var messages = Flatten(exception).ToList();
        Assert.True(
            messages.Exists(message => message.Contains(MissingKey, StringComparison.Ordinal)),
            $"The startup error must name '{MissingKey}'. Messages:{Environment.NewLine}{string.Join(Environment.NewLine, messages)}");
    }

    private static IEnumerable<string> Flatten(Exception exception)
    {
        yield return exception.Message;
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions.SelectMany(Flatten))
            {
                yield return inner;
            }
        }
        else if (exception.InnerException is not null)
        {
            foreach (var inner in Flatten(exception.InnerException))
            {
                yield return inner;
            }
        }
    }
}
