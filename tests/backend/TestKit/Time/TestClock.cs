using Microsoft.Extensions.Time.Testing;

namespace Apogeu.TestKit.Time;

/// <summary>
/// Controlled time for tests (ADR-0009): production code receives a <see cref="TimeProvider"/> and the test passes a
/// <see cref="FakeTimeProvider"/> that only moves when the test calls <c>Advance</c> or <c>SetUtcNow</c>.
/// </summary>
public static class TestClock
{
    /// <summary>Fixed instant every test clock starts at, unless the test chooses another one.</summary>
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a clock frozen at <paramref name="start"/> (UTC), or at <see cref="Epoch"/>.</summary>
    public static FakeTimeProvider Create(DateTimeOffset? start = null) => new(start ?? Epoch);
}
