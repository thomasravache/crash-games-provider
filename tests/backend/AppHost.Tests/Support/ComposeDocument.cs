using YamlDotNet.RepresentationModel;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>
/// Read-only view of a docker-compose.yaml (YamlDotNet, the serializer Aspire itself writes it with): services, their
/// environment, published ports, volumes and memory limits, and the top-level volumes and networks. Both the map and the
/// list syntax of the Compose specification are read for the environment and the volumes.
/// </summary>
internal sealed class ComposeDocument
{
    private readonly YamlMappingNode _root;

    private ComposeDocument(YamlMappingNode root) => _root = root;

    public static ComposeDocument Load(string path)
    {
        using var reader = new StreamReader(path);
        var stream = new YamlStream();
        stream.Load(reader);
        Xunit.Assert.True(stream.Documents.Count == 1, $"{path} must hold one YAML document, found {stream.Documents.Count}.");
        var root = stream.Documents[0].RootNode as YamlMappingNode;
        Xunit.Assert.True(root is not null, $"{path} is not a YAML mapping.");
        return new ComposeDocument(root);
    }

    public IReadOnlyList<string> ServiceNames => Keys(Child(_root, "services"));

    public IReadOnlyList<string> VolumeNames => Keys(Child(_root, "volumes"));

    public IReadOnlyList<string> NetworkNames => Keys(Child(_root, "networks"));

    public bool HasService(string name) => Service(name) is not null;

    /// <summary>The environment of the service (map or <c>KEY=VALUE</c> list); empty when it declares none.</summary>
    public IReadOnlyDictionary<string, string> Environment(string service)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        switch (Child(RequiredService(service), "environment"))
        {
            case YamlMappingNode map:
                foreach (var (key, value) in map.Children)
                {
                    environment[Scalar(key)] = Scalar(value);
                }

                break;
            case YamlSequenceNode list:
                foreach (var entry in list.Children.Select(Scalar))
                {
                    var separator = entry.IndexOf('=', StringComparison.Ordinal);
                    environment[separator < 0 ? entry : entry[..separator]] = separator < 0 ? string.Empty : entry[(separator + 1)..];
                }

                break;
        }

        return environment;
    }

    /// <summary>The <c>ports</c> entries of the service (published on the host), as written.</summary>
    public IReadOnlyList<string> Ports(string service) =>
        Child(RequiredService(service), "ports") is YamlSequenceNode ports ? ports.Children.Select(Describe).ToList() : [];

    /// <summary>The volumes mounted by the service, as (source, target); the short syntax <c>source:target[:mode]</c> too.</summary>
    public IReadOnlyList<(string Source, string Target)> Mounts(string service)
    {
        if (Child(RequiredService(service), "volumes") is not YamlSequenceNode volumes)
        {
            return [];
        }

        return volumes.Children.Select(volume => volume switch
        {
            YamlMappingNode map => (Scalar(Child(map, "source")), Scalar(Child(map, "target"))),
            _ => ShortMount(Scalar(volume)),
        }).ToList();
    }

    /// <summary>
    /// The memory limit of the service: <c>deploy.resources.limits.memory</c> or <c>mem_limit</c>; <see langword="null"/>
    /// when it has none.
    /// </summary>
    public string? MemoryLimit(string service)
    {
        var node = RequiredService(service);
        var deployLimit = Child(Child(Child(Child(node, "deploy"), "resources"), "limits"), "memory");
        var value = deployLimit is not null ? Scalar(deployLimit) : Scalar(Child(node, "mem_limit"));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>The image of the service, as written.</summary>
    public string Image(string service) => Scalar(Child(RequiredService(service), "image"));

    /// <summary>The <c>user</c> of the service, or empty when it runs as the image's user.</summary>
    public string User(string service) => Scalar(Child(RequiredService(service), "user"));

    /// <summary>The entrypoint followed by the command of the service (list or string syntax), as written.</summary>
    public IReadOnlyList<string> EntrypointAndCommand(string service)
    {
        var node = RequiredService(service);
        return [.. Words(Child(node, "entrypoint")), .. Words(Child(node, "command"))];
    }

    /// <summary>The services this one depends on, with their condition (empty for the list syntax).</summary>
    public IReadOnlyDictionary<string, string> DependsOn(string service)
    {
        var dependencies = new Dictionary<string, string>(StringComparer.Ordinal);
        switch (Child(RequiredService(service), "depends_on"))
        {
            case YamlMappingNode map:
                foreach (var (key, value) in map.Children)
                {
                    dependencies[Scalar(key)] = Scalar(Child(value, "condition"));
                }

                break;
            case YamlSequenceNode list:
                foreach (var entry in list.Children)
                {
                    dependencies[Scalar(entry)] = string.Empty;
                }

                break;
        }

        return dependencies;
    }

    private static List<string> Words(YamlNode? node) => node switch
    {
        YamlSequenceNode list => list.Children.Select(Scalar).ToList(),
        YamlScalarNode scalar when !string.IsNullOrEmpty(scalar.Value) => [scalar.Value],
        _ => [],
    };

    private YamlMappingNode? Service(string name) => Child(Child(_root, "services"), name) as YamlMappingNode;

    private YamlMappingNode RequiredService(string name)
    {
        var service = Service(name);
        Xunit.Assert.True(service is not null, $"The compose declares no service '{name}'. Services: {string.Join(", ", ServiceNames)}.");
        return service;
    }

    private static (string Source, string Target) ShortMount(string text)
    {
        var parts = text.Split(':');
        return parts.Length >= 2 ? (parts[0], parts[1]) : (string.Empty, parts[0]);
    }

    private static YamlNode? Child(YamlNode? node, string key) =>
        node is YamlMappingNode map && map.Children.TryGetValue(new YamlScalarNode(key), out var child) ? child : null;

    private static List<string> Keys(YamlNode? node) =>
        node is YamlMappingNode map ? map.Children.Keys.Select(Scalar).ToList() : [];

    private static string Scalar(YamlNode? node) => node is YamlScalarNode scalar ? scalar.Value ?? string.Empty : string.Empty;

    private static string Describe(YamlNode node) => node is YamlScalarNode scalar ? scalar.Value ?? string.Empty : node.ToString();
}
