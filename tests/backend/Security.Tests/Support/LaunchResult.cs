namespace Apogeu.Security.Tests.Support;

/// <summary>A successful launch: the URL, the token in it, its expiry and the raw body.</summary>
internal sealed record LaunchResult(Uri LaunchUrl, string LaunchToken, DateTimeOffset ExpiresAt, string Body);
