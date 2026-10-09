using Apogeu.Operators.Contracts;
using Apogeu.Operators.Features.Configuration;
using Microsoft.Extensions.Options;

namespace Apogeu.Operators.Features.Authentication;

/// <summary>The frame ancestors of the operators, from <c>Operators:Demo:FrameAncestors</c>; entries that are not an origin are ignored.</summary>
internal sealed class OperatorFrameAncestors(IOptions<OperatorsOptions> options) : IOperatorFrameAncestors
{
    private readonly Lazy<IReadOnlyList<string>> _origins = new(() => Parse(options.Value.Demo.FrameAncestors));

    public IReadOnlyList<string> Origins => _origins.Value;

    private static string[] Parse(IEnumerable<string> configured) =>
        configured
            .Select(value => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
                ? uri.GetLeftPart(UriPartial.Authority)
                : null)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
