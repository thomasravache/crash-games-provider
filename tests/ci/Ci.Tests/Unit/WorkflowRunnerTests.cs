using System.Text.RegularExpressions;
using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Unit;

/// <summary>
/// SPEC-0059: every job picks its runner from the repository variable <c>CI_RUNNER</c> (hosted runner when empty) and
/// the steps that need <c>sudo</c> run only on the hosted runner, since the VM runner user has no sudo.
/// </summary>
public sealed partial class WorkflowRunnerTests
{
    public const string ConfigurableRunner = "${{ vars.CI_RUNNER || 'ubuntu-latest' }}";

    private static readonly string[] RunnerWorkflows = ["ci.yml", "nightly.yml", "images.yml", "visual-baselines.yml"];

    private const string CompliantWorkflow = """
        name: fixture
        on:
          pull_request:
            branches: [main]
        permissions:
          contents: read
        jobs:
          build:
            runs-on: ${{ vars.CI_RUNNER || 'ubuntu-latest' }}
            steps:
              - uses: actions/checkout@0123456789abcdef0123456789abcdef01234567
              - name: Install browsers
                if: steps.browsers.outputs.cache-hit != 'true' && runner.environment == 'github-hosted'
                run: pnpm exec playwright install --with-deps chromium
              - name: System packages
                if: runner.environment == 'github-hosted'
                run: |
                  sudo apt-get update
                  sudo apt-get install -y jq
              - name: Test
                run: echo ok
          lint:
            needs: build
            runs-on: ${{ vars.CI_RUNNER || 'ubuntu-latest' }}
            steps:
              - run: echo lint
        """;

    [Fact(DisplayName = "Dados os quatro workflows, quando cada job é lido, então o runs-on de todos é a expressão de CI_RUNNER com ubuntu-latest por padrão")]
    [Trait("Spec", "SPEC-0059:UT-01")]
    public void GivenWorkflowsWhenJobsAreReadThenEveryRunsOnIsTheConfigurableRunner()
    {
        var violations = RunnerWorkflows
            .Select(name =>
            {
                var workflow = WorkflowFile.TryLoad(name);
                Assert.True(workflow is not null, $"workflow {name} not found in {WorkflowFile.Directory}");
                Assert.NotEmpty(workflow.Jobs());
                return workflow;
            })
            .SelectMany(RunsOnViolations)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Jobs whose runs-on is not `{ConfigurableRunner}`:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Theory(DisplayName = "Dado um job fora da regra, quando o runs-on é verificado, então reprova citando arquivo e job")]
    [Trait("Spec", "SPEC-0059:UT-01")]
    [InlineData("ubuntu-latest fixo", "runs-on: ubuntu-latest")]
    [InlineData("self-hosted fixo", "runs-on: self-hosted")]
    [InlineData("expressão sem padrão", "runs-on: ${{ vars.CI_RUNNER }}")]
    [InlineData("sem runs-on", "")]
    public void GivenNonCompliantJobWhenRunsOnIsCheckedThenItFailsNamingFileAndJob(string scenario, string replacement)
    {
        var original = "  lint:\n    needs: build\n    runs-on: " + ConfigurableRunner;
        var broken = CompliantWorkflow.Replace(original, "  lint:\n    needs: build\n    " + replacement, StringComparison.Ordinal);
        Assert.NotEqual(CompliantWorkflow, broken, StringComparer.Ordinal);

        var violations = RunsOnViolations(LoadFixture(broken));

        var violation = Assert.Single(violations);
        Assert.Contains("fixture.yml", violation, StringComparison.Ordinal);
        Assert.Contains("job lint", violation, StringComparison.Ordinal);
        Assert.True(!violation.Contains("job build", StringComparison.Ordinal), $"{scenario}: {violation}");
    }

    [Fact(DisplayName = "Dado um workflow conforme, quando o runs-on é verificado, então não há violação")]
    [Trait("Spec", "SPEC-0059:UT-01")]
    public void GivenCompliantWorkflowWhenRunsOnIsCheckedThenThereAreNoViolations()
    {
        Assert.Empty(RunsOnViolations(LoadFixture(CompliantWorkflow)));
    }

