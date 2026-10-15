namespace Apogeu.AppHost.Tests.Support;

/// <summary>A dotenv file as Docker Compose reads it: <c>NAME=VALUE</c> lines; blank lines and <c># comments</c> ignored.</summary>
internal static class EnvFile
{
    public static IReadOnlyDictionary<string, string> Read(string path)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path).Select(line => line.Trim()))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            var name = (separator < 0 ? line : line[..separator]).Trim();
            if (name.StartsWith("export ", StringComparison.Ordinal))
            {
                name = name["export ".Length..].Trim();
            }

            variables[name] = separator < 0 ? string.Empty : line[(separator + 1)..].Trim().Trim('"', '\'');
        }

        return variables;
    }
}
