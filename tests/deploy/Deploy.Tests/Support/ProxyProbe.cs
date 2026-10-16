using Apogeu.Repository.Tests.Support;

namespace Apogeu.Deploy.Tests.Support;

/// <summary>
/// SPEC-0066 UT-01 (Emendas v3 and v4): the real Caddyfile in caddy:2.11.7 in front of an echo backend (busybox httpd running a
/// CGI that prints the X-Forwarded-* it received), reachable as <c>api</c> and <c>demo-casino</c> on a network of its own.
/// The client sends forged X-Forwarded-For/Host/Proto; the echo tells what the backend got. The access log of the proxy
/// is the output of its container (docker logs). Disposing removes the
/// containers, the network and the temporary files.
/// </summary>
internal sealed class ProxyProbe : IAsyncDisposable
{
    public const string ForgedFor = "1.2.3.4";

    private const string EchoScript =
        """
        printf 'Content-Type: text/plain\r\n\r\n'
        echo "XFF=$HTTP_X_FORWARDED_FOR"
        echo "XFH=$HTTP_X_FORWARDED_HOST"
        echo "XFP=$HTTP_X_FORWARDED_PROTO"
        """;

    private readonly string _name;
    private readonly string _directory;

    private ProxyProbe(string name, string directory)
    {
        _name = name;
        _directory = directory;
    }

    private int Port { get; set; }

    private int HttpPort { get; set; }

    /// <summary>
    /// Starts the proxy and, unless <paramref name="withBackend"/> is false, the echo backend. Without it, <c>api</c> and
    /// <c>demo-casino</c> do not resolve and the proxied routes answer 502 (backend down, Emenda v5).
    /// </summary>
    public static async Task<ProxyProbe> StartAsync(string caddyfile, string domain, CancellationToken cancellationToken, bool withBackend = true)
    {
        var name = "apogeu-test-ut01-" + Guid.NewGuid().ToString("N")[..12];
        var directory = Path.Combine(Path.GetTempPath(), name);
        var probe = new ProxyProbe(name, directory);
        Directory.CreateDirectory(Path.Combine(directory, "www", "casino"));
        await File.WriteAllTextAsync(Path.Combine(directory, "httpd.conf"), "*.cgi:/bin/sh\n", cancellationToken).ConfigureAwait(false);
        foreach (var script in new[] { Path.Combine(directory, "www", "echo.cgi"), Path.Combine(directory, "www", "casino", "echo.cgi") })
        {
            await File.WriteAllTextAsync(script, EchoScript.ReplaceLineEndings("\n") + "\n", cancellationToken).ConfigureAwait(false);
            File.SetUnixFileMode(script, (UnixFileMode)0b111_101_101);
        }

        await probe.DockerAsync(["network", "create", name], cancellationToken).ConfigureAwait(false);
        if (withBackend)
        {
            await probe.DockerAsync(
                [
                    "run", "--detach", "--name", name + "-echo", "--network", name, "--network-alias", "api", "--network-alias", "demo-casino",
                    "--volume", $"{directory}:/srv:ro", "busybox:1.37.0-musl", "httpd", "-f", "-p", "8080", "-h", "/srv/www", "-c", "/srv/httpd.conf",
                ],
                cancellationToken).ConfigureAwait(false);
        }

        await probe.DockerAsync(
            [
                "run", "--detach", "--name", name + "-proxy", "--network", name, "--env", $"{DeployScripts.DomainVariable}={domain}",
                "--volume", $"{caddyfile}:/etc/caddy/Caddyfile:ro", "--publish", "127.0.0.1::443", "--publish", "127.0.0.1::80", DockerCli.CaddyImage,
            ],
            cancellationToken).ConfigureAwait(false);
        var port = await probe.DockerAsync(["port", name + "-proxy", "443/tcp"], cancellationToken).ConfigureAwait(false);
        probe.Port = int.Parse(port.StandardOutput.Split('\n')[0].Trim().Split(':')[^1], System.Globalization.CultureInfo.InvariantCulture);
        var http = await probe.DockerAsync(["port", name + "-proxy", "80/tcp"], cancellationToken).ConfigureAwait(false);
        probe.HttpPort = int.Parse(http.StandardOutput.Split('\n')[0].Trim().Split(':')[^1], System.Globalization.CultureInfo.InvariantCulture);
        return probe;
    }

    /// <summary>GET with forged X-Forwarded-* through the proxy; the X-Forwarded-* the backend received (XFF, XFH, XFP).</summary>
    public async Task<IReadOnlyDictionary<string, string>> EchoAsync(string host, string path, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var result = await ProcessRunner.RunAsync(
                "curl",
                [
                    "--silent", "--fail", "--insecure", "--max-time", "5",
                    "--resolve", $"{host}:{Port}:127.0.0.1",
                    "--header", "X-Forwarded-For: " + ForgedFor,
                    "--header", "X-Forwarded-Host: evil.example",
                    "--header", "X-Forwarded-Proto: http",
                    $"https://{host}:{Port}{path}",
                ],
                RepositoryRoot.Path,
                ProcessRunner.ShortTimeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                return result.StandardOutput
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(line => line.Split('=', 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new Xunit.Sdk.XunitException($"the proxy did not answer {host}{path}. " + result.Describe());
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>GET over HTTPS through the proxy with the given headers; the status (any). Retries while the proxy is not answering yet.</summary>
    public Task<int> GetAsync(string host, string path, IEnumerable<string> headers, CancellationToken cancellationToken) =>
        GetAsync("https", host, path, headers, cancellationToken);

    /// <summary>GET over <paramref name="scheme"/> (http or https) through the proxy, without following redirects; the status.</summary>
    public async Task<int> GetAsync(string scheme, string host, string path, IEnumerable<string> headers, CancellationToken cancellationToken)
    {
        var port = string.Equals(scheme, "http", StringComparison.Ordinal) ? HttpPort : Port;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var result = await ProcessRunner.RunAsync(
                "curl",
                [
                    "--silent", "--insecure", "--max-time", "5", "--output", "/dev/null", "--write-out", "%{http_code}",
                    "--resolve", $"{host}:{port}:127.0.0.1",
                    .. headers.SelectMany(header => new[] { "--header", header }),
                    $"{scheme}://{host}:{port}{path}",
                ],
                RepositoryRoot.Path,
                ProcessRunner.ShortTimeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                return int.Parse(result.StandardOutput.Trim(), System.Globalization.CultureInfo.InvariantCulture);
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new Xunit.Sdk.XunitException($"the proxy did not answer {scheme}://{host}{path}. " + result.Describe());
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Everything the proxy container wrote (standard output and error): its access log.</summary>
    public async Task<string> LogsAsync(CancellationToken cancellationToken)
    {
        var result = await DockerCli.RunAsync(["logs", _name + "-proxy"], cancellationToken).ConfigureAwait(false);
        return result.Output;
    }

    public async ValueTask DisposeAsync()
    {
        await DockerCli.RunAsync(["rm", "--force", _name + "-proxy", _name + "-echo"], CancellationToken.None).ConfigureAwait(false);
        await DockerCli.RunAsync(["network", "rm", _name], CancellationToken.None).ConfigureAwait(false);
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task<ProcessResult> DockerAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var result = await DockerCli.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            await DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("docker: " + result.Describe());
        }

        return result;
    }
}
