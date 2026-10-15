using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Apogeu.DemoCasino.Apogeu;

/// <summary>The named HttpClient "Apogeu": based on Apogeu:BaseUrl, every request signed by <see cref="OperatorSigningHandler"/>.</summary>
internal static class ApogeuClientRegistration
{
    public const string ClientName = "Apogeu";

    public static IServiceCollection AddApogeuClient(this IServiceCollection services)
    {
        services.AddOptions<ApogeuOptions>()
            .BindConfiguration(ApogeuOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<OperatorSigningHandler>();
        services.AddHttpClient(ClientName, (provider, client) =>
                client.BaseAddress = provider.GetRequiredService<IOptions<ApogeuOptions>>().Value.BaseUrl)
            .AddHttpMessageHandler<OperatorSigningHandler>();
        return services;
    }
}
