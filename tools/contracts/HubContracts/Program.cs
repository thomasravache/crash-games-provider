using Apogeu.HubContracts;

// Usage: dotnet Apogeu.HubContracts.dll [--assembly <path.dll>] --output <manifest.json>
//   --assembly  exports only the hubs of that assembly; without it, the Apogeu assemblies next to this tool are scanned.
const string Usage = "Usage: dotnet Apogeu.HubContracts.dll [--assembly <path.dll>] --output <manifest.json>";

var (assembly, output) = args switch
{
    ["--output", var o] => (null, o),
    ["--assembly", var a, "--output", var o] => (a, o),
    ["--output", var o, "--assembly", var a] => (a, o),
    _ => ((string?)null, (string?)null),
};
if (output is null)
{
    await Console.Error.WriteLineAsync(Usage).ConfigureAwait(false);
    return 2;
}

if (assembly is not null && !File.Exists(assembly))
{
    await Console.Error.WriteLineAsync($"Assembly not found: {assembly}").ConfigureAwait(false);
    return 2;
}

var hubs = assembly is null
    ? HubDiscovery.FindHubs(AppContext.BaseDirectory)
    : HubDiscovery.FindHubsInAssembly(assembly);
var manifest = HubManifest.Build(hubs);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
await File.WriteAllTextAsync(output, manifest).ConfigureAwait(false);
return 0;
