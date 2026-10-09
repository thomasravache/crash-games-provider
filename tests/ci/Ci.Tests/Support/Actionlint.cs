using Apogeu.Repository.Tests.Support;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Runs actionlint at the version pinned in <see cref="CiPipeline.ActionlintVersion"/>: the local binary when it is
/// exactly that version, otherwise the official Docker image of the same tag (the repository is mounted read-only).
/// </summary>
public static class Actionlint
{
    public static string DockerImage => "rhysd/actionlint:" + CiPipeline.ActionlintVersion;

    public static async Task<ProcessResult> RunAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        var version = await ProcessRunner
            .RunAsync("actionlint", ["-version"], repositoryRoot, ProcessRunner.ShortTimeout, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var localMatches = version.Succeeded
            && version.StandardOutput.Split('\n')[0].Trim().Equals(CiPipeline.ActionlintVersion, StringComparison.Ordinal);

        if (localMatches)
        {
            return await ProcessRunner
                .RunAsync("actionlint", ["-no-color"], repositoryRoot, ProcessRunner.ShortTimeout, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return await ProcessRunner
            .RunAsync(
                "docker",
                ["run", "--rm", "-v", repositoryRoot + ":/repo:ro", "--workdir", "/repo", DockerImage, "-no-color"],
                repositoryRoot,
                ProcessRunner.LongTimeout,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
