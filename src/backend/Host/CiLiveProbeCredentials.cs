namespace Apogeu.Host;

// ci-live probe IT-03: fake credential committed on purpose; the security job must reject it.
internal static class CiLiveProbeCredentials
{
    internal const string AwsAccessKeyId = "AKIASCPIRZF5MYN565P2";
}
