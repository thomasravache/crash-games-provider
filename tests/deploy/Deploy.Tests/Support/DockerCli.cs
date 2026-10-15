using System.Net;
using System.Net.Sockets;
using Apogeu.Repository.Tests.Support;

namespace Apogeu.Deploy.Tests.Support;

/// <summary>Docker CLI, curl and free ports for the deployment tests (SPEC-0066).</summary>
internal static class DockerCli
{
    /// <summary>The proxy image of the contract (SPEC-0066 §2, ADR-0029).</summary>
    public const string CaddyImage = "caddy:2.11.7";

    /// <summary>The PostgreSQL of the integration tests (as the TestKit, ADR-0010).</summary>
    public const string PostgreSqlImage = "postgres:18.3";

    public static Task<ProcessResult> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken) =>
        ProcessRunner.RunAsync("docker", arguments, RepositoryRoot.Path, ProcessRunner.LongTimeout, cancellationToken: cancellationToken);

    /// <summary>A TCP port free on the loopback right now (never 80/443 nor the AppHost ports, which are taken or privileged).</summary>
    public static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>
    /// GET through the proxy of a test deployment: <c>{scheme}://{host}:{port}{path}</c> resolved to 127.0.0.1, without
    /// following redirects and accepting the internal CA of Caddy (test certificates). Returns status, Location and body.
    /// </summary>
    public static async Task<HttpAnswer> CurlAsync(
        string scheme, string host, int port, string path, CancellationToken cancellationToken)
    {
        var url = $"{scheme}://{host}:{port}{path}";
        var bodyFile = Path.GetTempFileName();
        try
        {
            var result = await ProcessRunner.RunAsync(
                "curl",
                [
                    "--silent", "--insecure", "--max-time", "10",
                    "--resolve", $"{host}:{port}:127.0.0.1",
                    "--output", bodyFile,
                    "--write-out", "%{http_code} %{redirect_url}",
                    url,
                ],
                RepositoryRoot.Path,
                ProcessRunner.ShortTimeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var parts = result.StandardOutput.Trim().Split(' ', 2);
            var status = int.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var code) ? code : 0;
            var location = parts.Length > 1 ? parts[1] : string.Empty;
            var body = File.Exists(bodyFile) ? await File.ReadAllTextAsync(bodyFile, cancellationToken).ConfigureAwait(false) : string.Empty;
            return new HttpAnswer(url, status, location, body);
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    /// <summary>
    /// Removes everything a test compose project left: containers, networks and volumes by the compose project label, and
    /// the images whose repository starts with <paramref name="imagePrefix"/>. Best effort: never throws.
    /// </summary>
    public static async Task RemoveProjectAsync(string project, string imagePrefix)
    {
        var none = CancellationToken.None;
        var label = "label=com.docker.compose.project=" + project;
        await RunAsync(["compose", "--project-name", project, "down", "--volumes", "--remove-orphans", "--timeout", "2"], none).ConfigureAwait(false);
        foreach (var (kind, list) in new[]
                 {
                     ("container", new[] { "ps", "--all", "--quiet", "--filter", label }),
                     ("network", new[] { "network", "ls", "--quiet", "--filter", label }),
                     ("volume", new[] { "volume", "ls", "--quiet", "--filter", label }),
                 })
        {
            var ids = (await RunAsync(list, none).ConfigureAwait(false)).StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ids.Length > 0)
            {
                string[] remove = string.Equals(kind, "container", StringComparison.Ordinal) ? ["rm", "--force", .. ids] : [kind, "rm", "--force", .. ids];
                await RunAsync(remove, none).ConfigureAwait(false);
            }
        }

        var images = (await RunAsync(["image", "ls", "--format", "{{.Repository}}:{{.Tag}}"], none).ConfigureAwait(false)).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(image => image.StartsWith(imagePrefix, StringComparison.Ordinal))
            .ToArray();
        if (images.Length > 0)
        {
            await RunAsync(["image", "rm", "--force", .. images], none).ConfigureAwait(false);
        }
    }

    /// <summary>Running containers of a compose project, by service name (label com.docker.compose.service).</summary>
    public static async Task<IReadOnlyDictionary<string, string>> ServiceContainersAsync(string project, CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            [
                "ps", "--filter", "label=com.docker.compose.project=" + project,
                "--format", "{{.Label \"com.docker.compose.service\"}} {{.ID}}",
            ],
            cancellationToken).ConfigureAwait(false);
        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First()[1], StringComparer.Ordinal);
    }
}
