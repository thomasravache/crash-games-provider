namespace Apogeu.Operators.Features.Configuration;

/// <summary>Configuration section <c>Operators:Demo</c>: keys come from secret parameters (ADR-0014), never from files in the repository.</summary>
public sealed class DemoOperatorOptions
{
    /// <summary>Active API keys (two during a rotation, ADR-0014).</summary>
    public IList<OperatorApiKeyOptions> ApiKeys { get; } = [];

    /// <summary>Origins allowed to frame the game (CSP <c>frame-ancestors</c>).</summary>
    public IList<string> FrameAncestors { get; } = [];
}
