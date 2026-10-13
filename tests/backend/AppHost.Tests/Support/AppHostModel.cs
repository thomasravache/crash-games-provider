using System.Globalization;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>
/// Builds the AppHost model (Aspire.Hosting.Testing, run mode) without starting it: no container, process or port is
/// allocated. Environment variables are read from the environment callbacks as declared, so an endpoint reference is
/// described by its shape (resource and endpoint), since its port is only known after the start.
/// </summary>
internal static class AppHostModel
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(2);

    public static async Task<DistributedApplication> BuildAsync(string[] args, CancellationToken cancellationToken)
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AppHost>(args, cancellationToken)
            .WaitAsync(BuildTimeout, cancellationToken)
            .ConfigureAwait(false);

        return await builder.BuildAsync(cancellationToken).WaitAsync(BuildTimeout, cancellationToken).ConfigureAwait(false);
    }

    public static IResource Resource(DistributedApplication app, string name)
    {
        var model = (DistributedApplicationModel?)app.Services.GetService(typeof(DistributedApplicationModel));
        var resource = model?.Resources.SingleOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
        Xunit.Assert.True(resource is not null, $"The AppHost does not declare the resource '{name}'.");
        return resource;
    }

    /// <summary>
    /// Runs the environment callbacks of the resource and describes the requested variables (absent ones are left out).
    /// Only these are described, so the generated secrets of the AppHost are never resolved here.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> EnvironmentAsync(
        DistributedApplication app,
        string resourceName,
        IEnumerable<string> names,
        CancellationToken cancellationToken)
    {
        var resource = Resource(app, resourceName);
        var executionContext = (DistributedApplicationExecutionContext?)app.Services.GetService(typeof(DistributedApplicationExecutionContext));
        Xunit.Assert.True(executionContext is not null, "Setup failed: the AppHost has no execution context.");
        Xunit.Assert.True(executionContext.IsRunMode, "Setup failed: the AppHost model was not built in run mode.");

        var raw = new Dictionary<string, object>(StringComparer.Ordinal);
        var context = new EnvironmentCallbackContext(executionContext, resource, raw, cancellationToken);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context).ConfigureAwait(false);
        }

        var described = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (raw.TryGetValue(name, out var value))
            {
                described[name] = await DescribeAsync(value, cancellationToken).ConfigureAwait(false);
            }
        }

        return described;
    }

    /// <summary>The http endpoint declared on the resource.</summary>
    public static EndpointAnnotation HttpEndpoint(DistributedApplication app, string resourceName)
    {
        var endpoint = Resource(app, resourceName).Annotations.OfType<EndpointAnnotation>()
            .SingleOrDefault(candidate => string.Equals(candidate.Name, "http", StringComparison.Ordinal));
        Xunit.Assert.True(endpoint is not null, $"The resource '{resourceName}' declares no 'http' endpoint.");
        return endpoint;
    }

    /// <summary>Shape of a local endpoint reference, as returned by <see cref="EnvironmentAsync"/>.</summary>
    public static string Endpoint(string resourceName, string endpointName = "http") =>
        $"endpoint:{resourceName}/{endpointName}";

    /// <summary>
    /// A plain value as itself; an endpoint reference as <c>endpoint:&lt;resource&gt;/&lt;endpoint&gt;</c>; an expression
    /// made of a single value as that value; any other expression by its manifest expression.
    /// </summary>
    private static async Task<string> DescribeAsync(object value, CancellationToken cancellationToken) => value switch
    {
        string text => text,
        EndpointReference endpoint => Endpoint(endpoint.Resource.Name, endpoint.EndpointName),
        EndpointReferenceExpression { Property: EndpointProperty.Url } expression =>
            Endpoint(expression.Endpoint.Resource.Name, expression.Endpoint.EndpointName),
        ReferenceExpression { ValueProviders.Count: 0 } expression => expression.Format,
        ReferenceExpression { Format: "{0}", ValueProviders.Count: 1 } expression =>
            await DescribeAsync(expression.ValueProviders[0], cancellationToken).ConfigureAwait(false),
        ParameterResource parameter => await parameter.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty,
        IManifestExpressionProvider expression => expression.ValueExpression,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
