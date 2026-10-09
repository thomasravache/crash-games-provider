using System.Text;
using System.Text.Json;

namespace Apogeu.Security.Tests.Support;

/// <summary>Launch request bodies of the contract (SPEC-0009 §6), as the exact bytes that are signed and sent.</summary>
internal static class LaunchBodies
{
    public static byte[] Demo(string playerId, string? nickname = "Pipeiro") =>
        Json(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["playerId"] = playerId,
            ["nickname"] = nickname,
            ["currency"] = "BRL",
            ["gameCode"] = "pipa",
            ["mode"] = "demo",
            ["language"] = "pt-BR",
        });

    public static byte[] RealMoney(string playerId) =>
        Json(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["playerId"] = playerId,
            ["currency"] = "BRL",
            ["gameCode"] = "pipa",
            ["mode"] = "real",
            ["language"] = "pt-BR",
        });

    public static byte[] WithoutPlayerId() =>
        Json(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["currency"] = "BRL",
            ["gameCode"] = "pipa",
            ["mode"] = "demo",
            ["language"] = "pt-BR",
        });

    public static string NewPlayerId() => "player-" + Guid.NewGuid().ToString("N");

    private static byte[] Json(Dictionary<string, object?> body) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body));
}
