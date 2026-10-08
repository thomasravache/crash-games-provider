using System.Text.RegularExpressions;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>
/// Runs the REST TypeScript generator of the contracts (openapi-typescript behind <c>tools/contracts/openapi-to-ts.mjs</c>,
/// SPEC-0006 §2) over an OpenAPI document and inspects the declared type of a property in the output.
/// </summary>
internal static partial class TypeScriptGenerator
{
    public const string ScriptPath = "tools/contracts/openapi-to-ts.mjs";

    /// <summary><c>node tools/contracts/openapi-to-ts.mjs --input &lt;openapi.json&gt; --output &lt;file.ts&gt;</c>, run from the repository root.</summary>
    public static async Task<ProcessResult> RunAsync(string inputPath, string outputPath, CancellationToken cancellationToken) =>
        await ProcessRunner.RunAsync(
            "node",
            [ScriptPath, "--input", inputPath, "--output", outputPath],
            RepositoryRoot.Path,
            ProcessRunner.ShortTimeout,
            cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Declared type of <paramref name="property"/>; when it points at <c>components["schemas"]["X"]</c>, the declared
    /// type of the schema <c>X</c> (openapi-typescript emits referenced schemas that way).
    /// </summary>
    public static string PropertyType(string typeScript, string property)
    {
        var type = DeclaredType(typeScript, property);
        for (var depth = 0; depth < 8; depth++)
        {
            var reference = SchemaReference().Match(type);
            if (!reference.Success)
            {
                return type;
            }

            type = DeclaredType(typeScript, reference.Groups["name"].Value);
        }

        return type;
    }

    private static string DeclaredType(string typeScript, string name)
    {
        var match = Regex.Match(
            typeScript,
            $@"(?m)^\s*(?:readonly\s+)?(?:""{Regex.Escape(name)}""|{Regex.Escape(name)})\??:\s*(?<type>[^;\r\n]+);",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));
        if (!match.Success)
        {
            Assert.Fail($"The generated TypeScript declares no '{name}'. Output:{Environment.NewLine}{typeScript}");
        }

        return match.Groups["type"].Value.Trim();
    }

    [GeneratedRegex(@"^components\[""schemas""\]\[""(?<name>[^""]+)""\]$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SchemaReference();
}
