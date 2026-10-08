namespace Apogeu.TestKit.Builders;

/// <summary>
/// Base of the test data builders: each builder starts from valid defaults, the test overrides only what it is about
/// (fluent <c>With…</c> methods on the derived builder) and calls <see cref="Build"/>.
/// </summary>
/// <typeparam name="T">The type the builder creates.</typeparam>
public abstract class TestDataBuilder<T>
{
    /// <summary>Creates a new instance from the current state of the builder.</summary>
    public abstract T Build();

    /// <summary>Creates <paramref name="count"/> instances from the current state of the builder.</summary>
    public IReadOnlyList<T> BuildMany(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return [.. Enumerable.Range(0, count).Select(_ => Build())];
    }
}
