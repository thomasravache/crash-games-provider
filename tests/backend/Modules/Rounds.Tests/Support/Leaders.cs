namespace Apogeu.Rounds.Tests.Support;

/// <summary>Which instance leads the room <c>demo</c>.</summary>
internal static class Leaders
{
    /// <summary>Waits until exactly one of <paramref name="hosts"/> leads, and returns it.</summary>
    public static async Task<RoundsHost> SingleAsync(IReadOnlyList<RoundsHost> hosts, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        await Eventually.HoldsAsync(
            () => hosts.Count(h => h.IsLeading) == 1,
            timeout,
            () => "exactly one leader among " + string.Join(", ", hosts.Select(h => $"{h.Name}={h.IsLeading}"))).ConfigureAwait(false);
        return hosts.Single(h => h.IsLeading);
    }
}
