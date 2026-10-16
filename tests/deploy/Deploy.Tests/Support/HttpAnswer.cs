namespace Apogeu.Deploy.Tests.Support;

/// <summary>Answer of <see cref="DockerCli.CurlAsync"/> (status 0 when nothing answered).</summary>
internal sealed record HttpAnswer(string Url, int Status, string Location, string Body)
{
    public override string ToString() => $"GET {Url} -> {Status} {Location} «{Body.Trim()}»";
}
