using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Repository.Tests.Integration;

[Collection(RepositoryFixtureDefinition.Name)]
public sealed class CommitMessageHookTests(RepositoryCopyFixture repository)
{
    private static readonly string[] Identity =
    [
        "-c", "user.name=Repository Tests",
        "-c", "user.email=repository-tests@example.invalid",
        "-c", "commit.gpgsign=false",
    ];

    [Fact(DisplayName = "Dado o hook commit-msg ativo, quando se tenta commitar \"ajustes\", então o commit é rejeitado")]
    [Trait("Spec", "SPEC-0002:IT-05")]
    public async Task GivenCommitMsgHookWhenCommittingAjustesThenCommitIsRejected()
    {
        var workTree = await CreateRepositoryWithHookAsync();

        var commit = await CommitAsync(workTree, "ajustes");

        Assert.False(commit.Succeeded, "Expected the commit-msg hook to reject \"ajustes\". " + commit.Describe());
        Assert.Equal(0, await CountCommitsAsync(workTree));
    }

    [Fact(DisplayName = "Dado o hook commit-msg ativo, quando a mensagem não tem Refs: SPEC-NNNN, então o commit é rejeitado")]
    [Trait("Spec", "SPEC-0002:IT-05")]
    public async Task GivenCommitMsgHookWhenMessageHasNoSpecReferenceThenCommitIsRejected()
    {
        var workTree = await CreateRepositoryWithHookAsync();

        var commit = await CommitAsync(workTree, "chore(repo): estrutura");

        Assert.False(commit.Succeeded, "Expected the commit-msg hook to reject a message without `Refs: SPEC-NNNN`. " + commit.Describe());
        Assert.Equal(0, await CountCommitsAsync(workTree));
    }

    [Fact(DisplayName = "Dado o hook commit-msg ativo, quando a mensagem segue o padrão com Refs, então o commit é aceito")]
    [Trait("Spec", "SPEC-0002:IT-05")]
    public async Task GivenCommitMsgHookWhenMessageIsConventionalWithRefsThenCommitIsAccepted()
    {
        var workTree = await CreateRepositoryWithHookAsync();

        var commit = await CommitAsync(workTree, "chore(repo): estrutura\n\nRefs: SPEC-0002");

        Assert.True(commit.Succeeded, "Expected the commit-msg hook to accept a Conventional Commit with `Refs: SPEC-0002`. " + commit.Describe());
        Assert.Equal(1, await CountCommitsAsync(workTree));
    }

    private async Task<string> CreateRepositoryWithHookAsync()
    {
        var workTree = repository.CreateIsolatedCopy();
        Assert.True(
            File.Exists(Path.Combine(workTree, ".githooks", "commit-msg")),
            "Expected the commit-msg hook at .githooks/commit-msg (SPEC-0002 §2).");
        var cancellationToken = TestContext.Current.CancellationToken;

        var init = await ProcessRunner.GitAsync(workTree, ["init", "--quiet", "--initial-branch=main"], cancellationToken).ConfigureAwait(false);
        Assert.True(init.Succeeded, "Setup failed. " + init.Describe());

        var config = await ProcessRunner.GitAsync(workTree, ["config", "core.hooksPath", ".githooks"], cancellationToken).ConfigureAwait(false);
        Assert.True(config.Succeeded, "Setup failed. " + config.Describe());

        return workTree;
    }

    private static Task<ProcessResult> CommitAsync(string workTree, string message) =>
        ProcessRunner.GitAsync(
            workTree,
            [.. Identity, "commit", "--allow-empty", "--message", message],
            TestContext.Current.CancellationToken);

    private static async Task<int> CountCommitsAsync(string workTree)
    {
        var count = await ProcessRunner.GitAsync(workTree, ["rev-list", "--all", "--count"], TestContext.Current.CancellationToken).ConfigureAwait(false);
        Assert.True(count.Succeeded, "Setup failed. " + count.Describe());
        return int.Parse(count.StandardOutput.Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
