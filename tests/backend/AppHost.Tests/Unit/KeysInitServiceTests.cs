using Apogeu.AppHost.Tests.Support;
using Xunit;

namespace Apogeu.AppHost.Tests.Unit;

/// <summary>
/// SPEC-0065 UT-03 (Emenda v3): a volume Docker creates belongs to root, and the api runs as the non-root user of the .NET
/// images (UID 1654), so a short-lived <c>keys-init</c> service hands the key volume over to that user before the api starts.
/// </summary>
[Collection(PublishedComposeGroup.Name)]
public sealed class KeysInitServiceTests(PublishedComposeFixture published)
{
    public const string KeysInit = "keys-init";
    public const string KeysInitImage = "busybox:1.37.0";
    public const string ApiUser = "1654:1654";
    private const string KeysVolume = "dataprotection-keys";

    [Fact(DisplayName = "Dado o compose publicado, quando lido, então o serviço keys-init monta dataprotection-keys, troca o dono para 1654:1654, não publica portas, e a api depende dele com service_completed_successfully")]
    [Trait("Spec", "SPEC-0065:UT-03")]
    public void GivenPublishedComposeWhenReadThenKeysInitHandsTheKeyVolumeToTheApiUserBeforeTheApiStarts()
    {
        published.AssertPublished();
        var compose = ComposeDocument.Load(published.ComposePath);

        Assert.True(
            compose.HasService(KeysInit),
            $"The compose has no '{KeysInit}' service to hand the volume {KeysVolume} to the api user. Services: {string.Join(", ", compose.ServiceNames)}.");

        var image = compose.Image(KeysInit);
        Assert.True(
            string.Equals(image, KeysInitImage, StringComparison.Ordinal) || image.EndsWith("/" + KeysInitImage, StringComparison.Ordinal),
            $"{KeysInit} must run {KeysInitImage}, found '{image}'.");

        var mount = Assert.Single(compose.Mounts(KeysInit), candidate => string.Equals(candidate.Source, KeysVolume, StringComparison.Ordinal));
        var command = string.Join(' ', compose.EntrypointAndCommand(KeysInit));
        Assert.True(
            command.Contains("chown", StringComparison.Ordinal)
                && command.Contains(ApiUser, StringComparison.Ordinal)
                && command.Contains(mount.Target, StringComparison.Ordinal),
            $"{KeysInit} must change the owner of {mount.Target} to {ApiUser}; its command is '{command}'.");

        Assert.Empty(compose.Ports(KeysInit));

        Assert.True(
            compose.DependsOn("api").TryGetValue(KeysInit, out var condition)
                && string.Equals(condition, "service_completed_successfully", StringComparison.Ordinal),
            $"The api must depend on {KeysInit} with condition service_completed_successfully, found '{condition}'.");
    }
}
