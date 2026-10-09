using Apogeu.Operators.Features.Configuration;
using Apogeu.Operators.Infrastructure;
using Apogeu.Operators.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace Apogeu.Operators.Features.Launch;

/// <summary>
/// Issues a launch: a new <see cref="LaunchToken"/> whose hash is stored with the <see cref="LaunchSubject"/>, and the launch
/// URL <c>{gameBaseUrl}/?lt={launchToken}</c> that carries the token to the game, valid for <see cref="LaunchGrant.TimeToLive"/>.
/// </summary>
internal sealed class LaunchIssuer(ILaunchGrantStore grants, IOptions<OperatorsOptions> options, TimeProvider timeProvider)
{
    public async Task<LaunchResponse> IssueAsync(LaunchSubject subject, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var gameBaseUrl = options.Value.GameBaseUrl
            ?? throw new InvalidOperationException($"Missing required configuration '{OperatorsOptions.SectionName}:GameBaseUrl'.");
        var token = LaunchToken.Generate();
        var grant = LaunchGrant.Issue(token.Hash, subject, timeProvider.GetUtcNow());
        await grants.AddAsync(
            new LaunchGrantRecord
            {
                TokenHash = grant.TokenHash,
                OperatorId = subject.OperatorId,
                SalaId = subject.SalaId,
                PlayerId = subject.PlayerId,
                Nickname = subject.Nickname,
                Currency = subject.Currency,
                Mode = subject.Mode,
                Language = subject.Language,
                IssuedAt = grant.IssuedAt,
                ExpiresAt = grant.ExpiresAt,
            },
            cancellationToken).ConfigureAwait(false);

        return new LaunchResponse(LaunchUrlOf(gameBaseUrl, token), grant.ExpiresAt);
    }

    private static Uri LaunchUrlOf(Uri gameBaseUrl, LaunchToken token) =>
        new UriBuilder(gameBaseUrl)
        {
            Path = gameBaseUrl.AbsolutePath.TrimEnd('/') + "/",
            Query = "lt=" + token.Value,
            Fragment = string.Empty,
        }.Uri;
}
