using Apogeu.DemoCasino.Apogeu;
using Apogeu.DemoCasino.Session;

// Demo casino BFF (SPEC-0043, ADR-0027): the player's casino session in a signed cookie and the HMAC client of the demo
// operator to the Apogeu public API. The operator credentials come from configuration (AppHost parameters or user
// secrets): Apogeu:BaseUrl, Apogeu:OperatorKeyId, Apogeu:OperatorSecret. They never leave this process.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApogeuClient();
builder.Services.AddCasinoSession();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapCasinoSession();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Entry point, public so the tests reach it through <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
