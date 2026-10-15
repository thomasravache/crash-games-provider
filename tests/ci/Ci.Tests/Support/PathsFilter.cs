using System.Text;
using System.Text.RegularExpressions;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Filters of a <c>dorny/paths-filter</c> step (the literal <c>filters: |</c> block): filter name → its globs, with the
/// YAML anchors (<c>&amp;name</c>) and aliases (<c>- *name</c>) resolved. Globs follow the picomatch semantics the
/// action uses with <c>dot: true</c>: <c>**</c> crosses directories, <c>*</c> and <c>?</c> stay within one segment.
/// </summary>
public sealed partial class PathsFilter
{
    private readonly Dictionary<string, IReadOnlyList<string>> _filters;

    private PathsFilter(Dictionary<string, IReadOnlyList<string>> filters) => _filters = filters;

    public IReadOnlyCollection<string> Names => _filters.Keys;

    /// <summary>Reads the <c>filters: |</c> block of the first paths-filter step found in a job block.</summary>
    public static PathsFilter? FromJob(string jobBlock)
    {
        var lines = jobBlock.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => FiltersLine().IsMatch(line));
        if (start < 0)
        {
            return null;
        }

        var keyIndent = Indentation(lines[start]);
        var body = new List<string>();
        for (var index = start + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (!string.IsNullOrWhiteSpace(line) && Indentation(line) <= keyIndent)
            {
                break;
            }

            body.Add(line);
        }

        return Parse(body);
    }

    /// <summary>Globs of a filter (aliases expanded), or an empty list when the filter does not exist.</summary>
    public IReadOnlyList<string> Globs(string filter) => _filters.TryGetValue(filter, out var globs) ? globs : [];

    /// <summary>True when any changed file matches any glob of the filter.</summary>
    public bool Triggers(string filter, params string[] changedFiles) =>
        changedFiles.Any(file => Globs(filter).Any(glob => Matches(glob, file)));

    /// <summary>Picomatch-like glob match (<c>dot: true</c>).</summary>
    public static bool Matches(string glob, string path) =>
        Regex.IsMatch(path, ToRegex(glob), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static PathsFilter Parse(List<string> body)
    {
        var anchors = new Dictionary<string, string>(StringComparer.Ordinal);
        var raw = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;
        foreach (var line in body)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var key = FilterKeyLine().Match(line);
            if (key.Success)
            {
                var name = key.Groups["key"].Value;
                current = [];
                raw[name] = current;
                if (key.Groups["anchor"].Success)
                {
                    anchors[key.Groups["anchor"].Value] = name;
                }

                continue;
            }

            var item = ItemLine().Match(line);
            if (item.Success && current is not null)
            {
                current.Add(item.Groups["item"].Value.Trim().Trim('"', '\''));
            }
        }

        var resolved = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var name in raw.Keys)
        {
            resolved[name] = Expand(name, raw, anchors, []);
        }

        return new PathsFilter(resolved);
    }

    private static List<string> Expand(
        string name,
        Dictionary<string, List<string>> raw,
        Dictionary<string, string> anchors,
        HashSet<string> visiting)
    {
        if (!visiting.Add(name))
        {
            throw new InvalidOperationException($"cyclic alias in paths-filter: {name}");
        }

        var globs = new List<string>();
        foreach (var entry in raw[name])
        {
            if (entry.StartsWith('*') && anchors.TryGetValue(entry[1..], out var target))
            {
                globs.AddRange(Expand(target, raw, anchors, visiting));
            }
            else
            {
                globs.Add(entry);
            }
        }

        visiting.Remove(name);
        return globs;
    }

    private static string ToRegex(string glob)
    {
        var pattern = new StringBuilder("^");
        for (var index = 0; index < glob.Length; index++)
        {
            var c = glob[index];
            if (c == '*' && index + 1 < glob.Length && glob[index + 1] == '*')
            {
                index++;
                if (index + 1 < glob.Length && glob[index + 1] == '/')
                {
                    index++;
                    pattern.Append("(?:.*/)?");
                }
                else
                {
                    pattern.Append(".*");
                }
            }
            else if (c == '*')
            {
                pattern.Append("[^/]*");
            }
            else if (c == '?')
            {
                pattern.Append("[^/]");
            }
            else
            {
                pattern.Append(Regex.Escape(c.ToString()));
            }
        }

        return pattern.Append('$').ToString();
    }

    private static int Indentation(string line) => line.Length - line.TrimStart(' ').Length;

    [GeneratedRegex("""^\s*filters\s*:\s*\|""", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex FiltersLine();

    [GeneratedRegex("""^\s*(?<key>[A-Za-z0-9_-]+)\s*:\s*(&(?<anchor>[A-Za-z0-9_-]+))?\s*$""", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex FilterKeyLine();

    [GeneratedRegex("""^\s*-\s+(?<item>.+?)\s*$""", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex ItemLine();
}
