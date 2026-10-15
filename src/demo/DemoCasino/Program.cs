using Apogeu.DemoCasino.Apogeu;
using Apogeu.DemoCasino.Lobby;
using Apogeu.DemoCasino.Play;
using Apogeu.DemoCasino.Session;

// Demo casino BFF (SPEC-0043, ADR-0027): the player's casino session in a signed cookie and the HMAC client of the demo
// operator to the Apogeu public API. The operator credentials come from configuration (AppHost parameters or user
// secrets): Apogeu:BaseUrl, Apogeu:OperatorKeyId, Apogeu:OperatorSecret. They never leave this process. The lobby
// (SPEC-0044) lists the games of the signed Apogeu catalog; DemoCasino:Lobby:NewGames marks the new ones.
// The game page (SPEC-0045) gets a single-use launch URL from the signed launch of the Apogeu.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApogeuClient();
builder.Services.AddCasinoSession();
builder.Services.AddLobby();
builder.Services.AddPlay();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapCasinoSession();
app.MapLobby();
app.MapPlay();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Entry point, public so the tests reach it through <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
