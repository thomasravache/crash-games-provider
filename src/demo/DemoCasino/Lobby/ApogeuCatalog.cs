using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text.Json;
using Apogeu.DemoCasino.Apogeu;
using Microsoft.Extensions.Options;

namespace Apogeu.DemoCasino.Lobby;

/// <summary>
/// The games of the lobby (SPEC-0044 §6): the signed <c>GET /api/v1/games</c> of the Apogeu through the "Apogeu" client,
/// mapped by <see cref="LobbyGame.Of"/> and kept for <see cref="Lifetime"/> by the <see cref="TimeProvider"/> of DI. A
/// failure of the Apogeu (no connection, an error status, an unreadable body) is not cached: it is counted, logged and
/// reported as <see cref="ProviderUnavailableException"/>.
/// </summary>
internal sealed partial class ApogeuCatalog : IDisposable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private const string GamesPath = "api/v1/games";

    private readonly IHttpClientFactory _clients;
    private readonly IOptions<LobbyOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<ApogeuCatalog> _logger;
    private readonly Meter _meter;
    private readonly Counter<long> _failures;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private volatile Entry? _current;

    public ApogeuCatalog(
        IHttpClientFactory clients,
        IOptions<LobbyOptions> options,
        TimeProvider time,
        ILogger<ApogeuCatalog> logger,
        IMeterFactory meters)
    {
        ArgumentNullException.ThrowIfNull(meters);
        _clients = clients;
        _options = options;
        _time = time;
        _logger = logger;
        _meter = meters.Create("Apogeu.DemoCasino.Lobby");
        _failures = _meter.CreateCounter<long>(
            "democasino.apogeu.failures",
            unit: "{failure}",
            description: "Calls to the Apogeu catalog that failed (SPEC-0044 §8).");
    }

    /// <summary>The lobby games, from the cache while it is younger than <see cref="Lifetime"/>.</summary>
    public async Task<IReadOnlyList<LobbyGame>> GamesAsync(CancellationToken cancellationToken)
    {
        if (Fresh(_current) is { } cached)
        {
            return cached;
        }

        await _refresh.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Fresh(_current) is { } refreshed)
            {
                return refreshed;
            }

            var games = await FetchAsync(cancellationToken).ConfigureAwait(false);
            _current = new Entry(_time.GetUtcNow(), games);
            return games;
        }
        finally
        {
            _refresh.Release();
        }
    }

    public void Dispose()
    {
        _refresh.Dispose();
        _meter.Dispose();
    }

    private IReadOnlyList<LobbyGame>? Fresh(Entry? entry) =>
        entry is not null && _time.GetUtcNow() - entry.FetchedAt < Lifetime ? entry.Games : null;

    private async Task<IReadOnlyList<LobbyGame>> FetchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<CatalogGame>? catalog;
        try
        {
            var client = _clients.CreateClient(ApogeuClientRegistration.ClientName);
            using var response = await client.GetAsync(new Uri(GamesPath, UriKind.Relative), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw Unavailable($"status {(int)response.StatusCode}", exception: null);
            }

            catalog = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<CatalogGame>>(JsonSerializerOptions.Web, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw Unavailable(exception.GetType().Name, exception);
        }

        if (catalog is null || catalog.Any(game => game is null || game.Code is null || game.Name is null))
        {
            throw Unavailable("invalid catalog body", exception: null);
        }

        var newGames = _options.Value.NewGameCodes();
        return [.. catalog.Select(game => LobbyGame.Of(game, newGames))];
    }

    private ProviderUnavailableException Unavailable(string reason, Exception? exception)
    {
        _failures.Add(1);
        LogCatalogFailed(_logger, reason, exception);
        return new ProviderUnavailableException(reason, exception);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Apogeu catalog is unavailable: {Reason}.")]
    private static partial void LogCatalogFailed(ILogger logger, string reason, Exception? exception);

    private sealed record Entry(DateTimeOffset FetchedAt, IReadOnlyList<LobbyGame> Games);
}
