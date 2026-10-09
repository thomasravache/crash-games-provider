using Apogeu.BuildingBlocks.Security;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Features.Authentication;
using Apogeu.Operators.Features.Backoffice;
using Apogeu.Operators.Features.Configuration;
using Apogeu.Operators.Features.Development;
using Apogeu.Operators.Features.Hubs;
using Apogeu.Operators.Features.Launch;
using Apogeu.Operators.Features.Observability;
using Apogeu.Operators.Features.Sessions;
using Apogeu.Operators.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Apogeu.Operators.Features;

/// <summary>Entry points the Host calls to compose the Operators module (ADR-0001).</summary>
public static class OperatorsModule
{
    /// <summary>Name of the connection string of the module database (<c>ConnectionStrings:apogeu</c>).</summary>
    public const string ConnectionName = "apogeu";

    /// <summary>Name of the connection string of Redis (<c>ConnectionStrings:redis</c>).</summary>
    public const string RedisConnectionName = "redis";

    /// <summary>Meter of the security metrics (<c>security.launch_rejected</c>, <c>security.replay_detected</c>, <c>security.rate_limited</c>).</summary>
    public const string MeterName = OperatorsMetrics.MeterName;

    /// <summary>
    /// Registers the three identities of ADR-0015: the HMAC signature of operator systems, the player session (also on the
    /// hubs, with the sala policy as a global hub filter) and the backoffice bearer of Keycloak, their policies, and the
    /// launch and session stores. In Development the schema <c>operators</c> is migrated and the demo operator seeded at
    /// startup. Secrets and endpoints come from <c>Operators</c>, <c>Security:PlayerSession</c> and <c>Security:Backoffice</c>.
    /// </summary>
    public static IServiceCollection AddOperatorsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OperatorsOptions>().BindConfiguration(OperatorsOptions.SectionName);
        services.AddOptions<PlayerSessionOptions>().BindConfiguration(PlayerSessionOptions.SectionName);
        services.AddOptions<BackofficeAuthenticationOptions>().BindConfiguration(BackofficeAuthenticationOptions.SectionName);

        services.AddOperatorsInfrastructure(
            provider => RequiredConnectionString(provider, ConnectionName),
            provider => RequiredConnectionString(provider, RedisConnectionName));
        services.AddMetrics();
        services.AddSingleton<OperatorsMetrics>();
        services.AddSingleton<OperatorApiKeys>();
        services.AddSingleton(provider => new OperatorRequestVerifier(provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IOperatorFrameAncestors, OperatorFrameAncestors>();
        services.AddSingleton<PlayerSessionSigningKey>();
        services.AddSingleton<PlayerSessionTokens>();
        services.AddScoped<LaunchIssuer>();
        services.AddHostedService<OperatorsDevelopmentSetup>();
        services.Configure<HubOptions>(options => options.AddFilter<SalaAccessHubFilter>());

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, OperatorSignatureAuthenticationHandler>(OperatorsAuthentication.OperatorSignatureScheme, configureOptions: null)
            .AddJwtBearer(OperatorsAuthentication.PlayerSessionScheme)
            .AddJwtBearer(OperatorsAuthentication.BackofficeScheme);
        services.AddOptions<JwtBearerOptions>(OperatorsAuthentication.PlayerSessionScheme).Configure<PlayerSessionTokens>(ConfigurePlayerSession);
        services.AddOptions<JwtBearerOptions>(OperatorsAuthentication.BackofficeScheme).Configure<IOptions<BackofficeAuthenticationOptions>>(ConfigureBackoffice);

        services.AddAuthorizationBuilder()
            .AddPolicy(OperatorsAuthentication.OperatorPolicy, policy => policy
                .AddAuthenticationSchemes(OperatorsAuthentication.OperatorSignatureScheme)
                .RequireAuthenticatedUser())
            .AddPolicy(OperatorsAuthentication.PlayerPolicy, policy => policy
                .AddAuthenticationSchemes(OperatorsAuthentication.PlayerSessionScheme)
                .RequireAuthenticatedUser())
            .AddPolicy(OperatorsAuthentication.BackofficePolicy, policy => policy
                .AddAuthenticationSchemes(OperatorsAuthentication.BackofficeScheme)
                .RequireAuthenticatedUser()
                .RequireAssertion(context => BackofficeClaims.RolesOf(context.User).Count > 0));

        return services;
    }

    public static IEndpointRouteBuilder MapOperatorsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapLaunch();
        endpoints.MapCreateSession();
        endpoints.MapGetMe();
        endpoints.MapLaunchDemo();

        return endpoints;
    }

    private static string RequiredConnectionString(IServiceProvider provider, string name) =>
        provider.GetRequiredService<IConfiguration>().GetConnectionString(name)
        ?? throw new InvalidOperationException($"Missing required configuration 'ConnectionStrings:{name}'.");

    private static void ConfigurePlayerSession(JwtBearerOptions options, PlayerSessionTokens tokens)
    {
        options.MapInboundClaims = false;
        tokens.Configure(options.TokenValidationParameters);
        options.Events = new JwtBearerEvents
        {
            // Browsers cannot set headers on WebSockets: on the hubs the session travels as access_token in the query.
            OnMessageReceived = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase)
                    && context.Request.Query.TryGetValue("access_token", out var token)
                    && !string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    }

    private static void ConfigureBackoffice(JwtBearerOptions options, IOptions<BackofficeAuthenticationOptions> backoffice)
    {
        var configured = backoffice.Value;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = configured.RequireHttpsMetadata;
        if (configured.Authority is { } authority)
        {
            options.Authority = authority.AbsoluteUri.TrimEnd('/');
        }

        // Without an audience nothing validates: a backoffice that is not configured accepts no token (deny by default).
        options.Audience = configured.Audience;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.RequireSignedTokens = true;
        options.TokenValidationParameters.NameClaimType = "preferred_username";
    }
}
