using Apogeu.HubContracts;

// Usage: dotnet Apogeu.HubContracts.dll --output <manifest.json>
if (args is not ["--output", var output])
{
    await Console.Error.WriteLineAsync("Usage: dotnet Apogeu.HubContracts.dll --output <manifest.json>").ConfigureAwait(false);
    return 2;
}

var manifest = HubManifest.Build(HubDiscovery.FindHubs(AppContext.BaseDirectory));
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
await File.WriteAllTextAsync(output, manifest).ConfigureAwait(false);
return 0;