    [Fact(DisplayName = "Dados os workflows, quando os passos são lidos, então todo passo com sudo, apt-get ou --with-deps só roda no runner hospedado")]
    [Trait("Spec", "SPEC-0059:UT-02")]
    public void GivenWorkflowsWhenStepsAreReadThenPrivilegedStepsRunOnlyOnHostedRunner()
    {
        var workflows = WorkflowFile.LoadAll(WorkflowFile.WorkflowsDirectory);
        foreach (var name in RunnerWorkflows)
        {
            Assert.Contains(workflows, workflow => string.Equals(workflow.Name, name, StringComparison.Ordinal));
        }

        var violations = workflows.SelectMany(PrivilegedStepViolations).ToList();

        Assert.True(
            violations.Count == 0,
            $"Steps using sudo, apt-get or --with-deps without `if: runner.environment == 'github-hosted'`:{Environment.NewLine}"
            + string.Join(Environment.NewLine, violations));
    }

    [Theory(DisplayName = "Dado um passo com sudo, apt-get ou --with-deps sem a condição, quando verificado, então reprova citando arquivo, job e passo")]
    [Trait("Spec", "SPEC-0059:UT-02")]
    [InlineData("--with-deps sem if", "        if: steps.browsers.outputs.cache-hit != 'true' && runner.environment == 'github-hosted'\n", "", "Install browsers")]
    [InlineData("--with-deps com outra condição", "&& runner.environment == 'github-hosted'", "", "Install browsers")]
    [InlineData("sudo apt-get sem if", "        if: runner.environment == 'github-hosted'\n        run: |", "        run: |", "System packages")]
    [InlineData("condição invertida", "if: runner.environment == 'github-hosted'\n        run: |", "if: runner.environment != 'github-hosted'\n        run: |", "System packages")]
    public void GivenPrivilegedStepWithoutConditionWhenCheckedThenItFailsNamingFileJobAndStep(
        string scenario, string original, string replacement, string step)
    {
        var broken = CompliantWorkflow.Replace(original, replacement, StringComparison.Ordinal);
        Assert.NotEqual(CompliantWorkflow, broken, StringComparer.Ordinal);

        var violations = PrivilegedStepViolations(LoadFixture(broken));

        var violation = Assert.Single(violations);
        Assert.True(violation.Contains("fixture.yml", StringComparison.Ordinal), $"{scenario}: {violation}");
        Assert.True(violation.Contains("job build", StringComparison.Ordinal), $"{scenario}: {violation}");
        Assert.True(violation.Contains($"step '{step}'", StringComparison.Ordinal), $"{scenario}: {violation}");
    }

    [Fact(DisplayName = "Dado um workflow com passos privilegiados condicionados, quando verificado, então não há violação")]
    [Trait("Spec", "SPEC-0059:UT-02")]
    public void GivenGuardedPrivilegedStepsWhenCheckedThenThereAreNoViolations()
    {
        Assert.Empty(PrivilegedStepViolations(LoadFixture(CompliantWorkflow)));
    }

    private static WorkflowFile LoadFixture(string yaml)
    {
        using var fixture = WorkflowFixtureDirectory.Create(yaml);
        return Assert.Single(WorkflowFile.LoadAll(fixture.Path));
    }

    private static IEnumerable<string> RunsOnViolations(WorkflowFile workflow)
    {
        foreach (var (job, block) in workflow.Jobs())
        {
            var keys = new Mapping(block.Split('\n')[1..]);
            var runsOn = keys.Value("runs-on");
            if (runsOn is null)
            {
                yield return $"{workflow.Name} › job {job}: no runs-on";
            }
            else if (!string.Equals(Unquote(runsOn), ConfigurableRunner, StringComparison.Ordinal))
            {
                yield return $"{workflow.Name} › job {job}: runs-on is `{runsOn}`";
            }
        }
    }

