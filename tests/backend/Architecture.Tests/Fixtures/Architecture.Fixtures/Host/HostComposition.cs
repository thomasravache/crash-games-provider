using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Host;

/// <summary>
/// Known violation (UT-02): the Host registers AutoMapper through its extension method, which lives in namespace
/// Microsoft.Extensions.DependencyInjection inside the AutoMapper assembly (ADR-0002).
/// </summary>
public static class HostComposition
{
    public static IList<object> Compose(IList<object> services) => services.AddAutoMapper();
}
