using Xunit;

namespace Apogeu.ServiceDefaults.Tests.Unit;

public sealed class CiLiveProbeTests
{
    [Fact]
    public void CiLiveProbeFailsOnPurpose() => Assert.Fail("ci-live probe IT-02: this test fails on purpose.");
}
