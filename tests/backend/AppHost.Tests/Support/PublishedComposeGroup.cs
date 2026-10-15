using Xunit;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>The tests that read the published compose share one publish of the AppHost.</summary>
[CollectionDefinition(Name)]
public sealed class PublishedComposeGroup : ICollectionFixture<PublishedComposeFixture>
{
    public const string Name = "AppHost published compose";
}
