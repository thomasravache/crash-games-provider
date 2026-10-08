using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>
/// Test-only endpoint (IT-04) that logs a sensitive <c>token</c> property. It is added to the Host's pipeline from
/// the test through an <see cref="IStartupFilter"/>; it does not exist in production code.
/// </summary>
internal static partial class TokenLoggingEndpoint
{
    public const string Path = "/__tests__/token-log";
    public const string TokenValue = "abc";

    public static void Register(IServiceCollection services, string probe) =>
        services.AddSingleton<IStartupFilter>(new Filter(probe));

    [LoggerMessage(Level = LogLevel.Information, Message = "Teste de mascaramento {probe}: token={token}")]
    private static partial void LogToken(ILogger logger, string probe, string token);

    private sealed class Filter(string probe) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Map(Path, branch => branch.Run(context =>
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SPEC-0005.IT-04");
                LogToken(logger, probe, TokenValue);
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            }));
            next(app);
        };
    }
}
