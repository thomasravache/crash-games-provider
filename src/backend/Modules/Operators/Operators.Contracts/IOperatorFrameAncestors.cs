namespace Apogeu.Operators.Contracts;

/// <summary>
/// Origins the operators allow to frame the game (CSP <c>frame-ancestors</c>, SPEC-0009 §5), configured per operator. The
/// host writes them in the <c>Content-Security-Policy</c> of its responses.
/// </summary>
public interface IOperatorFrameAncestors
{
    /// <summary>Allowed origins (<c>scheme://host[:port]</c>), without duplicates; empty when no operator allows framing.</summary>
    IReadOnlyList<string> Origins { get; }
}