    private static IEnumerable<string> PrivilegedStepViolations(WorkflowFile workflow)
    {
        foreach (var (job, block) in workflow.Jobs())
        {
            var keys = new Mapping(block.Split('\n')[1..]);
            foreach (var step in keys.SequenceItems("steps"))
            {
                var run = step.Value("run");
                if (run is null || !PrivilegedCommand().IsMatch(run))
                {
                    continue;
                }

                var condition = step.Value("if");
                if (condition is null || !HostedRunnerCondition().IsMatch(condition))
                {
                    var name = step.Value("name") ?? run.Trim().Split('\n')[0];
                    yield return $"{workflow.Name} › job {job} › step '{Unquote(name)}': if is `{condition ?? "(none)"}`";
                }
            }
        }
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length >= 2 && (trimmed[0] == '"' || trimmed[0] == '\'') && trimmed[^1] == trimmed[0]
            ? trimmed[1..^1]
            : trimmed;
    }

    private static int Indentation(string line) => line.Length - line.TrimStart(' ').Length;

    private static bool IsBlankOrComment(string line) =>
        string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#');

    [GeneratedRegex(@"\bsudo\b|\bapt-get\b|--with-deps\b", RegexOptions.None, 1000)]
    private static partial Regex PrivilegedCommand();

    [GeneratedRegex(@"runner\.environment\s*==\s*'github-hosted'", RegexOptions.None, 1000)]
    private static partial Regex HostedRunnerCondition();

    [GeneratedRegex("""^["']?(?<key>[A-Za-z0-9_-]+)["']?\s*:(\s|$)""", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex KeyLine();

    /// <summary>
    /// Line-based view of one YAML block mapping (same approach as <see cref="WorkflowFile"/>, no YAML dependency):
    /// the keys sit at the indentation of the first meaningful line, and a key's value runs until the next line at
    /// that indentation or shallower.
    /// </summary>
    private sealed class Mapping
    {
        private readonly string[] _lines;
        private readonly int _indent;

        public Mapping(IEnumerable<string> lines)
        {
            _lines = [.. lines];
            _indent = _lines.Where(line => !IsBlankOrComment(line)).Select(Indentation).DefaultIfEmpty(0).First();
        }

        /// <summary>Text of the key's value (inline part plus continuation lines), or null when the key is absent.</summary>
        public string? Value(string key)
        {
            var (start, end) = Range(key);
            if (start < 0)
            {
                return null;
            }

            var inline = KeyLine().Replace(_lines[start].TrimStart(), string.Empty, 1).Trim();
            var continuation = _lines[(start + 1)..end].Where(line => !IsBlankOrComment(line)).Select(line => line.Trim());
            return string.Join('\n', new[] { inline }.Concat(continuation).Where(part => part.Length > 0));
        }

        /// <summary>Items of a block sequence under the key, each one as its own mapping.</summary>
        public List<Mapping> SequenceItems(string key)
        {
            var (start, end) = Range(key);
            var items = new List<Mapping>();
            if (start < 0)
            {
                return items;
            }

            var body = _lines[(start + 1)..end];
            var itemIndent = body
                .Where(line => line.TrimStart().StartsWith("- ", StringComparison.Ordinal) || string.Equals(line.Trim(), "-", StringComparison.Ordinal))
                .Select(Indentation)
                .DefaultIfEmpty(-1)
                .First();
            List<string>? current = null;
            foreach (var line in body)
            {
                if (Indentation(line) == itemIndent && line.TrimStart().StartsWith('-'))
                {
                    if (current is not null)
                    {
                        items.Add(new Mapping(current));
                    }

                    // "- key: value" becomes "  key: value" so the item's keys share one indentation.
                    current = [string.Concat(new string(' ', itemIndent), " ", line.TrimStart()[1..])];
                }
                else
                {
                    current?.Add(line);
                }
            }

            if (current is not null)
            {
                items.Add(new Mapping(current));
            }

            return items;
        }

        private (int Start, int End) Range(string key)
        {
            var start = Array.FindIndex(_lines, line =>
                !IsBlankOrComment(line)
                && Indentation(line) == _indent
                && KeyLine().Match(line.TrimStart()) is { Success: true } match
                && string.Equals(match.Groups["key"].Value, key, StringComparison.Ordinal));
            if (start < 0)
            {
                return (-1, -1);
            }

            var end = start + 1;
            while (end < _lines.Length && (IsBlankOrComment(_lines[end]) || Indentation(_lines[end]) > _indent))
            {
                end++;
            }

            return (start, end);
        }
    }
}
