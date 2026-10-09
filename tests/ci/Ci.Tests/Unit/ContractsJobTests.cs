using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Unit;

/// <summary>CT-01 (static part): the contracts job of ci.yml runs the SPEC-0006@1 check command.</summary>
public sealed class ContractsJobTests
{
    [Fact(DisplayName = "Dado o contrato SPEC-0006@1, quando o ci.yml é lido, então o job contracts executa `pnpm -C src/frontend contracts:check`")]
    [Trait("Spec", "SPEC-0004:CT-01")]
    public void GivenContractWhenCiWorkflowIsReadThenContractsJobRunsContractsCheck()
    {
        var ci = WorkflowFile.TryLoad(CiPipeline.CiWorkflow);
        Assert.True(ci is not null, $"workflow {CiPipeline.CiWorkflow} not found in {WorkflowFile.Directory}");

        var jobs = ci.Jobs();
        Assert.True(
            jobs.TryGetValue(CiPipeline.Contracts, out var contractsJob),
            $"{CiPipeline.CiWorkflow} has no `{CiPipeline.Contracts}` job (jobs: {string.Join(", ", jobs.Keys)}).");
        Assert.Contains(CiPipeline.ContractsCheckCommand, contractsJob, StringComparison.Ordinal);
    }
}
