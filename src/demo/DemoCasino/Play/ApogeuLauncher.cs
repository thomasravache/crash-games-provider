using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apogeu.DemoCasino.Apogeu;
using Apogeu.DemoCasino.Lobby;
using Apogeu.DemoCasino.Session;

namespace Apogeu.DemoCasino.Play;

/// <summary>
/// The signed launch of SPEC-0045 §4: <c>POST /api/v1/operator/launch</c> of the Apogeu through the "Apogeu" client (which
/// signs it), with the <see cref="LaunchRequest"/> of the session. Returns the single-use <c>launchUrl</c>. A
/// <c>400 GAME_NOT_AVAILABLE</c> becomes <see cref="GameNotAvailableException"/>; any other failure (no connection, an error
/// status, an unreadable body) is counted, logged and reported as <see cref="ProviderUnavailableException"/>. The launch URL
/// carries the launch token, so it is never logged.
/// </summary>
internal sealed partial class ApogeuLauncher : IDisposable
{
    public const string GameNotAvailable = "GAME_NOT_AVAILABLE";

    private const string LaunchPath = "api/v1/operator/launch";

    private readonly IHttpClientFactory _clients;
    private readonly ILogger<ApogeuLauncher> _logger;
    private readonly Meter _meter;
    private readonly Counter<long> _launches;
    private readonly Counter<long> _failures;

    public ApogeuLauncher(IHttpClientFactory clients, ILogger<ApogeuLauncher> logger, IMeterFactory meters)
    {
        ArgumentNullException.ThrowIfNull(meters);
        _clients = clients;
        _logger = logger;
        _meter = meters.Create("Apogeu.DemoCasino.Play");
        _launches = _meter.CreateCounter<long>(
            "democasino.launches",
            unit: "{launch}",
            description: "Game launches the Apogeu accepted (SPEC-0045 §8).");
        _failures = _meter.CreateCounter<long>(
            "democasino.launch.failures",
            unit: "{failure}",
            description: "Game launches that failed, by reason (SPEC-0045 §8).");
    }

    /// <summary>Launches <paramref name="gameCode"/> for <paramref name="player"/> and returns the absolute launch URL.</summary>
    public async Task<Uri> LaunchAsync(CasinoPlayer player, string gameCode, CancellationToken cancellationToken)
    {
        var request = LaunchRequest.For(player, gameCode);
        Uri? launchUrl;
        try
        {
            var client = _clients.CreateClient(ApogeuClientRegistration.ClientName);
            using var response = await client
                .PostAsJsonAsync(new Uri(LaunchPath, UriKind.Relative), request, JsonSerializerOptions.Web, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest
                    && string.Equals(await ErrorCodeAsync(response, cancellationToken).ConfigureAwait(false), GameNotAvailable, StringComparison.Ordinal))
                {
                    _failures.Add(1, new KeyValuePair<string, object?>("reason", GameNotAvailable));
                    LogGameNotAvailable(_logger, player.PlayerId, gameCode);
                    throw new GameNotAvailableException(gameCode);
                }

                throw Unavailable($"status {(int)response.StatusCode}", exception: null);
            }

            var body = await response.Content
                .ReadFromJsonAsync<LaunchResponse>(JsonSerializerOptions.Web, cancellationToken)
                .ConfigureAwait(false);
            launchUrl = Uri.TryCreate(body?.LaunchUrl, UriKind.Absolute, out var parsed) ? parsed : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw Unavailable(exception.GetType().Name, exception);
        }

        if (launchUrl is null || !(Uri.UriSchemeHttps.Equals(launchUrl.Scheme, StringComparison.OrdinalIgnoreCase) || Uri.UriSchemeHttp.Equals(launchUrl.Scheme, StringComparison.OrdinalIgnoreCase)))
        {
            throw Unavailable("invalid launch body", exception: null);
        }

        _launches.Add(1);
        LogLaunched(_logger, player.PlayerId, gameCode);
        return launchUrl;
    }

    public void Dispose() => _meter.Dispose();

    /// <summary>The <c>code</c> of an error body of the Apogeu (problem+json of SPEC-0009), or null when unreadable.</summary>
    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content
                .ReadFromJsonAsync<ApogeuError>(JsonSerializerOptions.Web, cancellationToken)
                .ConfigureAwait(false);
            return error?.Code;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private ProviderUnavailableException Unavailable(string reason, Exception? exception)
    {
        _failures.Add(1, new KeyValuePair<string, object?>("reason", LobbyEndpoints.ProviderUnavailable));
        LogLaunchFailed(_logger, reason, exception);
        return new ProviderUnavailableException(reason, exception);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Game launched. PlayerId={PlayerId} GameCode={GameCode}")]
    private static partial void LogLaunched(ILogger logger, string playerId, string gameCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Game not available. PlayerId={PlayerId} GameCode={GameCode}")]
    private static partial void LogGameNotAvailable(ILogger logger, string playerId, string gameCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Apogeu launch is unavailable: {Reason}.")]
    private static partial void LogLaunchFailed(ILogger logger, string reason, Exception? exception);

    private sealed record LaunchResponse(string? LaunchUrl);

    private sealed record ApogeuError(string? Code);
}
