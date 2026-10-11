using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Betting.Tests.Support;

/// <summary>Wraps the Host's own registration of a service with a test decorator (the real service stays underneath).</summary>
internal static class ServiceDecorators
{
    public static IServiceCollection Decorate<TService>(this IServiceCollection services, Func<TService, IServiceProvider, TService> decorate)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(decorate);
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService))
            ?? throw new InvalidOperationException($"The Host registers no {typeof(TService).Name} to decorate.");
        services.Remove(descriptor);
        services.Add(ServiceDescriptor.Describe(
            typeof(TService),
            provider => decorate(Inner<TService>(descriptor, provider), provider),
            descriptor.Lifetime));
        return services;
    }

    private static TService Inner<TService>(ServiceDescriptor descriptor, IServiceProvider provider)
        where TService : class =>
        descriptor switch
        {
            { ImplementationInstance: TService instance } => instance,
            { ImplementationFactory: { } factory } => (TService)factory(provider),
            { ImplementationType: { } type } => (TService)ActivatorUtilities.CreateInstance(provider, type),
            _ => throw new InvalidOperationException($"Unsupported registration of {typeof(TService).Name}."),
        };
}
