using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Apogeu.Betting.Tests.Support;

/// <summary><c>GET /api/v1/me/bets</c> (SPEC-0025 §6) with the session of the client: one page, its status and raw body.</summary>
internal static class MyBetsApi
{
    public const string Path = "/api/v1/me/bets";

    /// <summary>The request URI of a page of <paramref name="limit"/> bets before <paramref name="before"/> (both optional).</summary>
    public static Uri PageUri(int? limit, string? before = null)
    {
        var query = new List<string>();
        if (limit is { } value)
        {
            query.Add("limit=" + value.ToString(CultureInfo.InvariantCulture));
        }

        if (before is not null)
        {
            query.Add("before=" + Uri.EscapeDataString(before));
        }

        return new Uri(query.Count == 0 ? Path : Path + "?" + string.Join('&', query), UriKind.Relative);
    }

    public static async Task<(HttpStatusCode Status, string Body)> GetAsync(HttpClient client, int? limit, string? before, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var response = await client.GetAsync(PageUri(limit, before), ct).ConfigureAwait(false);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }

    /// <summary>A <c>200</c> page: its items and its <c>nextCursor</c> (<see langword="null"/> when absent or null).</summary>
    public static async Task<(IReadOnlyList<JsonElement> Items, string? NextCursor)> PageAsync(HttpClient client, int? limit, string? before, CancellationToken ct)
    {
        var (status, body) = await GetAsync(client, limit, before, ct).ConfigureAwait(false);
        Assert.True(status == HttpStatusCode.OK, $"Expected 200 from GET {PageUri(limit, before)}, got {(int)status}: {body}");
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var items = root.GetProperty("items").EnumerateArray().Select(item => item.Clone()).ToList();
        var next = root.TryGetProperty("nextCursor", out var cursor) && cursor.ValueKind == JsonValueKind.String ? cursor.GetString() : null;
        return (items, next);
    }

    /// <summary>Every page of <paramref name="limit"/> bets, following <c>nextCursor</c> until it is absent (at most 50 pages).</summary>
    public static async Task<IReadOnlyList<IReadOnlyList<JsonElement>>> AllPagesAsync(HttpClient client, int limit, CancellationToken ct)
    {
        var pages = new List<IReadOnlyList<JsonElement>>();
        string? cursor = null;
        do
        {
            var (items, next) = await PageAsync(client, limit, cursor, ct).ConfigureAwait(false);
            pages.Add(items);
            cursor = next;
        }
        while (cursor is not null && pages.Count < 50);

        Assert.True(cursor is null, "The history did not end within 50 pages.");
        return pages;
    }
}
