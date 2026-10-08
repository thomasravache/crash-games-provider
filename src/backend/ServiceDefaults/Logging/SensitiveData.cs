using System.Collections;
using System.Globalization;
using System.Text;

namespace Apogeu.ServiceDefaults.Logging;

/// <summary>
/// Masking rules shared by the OpenTelemetry log processor and the console formatter (ADR-0014, ADR-0017): the values
/// of sensitive keys become <see cref="SensitiveDataMaskingProcessor.Mask"/>, the keys themselves are kept.
/// </summary>
internal static class SensitiveData
{
    /// <summary>Key of the message template that <c>ILogger</c> adds to structured state.</summary>
    public const string OriginalFormatKey = "{OriginalFormat}";

    /// <summary>
    /// A key is sensitive when it contains one of these words (case-insensitive), so <c>accessToken</c> or
    /// <c>Authorization</c> are masked too. Over-masking is preferred to leaking.
    /// </summary>
    private static readonly string[] SensitiveWords = ["serverSeed", "token", "secret", "signature", "password", "authorization"];

    public static bool IsSensitive(string? key) =>
        key is not null
        && !string.Equals(key, OriginalFormatKey, StringComparison.Ordinal)
        && Array.Exists(SensitiveWords, word => key.Contains(word, StringComparison.OrdinalIgnoreCase));

    public static bool HasSensitiveKey(IReadOnlyList<KeyValuePair<string, object?>>? attributes)
    {
        if (attributes is null)
        {
            return false;
        }

        for (var index = 0; index < attributes.Count; index++)
        {
            if (IsSensitive(attributes[index].Key))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns a copy of <paramref name="attributes"/> with sensitive values masked (same keys, same order).</summary>
    public static List<KeyValuePair<string, object?>> MaskAttributes(IReadOnlyList<KeyValuePair<string, object?>> attributes)
    {
        var masked = new List<KeyValuePair<string, object?>>(attributes.Count);
        foreach (var attribute in attributes)
        {
            masked.Add(IsSensitive(attribute.Key)
                ? new KeyValuePair<string, object?>(attribute.Key, SensitiveDataMaskingProcessor.Mask)
                : attribute);
        }

        return masked;
    }

    /// <summary>
    /// Masks the formatted message of a structured log: when the state carries a sensitive key, the message is
    /// rendered again from its template with the sensitive values replaced; otherwise it is returned unchanged.
    /// </summary>
    public static string? MaskMessage(string? message, IReadOnlyList<KeyValuePair<string, object?>>? attributes)
    {
        if (message is null || attributes is null || !HasSensitiveKey(attributes))
        {
            return message;
        }

        return FindTemplate(attributes) is { } template
            ? RenderMasked(template, attributes)
            : ReplaceSensitiveValues(message, attributes);
    }

    public static string? FindTemplate(IReadOnlyList<KeyValuePair<string, object?>> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (string.Equals(attribute.Key, OriginalFormatKey, StringComparison.Ordinal))
            {
                return attribute.Value as string;
            }
        }

        return null;
    }

    /// <summary>
    /// Renders a message template (<c>"token={token}"</c>) the way <c>ILogger</c> does (invariant culture,
    /// <c>(null)</c> for nulls, enumerables joined by <c>", "</c>), resolving each hole by name.
    /// </summary>
    private static string RenderMasked(string template, IReadOnlyList<KeyValuePair<string, object?>> attributes)
    {
        var output = new StringBuilder(template.Length);
        var position = 0;
        var holeIndex = 0;
        while (position < template.Length)
        {
            var character = template[position];
            if (character is '{' or '}' && position + 1 < template.Length && template[position + 1] == character)
            {
                output.Append(character);
                position += 2;
                continue;
            }

            var close = character == '{' ? template.IndexOf('}', position + 1) : -1;
            if (close < 0)
            {
                output.Append(character);
                position++;
                continue;
            }

            var hole = template.AsSpan(position + 1, close - position - 1);
            var nameEnd = hole.IndexOfAny(',', ':');
            var name = (nameEnd < 0 ? hole : hole[..nameEnd]).ToString();
            var formatSpec = nameEnd < 0 ? string.Empty : hole[nameEnd..].ToString();
            var value = ResolveHole(name, holeIndex, attributes);
            output.Append(string.Format(CultureInfo.InvariantCulture, "{0" + formatSpec + "}", value));
            holeIndex++;
            position = close + 1;
        }

        return output.ToString();
    }

    private static object ResolveHole(string name, int holeIndex, IReadOnlyList<KeyValuePair<string, object?>> attributes)
    {
        if (IsSensitive(name))
        {
            return SensitiveDataMaskingProcessor.Mask;
        }

        foreach (var attribute in attributes)
        {
            if (string.Equals(attribute.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return Render(attribute.Value);
            }
        }

        // Positional fallback (ILogger lists the values in template order); masked if that slot is sensitive.
        if (holeIndex < attributes.Count && !string.Equals(attributes[holeIndex].Key, OriginalFormatKey, StringComparison.Ordinal))
        {
            return IsSensitive(attributes[holeIndex].Key) ? SensitiveDataMaskingProcessor.Mask : Render(attributes[holeIndex].Value);
        }

        return "(null)";
    }

    private static object Render(object? value) => value switch
    {
        null => "(null)",
        string text => text,
        IEnumerable sequence => string.Join(", ", sequence.Cast<object?>().Select(item => item ?? "(null)")),
        _ => value,
    };

    private static string ReplaceSensitiveValues(string message, IReadOnlyList<KeyValuePair<string, object?>> attributes)
    {
        var masked = message;
        foreach (var attribute in attributes)
        {
            if (IsSensitive(attribute.Key)
                && Convert.ToString(attribute.Value, CultureInfo.InvariantCulture) is { Length: > 0 } raw)
            {
                masked = masked.Replace(raw, SensitiveDataMaskingProcessor.Mask, StringComparison.Ordinal);
            }
        }

        return masked;
    }
}
