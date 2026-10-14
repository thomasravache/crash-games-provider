using System.Buffers.Binary;
using System.Globalization;
using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Sessions;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Apogeu.Betting.Features.MyBets;

/// <summary>
/// <c>GET /api/v1/me/bets?limit=1..50&amp;before=&lt;cursor&gt;</c> (player session): the personal history of the session's
/// player (SPEC-0025 §6); <c>400 INVALID_LIMIT</c> for a limit that is not a number in 1..50, <c>401</c> without a session.
/// </summary>
internal static class GetMyBetsEndpoint
{
    public const string Route = "/api/v1/me/bets";

    /// <summary>Smallest <c>limit</c>.</summary>
    public const int MinLimit = 1;

    /// <summary>Largest <c>limit</c>.</summary>
    public const int MaxLimit = 50;

    /// <summary>The page size when <c>limit</c> is not given.</summary>
    public const int DefaultLimit = 20;

    /// <summary>Bytes of a cursor: the UTC ticks of <c>placedAt</c> and the 16 bytes of the <c>betId</c>.</summary>
    private const int CursorBytes = sizeof(long) + 16;

    public static IEndpointRouteBuilder MapGetMyBets(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(Route, Handle)
            .RequireAuthorization(SecurityPolicies.PlayerSession)
            .WithName("GetMyBets")
            .WithTags("Betting")
            .WithSummary("The bets of the session player, most recent first, paged by an opaque cursor.")
            .Produces<MyBetsPage>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest, ErrorResponse.ContentType)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized, ErrorResponse.ContentType);

        return endpoints;
    }

    private static async Task<Results<Ok<MyBetsPage>, JsonHttpResult<ErrorResponse>>> Handle(
        HttpContext http, IBetStore store, string? limit, string? before, CancellationToken ct)
    {
        // Parsed here, not bound as a number, so a limit that is not one is INVALID_LIMIT in the error envelope (Emenda v4).
        var size = DefaultLimit;
        if (limit is not null
            && (!int.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out size) || size is < MinLimit or > MaxLimit))
        {
            return BadRequest(BettingErrorCodes.InvalidLimit, $"limit must be between {MinLimit} and {MaxLimit}.");
        }

        PlayerBetsCursor? cursor = null;
        if (before is not null && (cursor = DecodeCursor(before)) is null)
        {
            return BadRequest(BettingErrorCodes.InvalidCursor, "before is not a cursor of this history.");
        }

        // Only the session's own bets: the player comes from the token, never from the request.
        var session = BettingSession.From(http.User);

        // One more than the page tells whether another page follows.
        var bets = await store.ListPlayerBetsAsync(session.Owner, cursor, size + 1, ct).ConfigureAwait(false);
        var page = bets.Take(size).ToList();
        var next = bets.Count > size ? EncodeCursor(page[^1]) : null;
        return TypedResults.Ok(new MyBetsPage([.. page.Select(ToItem)], next));
    }

    private static MyBetItem ToItem(PlayerBet found)
    {
        var bet = found.Bet;
        return new MyBetItem(
            bet.Id,
            bet.RoundId?.Value,
            bet.RoundNumber,
            bet.Panel,
            new Money(bet.Amount),
            bet.Owner.Currency.Code,
            bet.AutoCashOut is { } auto ? new Multiplier(auto) : null,
            bet.CashOutMultiplier is { } multiplier ? new Multiplier(multiplier) : null,
            bet.Payout is { } payout ? new Money(payout) : null,
            StatusOf(bet.Status),
            found.PlacedAt);
    }

    private static MyBetStatus StatusOf(BetStatus status) => status switch
    {
        BetStatus.Placed => MyBetStatus.Placed,
        BetStatus.Queued => MyBetStatus.Queued,
        BetStatus.Active => MyBetStatus.Active,
        BetStatus.CashedOut => MyBetStatus.CashedOut,
        BetStatus.Lost => MyBetStatus.Lost,
        BetStatus.Cancelled => MyBetStatus.Cancelled,
        BetStatus.Refunded => MyBetStatus.Refunded,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown bet status."),
    };

    /// <summary>The opaque cursor after <paramref name="last"/>: base64url of its acceptance ticks and its id.</summary>
    private static string EncodeCursor(PlayerBet last)
    {
        Span<byte> bytes = stackalloc byte[CursorBytes];
        BinaryPrimitives.WriteInt64BigEndian(bytes, last.PlacedAt.UtcTicks);
        _ = last.Bet.Id.TryWriteBytes(bytes[sizeof(long)..], bigEndian: true, out _);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static PlayerBetsCursor? DecodeCursor(string cursor)
    {
        var base64 = cursor.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        Span<byte> bytes = stackalloc byte[CursorBytes + 3];
        if (!Convert.TryFromBase64String(base64, bytes, out var written) || written != CursorBytes)
        {
            return null;
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes);
        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return null;
        }

        return new PlayerBetsCursor(new DateTimeOffset(ticks, TimeSpan.Zero), new Guid(bytes[sizeof(long)..CursorBytes], bigEndian: true));
    }

    private static JsonHttpResult<ErrorResponse> BadRequest(string code, string title) =>
        TypedResults.Json(new ErrorResponse(title, StatusCodes.Status400BadRequest, code), statusCode: StatusCodes.Status400BadRequest, contentType: ErrorResponse.ContentType);
}
