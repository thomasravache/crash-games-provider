using Xunit;

namespace Apogeu.Repository.Tests.Support;

[CollectionDefinition(Name)]
public sealed class RepositoryFixtureDefinition : ICollectionFixture<RepositoryCopyFixture>
{
    public const string Name = "Repository copy";
}
