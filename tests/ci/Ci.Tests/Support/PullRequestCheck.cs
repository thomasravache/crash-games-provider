using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Apogeu.Ci.Tests.Support;

/// <summary>One check of a pull request, as reported by <c>gh pr checks --json</c>.</summary>
/// <param name="Bucket">pass | fail | pending | skipping | cancel.</param>
public sealed partial record PullRequestCheck(
    string Workflow,
    string Name,
    string Bucket,
    string Link,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt)
{
    public const string Fields = "workflow,name,bucket,state,link,startedAt,completedAt";

    public bool IsPending => string.Equals(Bucket, "pending", StringComparison.OrdinalIgnoreCase);

    public bool Skipped => string.Equals(Bucket, "skipping", StringComparison.OrdinalIgnoreCase);

    public bool Passed => string.Equals(Bucket, "pass", StringComparison.OrdinalIgnoreCase);

    public bool Failed => string.Equals(Bucket, "fail", StringComparison.OrdinalIgnoreCase);

    /// <summary>Job id taken from the check link (…/actions/runs/{run}/job/{job}), used to read its log.</summary>
    public string? JobId
    {
        get
        {
            var match = JobLink().Match(Link);
            return match.Success ? match.Groups["job"].Value : null;
        }
    }

    /// <summary>True when the check is job <paramref name="job"/> of workflow <paramref name="workflow"/>.</summary>
    public bool Is(string workflow, string job) =>
        (string.Equals(Workflow, workflow, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Name, job, StringComparison.OrdinalIgnoreCase))
        || string.Equals(Name, $"{workflow}/{job}", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Name, $"{workflow} / {job}", StringComparison.OrdinalIgnoreCase);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Workflow}/{Name}: {Bucket} ({Link})");

    public static IReadOnlyList<PullRequestCheck> ParseAll(JsonElement array) =>
        array.EnumerateArray().Select(Parse).ToList();

    private static PullRequestCheck Parse(JsonElement item) =>
        new(
            Text(item, "workflow"),
            Text(item, "name"),
            Text(item, "bucket"),
            Text(item, "link"),
            Time(item, "startedAt"),
            Time(item, "completedAt"));

    private static string Text(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static DateTimeOffset? Time(JsonElement item, string property)
    {
        var text = Text(item, property);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) && value.Year > 2000
            ? value
            : null;
    }

    [GeneratedRegex("/job/(?<job>[0-9]+)", RegexOptions.None, 1000)]
    private static partial Regex JobLink();
}
