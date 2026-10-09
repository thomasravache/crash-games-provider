namespace Apogeu.Ci.Tests.Support;

/// <summary>Names fixed by the SPEC-0004 contract (section 6): workflows, the jobs of ci.yml and the gate commands.</summary>
public static class CiPipeline
{
    public const string CiWorkflow = "ci.yml";
    public const string ImagesWorkflow = "images.yml";
    public const string NightlyWorkflow = "nightly.yml";

    /// <summary>Workflow name shown in the checks (`ci/backend`, …).</summary>
    public const string CiWorkflowName = "ci";

    public const string Backend = "backend";
    public const string Frontend = "frontend";
    public const string E2e = "e2e";
    public const string Security = "security";
    public const string Contracts = "contracts";
    public const string Sdd = "sdd";

    public const string ContractsCheckCommand = "pnpm -C src/frontend contracts:check";

    /// <summary>Script owned by the implementation that enforces SHA pinning and permissions.</summary>
    public const string CheckWorkflowsScript = "tools/ci/check-workflows";

    /// <summary>Pinned actionlint (local binary of this exact version, otherwise the Docker image of the same tag).</summary>
    public const string ActionlintVersion = "1.7.12";

    public static readonly string[] ContractWorkflows = [CiWorkflow, ImagesWorkflow, NightlyWorkflow];

    /// <summary>Jobs whose checks every PR to main reports (informative; required on the server only with SPEC-0015).</summary>
    public static readonly string[] PullRequestJobs = [Backend, Frontend, E2e, Security, Contracts, Sdd];
}
