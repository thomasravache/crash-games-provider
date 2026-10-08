var builder = DistributedApplication.CreateBuilder(args);

// SPEC-0005 scaffold: resources (postgres, redis, keycloak, api, game-pipa, backoffice, demo-site) are not declared yet.

await builder.Build().RunAsync().ConfigureAwait(false);
