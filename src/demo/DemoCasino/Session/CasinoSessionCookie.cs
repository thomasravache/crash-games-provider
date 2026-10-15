using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Apogeu.DemoCasino.Session;

/// <summary>
/// The casino session in the <c>casino_session</c> cookie (SPEC-0043 §6): the player protected by Data Protection
/// (encrypted and authenticated, so a tampered value is rejected), HttpOnly, SameSite=Lax, Secure over HTTPS.
/// </summary>
internal sealed class CasinoSessionCookie(IDataProtectionProvider protection, TimeProvider time)
{
    public const string Name = "casino_session";

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private readonly ITimeLimitedDataProtector _protector =
        protection.CreateProtector("Apogeu.DemoCasino.Session.v1").ToTimeLimitedDataProtector();

    public void Write(HttpContext context, CasinoPlayer player)
    {
        ArgumentNullException.ThrowIfNull(context);
        var payload = JsonSerializer.SerializeToUtf8Bytes(player);
        var value = Base64UrlEncode(_protector.Protect(payload, time.GetUtcNow().Add(Lifetime)));
        context.Response.Cookies.Append(Name, value, Options(context, maxAge: Lifetime));
    }

    public CasinoPlayer? Read(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Request.Cookies.TryGetValue(Name, out var value) || string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            var payload = _protector.Unprotect(Base64UrlDecode(value), out _);
            return JsonSerializer.Deserialize<CasinoPlayer>(payload);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }

    public static void Delete(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.Cookies.Delete(Name, Options(context, maxAge: null));
    }

    private static CookieOptions Options(HttpContext context, TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps,
        Path = "/",
        IsEssential = true,
        MaxAge = maxAge,
    };

    private static string Base64UrlEncode(byte[] bytes) => System.Buffers.Text.Base64Url.EncodeToString(bytes);

    private static byte[] Base64UrlDecode(string text) => System.Buffers.Text.Base64Url.DecodeFromChars(text);
}
