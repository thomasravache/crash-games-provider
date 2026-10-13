namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>One DemoCasino per test class (the session lives in the cookie, so tests do not interfere).</summary>
public sealed class DemoCasinoFixture : IAsyncDisposable
{
    private readonly Lazy<DemoCasinoHost> _host = new(() => DemoCasinoHost.Start());

    internal DemoCasinoHost Host => _host.Value;

    public async ValueTask DisposeAsync()
    {
        if (_host.IsValueCreated)
        {
            await _host.Value.DisposeAsync().ConfigureAwait(false);
        }
    }
}
