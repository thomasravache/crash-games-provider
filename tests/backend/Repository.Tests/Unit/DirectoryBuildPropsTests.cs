using System.Xml.Linq;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Repository.Tests.Unit;

public sealed class DirectoryBuildPropsTests
{
    private const string FileName = "Directory.Build.props";

    [Theory(DisplayName = "Dado o Directory.Build.props, quando lido, então a propriedade estrita está presente")]
    [Trait("Spec", "SPEC-0002:UT-01")]
    [InlineData("Nullable", "enable")]
    [InlineData("TreatWarningsAsErrors", "true")]
    [InlineData("AnalysisMode", "Recommended")]
    public void GivenDirectoryBuildPropsWhenReadThenStrictPropertyIsSet(string property, string expected)
    {
        var document = LoadDirectoryBuildProps();

        var values = document
            .Descendants(property)
            .Where(element => string.Equals(element.Parent?.Name.LocalName, "PropertyGroup", StringComparison.Ordinal))
            .Select(element => element.Value.Trim())
            .ToList();

        Assert.True(
            values.Count > 0,
            $"{FileName} does not define <{property}> inside a <PropertyGroup>.");
        Assert.Contains(expected, values, StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Dado o Directory.Build.props, quando lido, então referencia o Meziantou.Analyzer")]
    [Trait("Spec", "SPEC-0002:UT-01")]
    public void GivenDirectoryBuildPropsWhenReadThenMeziantouAnalyzerIsReferenced()
    {
        var document = LoadDirectoryBuildProps();

        var references = document
            .Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update"))
            .ToList();

        Assert.Contains("Meziantou.Analyzer", references, StringComparer.OrdinalIgnoreCase);
    }

    private static XDocument LoadDirectoryBuildProps()
    {
        var path = RepositoryRoot.Combine(FileName);
        Assert.True(File.Exists(path), $"{FileName} was not found at the repository root ({path}).");
        return XDocument.Load(path);
    }
}
