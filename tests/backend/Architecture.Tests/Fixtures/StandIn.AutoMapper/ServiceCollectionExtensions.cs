namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Stand-in for AutoMapper's registration extension (SPEC-0003 UT-02). The real package declares it in this same
/// namespace, so the dependency is only recognisable by the assembly it comes from.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static TServices AddAutoMapper<TServices>(this TServices services)
        where TServices : class => services;
}
