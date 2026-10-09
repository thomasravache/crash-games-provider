using System.Text.RegularExpressions;
using Apogeu.Repository.Tests.Support;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Minimal, line-based reader of a GitHub Actions workflow: enough to find top-level keys, the job blocks and the
/// <c>uses:</c> references without a YAML dependency. Syntax validity is actionlint's job, not this reader's.
/// </summary>
public sealed partial class WorkflowFile
{
    public const string Directory = ".github/workflows";

    private readonly string[] _lines;

    private WorkflowFile(string name, string path, string text)
    {
        Name = name;
        Path = path;
        Text = text;
        _lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    public string Name { get; }

    public string Path { get; }

    public string Text { get; }

    public static string WorkflowsDirectory => RepositoryRoot.Combine(".github", "workflows");

    /// <summary>Loads a workflow of the repository, or returns null when the file does not exist.</summary>
    public static WorkflowFile? TryLoad(string fileName)
    {
        var path = System.IO.Path.Combine(WorkflowsDirectory, fileName);
        return File.Exists(path) ? new WorkflowFile(fileName, path, File.ReadAllText(path)) : null;
    }

    public static IReadOnlyList<WorkflowFile> LoadAll(string directory)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            return [];
        }

        return System.IO.Directory
            .EnumerateFiles(directory)
            .Where(file => file.EndsWith(".yml", StringComparison.Ordinal) || file.EndsWith(".yaml", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(file => new WorkflowFile(System.IO.Path.GetFileName(file), file, File.ReadAllText(file)))
            .ToList();
    }

    /// <summary>Block of a top-level key (the key line and its indented lines), or null when absent.</summary>
    public string? TopLevelBlock(string key)
    {
        var start = Array.FindIndex(_lines, line => IsTopLevelKey(line, key));
        if (start < 0)
        {
            return null;
        }

        var end = start + 1;
        while (end < _lines.Length && !IsTopLevelLine(_lines[end]))
        {
            end++;
        }

        return string.Join('\n', _lines[start..end]);
    }

    /// <summary>Jobs of the workflow: job id → its block of lines.</summary>
    public IReadOnlyDictionary<string, string> Jobs()
    {
        var jobs = new Dictionary<string, string>(StringComparer.Ordinal);
        var block = TopLevelBlock("jobs");
        if (block is null)
        {
            return jobs;
        }

        var lines = block.Split('\n')[1..];
        var jobIndent = lines
            .Where(line => !IsBlankOrComment(line))
            .Select(Indentation)
            .DefaultIfEmpty(-1)
            .First();
        if (jobIndent <= 0)
        {
            return jobs;
        }

        string? current = null;
        var buffer = new List<string>();
        foreach (var line in lines)
        {
            var match = KeyLine().Match(line);
            if (!IsBlankOrComment(line) && Indentation(line) == jobIndent && match.Success)
            {
                Flush();
                current = match.Groups["key"].Value;
            }

            buffer.Add(line);
        }

        Flush();
        return jobs;

        void Flush()
        {
            if (current is not null)
            {
                jobs[current] = string.Join('\n', buffer);
            }

            buffer.Clear();
        }
    }

    /// <summary>Every <c>uses:</c> reference (steps and reusable workflows), with its 1-based line number.</summary>
    public IReadOnlyList<(int Line, string Reference)> UsesReferences() =>
        _lines
            .Select((line, index) => (Line: index + 1, Match: UsesLine().Match(line)))
            .Where(item => item.Match.Success)
            .Select(item => (item.Line, item.Match.Groups["ref"].Value))
            .ToList();

    private static bool IsTopLevelKey(string line, string key)
    {
        var match = KeyLine().Match(line);
        return match.Success && Indentation(line) == 0 && string.Equals(match.Groups["key"].Value, key, StringComparison.Ordinal);
    }

    private static bool IsTopLevelLine(string line) => !IsBlankOrComment(line) && Indentation(line) == 0;

    private static bool IsBlankOrComment(string line) =>
        string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#');

    private static int Indentation(string line) => line.Length - line.TrimStart(' ').Length;

    [GeneratedRegex("""^\s*["']?(?<key>[A-Za-z0-9_-]+)["']?\s*:(\s|$)""", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex KeyLine();

    [GeneratedRegex("""^\s*(-\s+)?uses\s*:\s*["']?(?<ref>[^"'\s#]+)""", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex UsesLine();
}
