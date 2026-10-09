namespace Apogeu.Operators.Features.Launch;

/// <summary>Body of <c>200</c> of <c>POST /api/v1/operator/launch</c>: <c>launchUrl = {gameBaseUrl}/?lt={launchToken}</c>.</summary>
public sealed record LaunchResponse(Uri LaunchUrl, DateTimeOffset ExpiresAt);
